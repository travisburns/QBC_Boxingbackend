using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using QBC.Api.Services.Square;
using QBC.Api.Tests.Infrastructure;
using Xunit;

namespace QBC.Api.Tests;

/// <summary>
/// End-to-end membership checkout through the controllers and MembershipService,
/// with Square faked. Verifies the happy path, a decline, duplicate protection,
/// and cancellation — all against the real persistence + status mapping.
/// </summary>
public sealed class CheckoutFlowTests(TestWebAppFactory factory) : IClassFixture<TestWebAppFactory>
{
    private readonly TestWebAppFactory _factory = factory;

    private static object SubRequest(string plan = "membership") =>
        new { planId = plan, sourceId = "cnon:card-nonce-ok", idempotencyKey = Guid.NewGuid().ToString() };

    [Fact]
    public async Task Successful_checkout_creates_an_active_membership_with_display_card_only()
    {
        _factory.Square.FailCreateSubscriptionWith = null;
        var client = _factory.CreateClient();
        var reg = await client.RegisterAsync("buyer.ok@qbc.test");
        client.Authorize(reg.Token);

        var res = await client.PostAsJsonAsync("/api/checkout/subscription", SubRequest("membership"));

        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        var body = await res.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("active", body.GetProperty("status").GetString());
        var m = body.GetProperty("membership");
        Assert.Equal("membership", m.GetProperty("planId").GetString());
        Assert.Equal("Visa", m.GetProperty("cardBrand").GetString());
        Assert.Equal("1111", m.GetProperty("cardLast4").GetString());

        // And the account now reflects the active membership.
        var membership = await client.GetFromJsonAsync<JsonElement>("/api/account/membership");
        Assert.Equal("active", membership.GetProperty("status").GetString());
    }

    [Fact]
    public async Task A_declined_card_returns_422_and_leaves_no_active_membership()
    {
        _factory.Square.FailCreateSubscriptionWith = new SquareApiException("Card declined.");
        try
        {
            var client = _factory.CreateClient();
            var reg = await client.RegisterAsync("buyer.declined@qbc.test");
            client.Authorize(reg.Token);

            var res = await client.PostAsJsonAsync("/api/checkout/subscription", SubRequest("membership"));
            Assert.Equal(HttpStatusCode.UnprocessableEntity, res.StatusCode);

            var membership = await client.GetFromJsonAsync<JsonElement>("/api/account/membership");
            Assert.Equal("none", membership.GetProperty("status").GetString());
        }
        finally
        {
            _factory.Square.FailCreateSubscriptionWith = null;
        }
    }

    [Fact]
    public async Task A_second_checkout_while_active_is_rejected_400()
    {
        _factory.Square.FailCreateSubscriptionWith = null;
        var client = _factory.CreateClient();
        var reg = await client.RegisterAsync("buyer.dupe@qbc.test");
        client.Authorize(reg.Token);

        var first = await client.PostAsJsonAsync("/api/checkout/subscription", SubRequest("membership"));
        Assert.Equal(HttpStatusCode.OK, first.StatusCode);

        var second = await client.PostAsJsonAsync("/api/checkout/subscription", SubRequest("membership"));
        Assert.Equal(HttpStatusCode.BadRequest, second.StatusCode);
    }

    [Fact]
    public async Task Cancel_sets_cancel_at_period_end_and_calls_Square()
    {
        _factory.Square.FailCreateSubscriptionWith = null;
        var before = _factory.Square.CancelCalls;
        var client = _factory.CreateClient();
        var reg = await client.RegisterAsync("buyer.cancel@qbc.test");
        client.Authorize(reg.Token);

        await client.PostAsJsonAsync("/api/checkout/subscription", SubRequest("membership"));
        var res = await client.PostAsync("/api/account/membership/cancel", new StringContent(string.Empty));

        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        var body = await res.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(body.GetProperty("cancelAtPeriodEnd").GetBoolean());
        Assert.True(_factory.Square.CancelCalls > before);
    }
}
