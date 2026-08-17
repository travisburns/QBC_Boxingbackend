using System.Net;
using System.Text;
using Microsoft.Extensions.DependencyInjection;
using QBC.Api.Data;
using QBC.Api.Tests.Infrastructure;
using Xunit;

namespace QBC.Api.Tests;

/// <summary>
/// The Square webhook endpoint: signature is enforced, valid events are accepted,
/// and each event id is processed at most once (idempotency log).
/// </summary>
public sealed class WebhookTests(TestWebAppFactory factory) : IClassFixture<TestWebAppFactory>
{
    private readonly TestWebAppFactory _factory = factory;

    private static HttpRequestMessage WebhookPost(string body) =>
        new(HttpMethod.Post, "/api/webhooks/square")
        {
            Content = new StringContent(body, Encoding.UTF8, "application/json"),
            Headers = { { "x-square-hmacsha256-signature", "test-signature" } },
        };

    [Fact]
    public async Task An_invalid_signature_is_rejected_401()
    {
        _factory.Square.WebhookSignatureValid = false;
        try
        {
            var client = _factory.CreateClient();
            var res = await client.SendAsync(WebhookPost("""{"event_id":"e_bad","type":"subscription.updated","data":{"id":"sub_x"}}"""));
            Assert.Equal(HttpStatusCode.Unauthorized, res.StatusCode);
        }
        finally
        {
            _factory.Square.WebhookSignatureValid = true;
        }
    }

    [Fact]
    public async Task A_valid_event_for_an_unknown_subscription_is_accepted_as_a_noop()
    {
        _factory.Square.WebhookSignatureValid = true;
        var client = _factory.CreateClient();

        var res = await client.SendAsync(WebhookPost(
            """{"event_id":"e_unknown","type":"subscription.updated","data":{"id":"sub_not_local"}}"""));

        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
    }

    [Fact]
    public async Task The_same_event_id_is_processed_only_once()
    {
        _factory.Square.WebhookSignatureValid = true;
        var client = _factory.CreateClient();
        const string body = """{"event_id":"e_dup","type":"subscription.updated","data":{"id":"sub_not_local"}}""";

        var first = await client.SendAsync(WebhookPost(body));
        var second = await client.SendAsync(WebhookPost(body));

        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        Assert.Equal(HttpStatusCode.OK, second.StatusCode);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        Assert.Equal(1, db.WebhookEvents.Count(w => w.EventId == "e_dup"));
    }
}
