using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using QBC.Api.Services.Square;
using QBC.Api.Tests.Infrastructure;
using Xunit;

namespace QBC.Api.Tests;

/// <summary>
/// End-to-end one-time day-pass checkout through the controllers and
/// DayPassService, with Square faked. Covers the happy path, the reservation
/// date window, a decline, the save-card / pay-with-saved-card flow, and the
/// owner's front-desk check-in + redeem.
/// </summary>
public sealed class DayPassFlowTests(TestWebAppFactory factory) : IClassFixture<TestWebAppFactory>
{
    private readonly TestWebAppFactory _factory = factory;

    private static string InDays(int days) =>
        DateOnly.FromDateTime(DateTime.UtcNow).AddDays(days).ToString("yyyy-MM-dd");

    private static object PassRequest(string? date = null, bool saveCard = false) =>
        new
        {
            productId = "day-pass",
            visitDate = date ?? InDays(1),
            sourceId = "cnon:card-nonce-ok",
            idempotencyKey = Guid.NewGuid().ToString(),
            saveCard,
        };

    [Fact]
    public async Task Successful_day_pass_charges_once_and_records_the_reserved_day()
    {
        _factory.Square.FailCreatePaymentWith = null;
        var before = _factory.Square.CreatePaymentCalls;
        var client = _factory.CreateClient();
        client.Authorize((await client.RegisterAsync("pass.ok@qbc.test")).Token);

        var date = InDays(2);
        var res = await client.PostAsJsonAsync("/api/checkout/day-pass", PassRequest(date));

        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        var pass = await res.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("day-pass", pass.GetProperty("productId").GetString());
        Assert.Equal(date, pass.GetProperty("visitDate").GetString());
        Assert.Equal("paid", pass.GetProperty("status").GetString());
        Assert.Equal("Visa", pass.GetProperty("cardBrand").GetString());
        Assert.True(_factory.Square.CreatePaymentCalls > before);

        // And it shows up in the buyer's own list.
        var mine = await client.GetFromJsonAsync<JsonElement>("/api/account/day-passes");
        Assert.True(mine.GetArrayLength() >= 1);
    }

    [Fact]
    public async Task A_date_beyond_the_booking_window_is_rejected_400()
    {
        var client = _factory.CreateClient();
        client.Authorize((await client.RegisterAsync("pass.toofar@qbc.test")).Token);

        var res = await client.PostAsJsonAsync("/api/checkout/day-pass", PassRequest(InDays(30)));
        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
    }

    [Fact]
    public async Task A_date_in_the_past_is_rejected_400()
    {
        var client = _factory.CreateClient();
        client.Authorize((await client.RegisterAsync("pass.past@qbc.test")).Token);

        var res = await client.PostAsJsonAsync("/api/checkout/day-pass", PassRequest(InDays(-1)));
        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
    }

    [Fact]
    public async Task A_declined_card_returns_422()
    {
        _factory.Square.FailCreatePaymentWith = new SquareApiException("Card declined.");
        try
        {
            var client = _factory.CreateClient();
            client.Authorize((await client.RegisterAsync("pass.declined@qbc.test")).Token);

            var res = await client.PostAsJsonAsync("/api/checkout/day-pass", PassRequest());
            Assert.Equal(HttpStatusCode.UnprocessableEntity, res.StatusCode);
        }
        finally
        {
            _factory.Square.FailCreatePaymentWith = null;
        }
    }

    [Fact]
    public async Task Saving_a_card_lets_a_repeat_purchase_pay_with_the_saved_card()
    {
        _factory.Square.FailCreatePaymentWith = null;
        var client = _factory.CreateClient();
        client.Authorize((await client.RegisterAsync("pass.saver@qbc.test")).Token);

        // First purchase saves the card on file.
        var first = await client.PostAsJsonAsync("/api/checkout/day-pass", PassRequest(saveCard: true));
        Assert.Equal(HttpStatusCode.OK, first.StatusCode);

        var saved = await client.GetFromJsonAsync<JsonElement>("/api/account/saved-card");
        Assert.True(saved.GetProperty("hasCard").GetBoolean());
        Assert.Equal("1111", saved.GetProperty("cardLast4").GetString());

        // Second purchase uses the saved card — no new token needed.
        var repeat = await client.PostAsJsonAsync("/api/checkout/day-pass", new
        {
            productId = "day-pass",
            visitDate = InDays(3),
            idempotencyKey = Guid.NewGuid().ToString(),
            useSavedCard = true,
        });
        Assert.Equal(HttpStatusCode.OK, repeat.StatusCode);
    }

    [Fact]
    public async Task Owner_can_see_and_redeem_a_day_pass_for_a_date()
    {
        _factory.Square.FailCreatePaymentWith = null;
        var client = _factory.CreateClient();
        client.Authorize((await client.RegisterAsync("pass.checkin@qbc.test")).Token);

        var date = InDays(1);
        var buy = await client.PostAsJsonAsync("/api/checkout/day-pass", PassRequest(date));
        var pass = await buy.Content.ReadFromJsonAsync<JsonElement>();
        var passId = pass.GetProperty("id").GetInt32();

        // Owner registers, is granted Admin, then logs in fresh so the token
        // carries the Admin role claim.
        var admin = _factory.CreateClient();
        await admin.RegisterAsync("owner.desk@qbc.test");
        await _factory.PromoteToAdminAsync("owner.desk@qbc.test");
        admin.Authorize(await admin.LoginAsync("owner.desk@qbc.test", "password123"));

        var list = await admin.GetFromJsonAsync<JsonElement>($"/api/admin/day-passes?date={date}");
        Assert.True(list.GetProperty("total").GetInt32() >= 1);

        var redeem = await admin.PostAsync($"/api/admin/day-passes/{passId}/redeem",
            new StringContent(string.Empty));
        Assert.Equal(HttpStatusCode.OK, redeem.StatusCode);
        var redeemed = await redeem.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("redeemed", redeemed.GetProperty("status").GetString());
    }
}
