using System.Net;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using QBC.Api.Models;
using QBC.Api.Options;
using QBC.Api.Services.Square;
using QBC.Api.Tests.Infrastructure;
using Xunit;

namespace QBC.Api.Tests;

/// <summary>
/// Unit tests for the Square REST gateway: correct request shaping, response
/// parsing, error surfacing, and webhook signature verification — all against a
/// stubbed HTTP transport (no network).
/// </summary>
public sealed class SquareGatewayTests
{
    private static SquareGateway BuildGateway(QueuedHttpMessageHandler handler, SquareOptions? opt = null)
    {
        var http = new HttpClient(handler) { BaseAddress = new Uri("https://connect.squareupsandbox.com") };
        return new SquareGateway(http, Options.Create(opt ?? new SquareOptions { LocationId = "L1" }),
            NullLogger<SquareGateway>.Instance);
    }

    private static ApplicationUser User() =>
        new() { Id = "u1", Email = "buyer@qbc.test", FirstName = "Buy", LastName = "Er" };

    [Fact]
    public async Task EnsureCustomer_returns_the_existing_id_without_calling_Square()
    {
        var handler = new QueuedHttpMessageHandler();
        var gw = BuildGateway(handler);

        var id = await gw.EnsureCustomerAsync(User(), "cust_existing", default);

        Assert.Equal("cust_existing", id);
        Assert.Empty(handler.Requests); // short-circuits — no HTTP
    }

    [Fact]
    public async Task EnsureCustomer_creates_a_customer_and_parses_the_id()
    {
        var handler = new QueuedHttpMessageHandler()
            .Enqueue(HttpStatusCode.OK, """{"customer":{"id":"cust_new"}}""");
        var gw = BuildGateway(handler);

        var id = await gw.EnsureCustomerAsync(User(), null, default);

        Assert.Equal("cust_new", id);
        var req = Assert.Single(handler.Requests);
        Assert.Equal(HttpMethod.Post, req.Method);
        Assert.Contains("/v2/customers", req.Uri);
        Assert.Contains("buyer@qbc.test", req.Body);        // email_address in payload
        Assert.Contains("\"reference_id\":\"u1\"", req.Body); // ties Square customer to our user
    }

    [Fact]
    public async Task CreateCardOnFile_parses_brand_and_last4()
    {
        var handler = new QueuedHttpMessageHandler().Enqueue(HttpStatusCode.OK,
            """{"card":{"id":"card_1","card_brand":"VISA","last_4":"1111"}}""");
        var gw = BuildGateway(handler);

        var card = await gw.CreateCardOnFileAsync("cust_1", "cnon:src", "idem-1", default);

        Assert.Equal("card_1", card.Id);
        Assert.Equal("VISA", card.Brand);
        Assert.Equal("1111", card.Last4);
        Assert.Contains("/v2/cards", handler.Requests[0].Uri);
        Assert.Contains("\"idempotency_key\":\"idem-1\"", handler.Requests[0].Body);
    }

    [Fact]
    public async Task CreateSubscription_sends_plan_and_parses_status()
    {
        var handler = new QueuedHttpMessageHandler().Enqueue(HttpStatusCode.OK,
            """{"subscription":{"id":"sub_1","status":"ACTIVE","card_id":"card_1","charged_through_date":"2026-09-17"}}""");
        var gw = BuildGateway(handler);

        var sub = await gw.CreateSubscriptionAsync("cust_1", "var_boxing", "card_1", "idem-2", default);

        Assert.Equal("sub_1", sub.Id);
        Assert.Equal("ACTIVE", sub.Status);
        Assert.Equal("card_1", sub.CardId);
        Assert.Equal(new DateTime(2026, 9, 17, 0, 0, 0, DateTimeKind.Utc), sub.ChargedThroughUtc);
        var body = handler.Requests[0].Body;
        Assert.Contains("\"plan_variation_id\":\"var_boxing\"", body);
        Assert.Contains("\"location_id\":\"L1\"", body);
    }

    [Fact]
    public async Task A_Square_error_response_surfaces_its_detail_as_SquareApiException()
    {
        var handler = new QueuedHttpMessageHandler().Enqueue(HttpStatusCode.BadRequest,
            """{"errors":[{"category":"PAYMENT_METHOD_ERROR","code":"CARD_DECLINED","detail":"Card declined."}]}""");
        var gw = BuildGateway(handler);

        var ex = await Assert.ThrowsAsync<SquareApiException>(
            () => gw.CreateSubscriptionAsync("cust_1", "var_boxing", "card_1", "idem", default));
        Assert.Equal("Card declined.", ex.Message);
    }

    // ---- webhook signature verification (pure crypto, deterministic) ----

    private static string Sign(string key, string url, string body)
    {
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(key));
        return Convert.ToBase64String(hmac.ComputeHash(Encoding.UTF8.GetBytes(url + body)));
    }

    [Fact]
    public void VerifyWebhookSignature_accepts_a_correct_signature()
    {
        var opt = new SquareOptions
        {
            WebhookSignatureKey = "whsk",
            WebhookNotificationUrl = "https://api.qbc/webhooks/square",
        };
        var gw = BuildGateway(new QueuedHttpMessageHandler(), opt);
        var body = """{"event_id":"e1"}""";

        Assert.True(gw.VerifyWebhookSignature(Sign("whsk", opt.WebhookNotificationUrl, body), body));
    }

    [Fact]
    public void VerifyWebhookSignature_rejects_a_tampered_body()
    {
        var opt = new SquareOptions
        {
            WebhookSignatureKey = "whsk",
            WebhookNotificationUrl = "https://api.qbc/webhooks/square",
        };
        var gw = BuildGateway(new QueuedHttpMessageHandler(), opt);
        var sig = Sign("whsk", opt.WebhookNotificationUrl, """{"event_id":"e1"}""");

        Assert.False(gw.VerifyWebhookSignature(sig, """{"event_id":"e1","tampered":true}"""));
    }

    [Fact]
    public void VerifyWebhookSignature_rejects_garbage_and_missing_config()
    {
        var configured = new SquareOptions
        {
            WebhookSignatureKey = "whsk",
            WebhookNotificationUrl = "https://api.qbc/webhooks/square",
        };
        var gw = BuildGateway(new QueuedHttpMessageHandler(), configured);
        Assert.False(gw.VerifyWebhookSignature("not-valid-base64!!!", "{}"));

        // With no signature key configured, verification always fails closed.
        var unconfigured = BuildGateway(new QueuedHttpMessageHandler(), new SquareOptions());
        Assert.False(unconfigured.VerifyWebhookSignature("anything", "{}"));
    }
}
