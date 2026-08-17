using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using QBC.Api.Tests.Infrastructure;
using Xunit;

namespace QBC.Api.Tests;

/// <summary>
/// The account portal is auth-gated, and a brand-new (free, plan-less) account
/// is a valid state: its membership reads as "none", with no Square involvement.
/// </summary>
public sealed class AccountMembershipTests(TestWebAppFactory factory)
    : IClassFixture<TestWebAppFactory>
{
    private readonly TestWebAppFactory _factory = factory;

    [Fact]
    public async Task Membership_requires_authentication()
    {
        var client = _factory.CreateClient();
        var res = await client.GetAsync("/api/account/membership");
        Assert.Equal(HttpStatusCode.Unauthorized, res.StatusCode);
    }

    [Fact]
    public async Task A_new_free_account_has_membership_status_none()
    {
        var client = _factory.CreateClient();
        var reg = await client.RegisterAsync("free.account@qbc.test");

        var req = new HttpRequestMessage(HttpMethod.Get, "/api/account/membership").WithBearer(reg.Token);
        var res = await client.SendAsync(req);

        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        var body = await res.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("none", body.GetProperty("status").GetString());
        Assert.Equal(JsonValueKind.Null, body.GetProperty("planId").ValueKind);
        Assert.False(body.GetProperty("cancelAtPeriodEnd").GetBoolean());

        // A free account was never handed to Square during signup.
        Assert.Equal(0, _factory.Square.EnsureCustomerCalls);
    }

    [Fact]
    public async Task Cancel_with_no_membership_is_a_clean_400()
    {
        var client = _factory.CreateClient();
        var reg = await client.RegisterAsync("nothing.to.cancel@qbc.test");

        var req = new HttpRequestMessage(HttpMethod.Post, "/api/account/membership/cancel")
            .WithBearer(reg.Token);
        var res = await client.SendAsync(req);

        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
    }

    [Fact]
    public async Task Plans_are_public()
    {
        var client = _factory.CreateClient();
        var res = await client.GetAsync("/api/plans");
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
    }
}
