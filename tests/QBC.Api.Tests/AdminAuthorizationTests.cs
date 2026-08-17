using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using QBC.Api.Tests.Infrastructure;
using Xunit;

namespace QBC.Api.Tests;

/// <summary>
/// The owner CRM is gated with [Authorize(Roles = "Admin")]. Verify the three
/// outcomes: anonymous → 401, a normal member → 403, an admin → 200.
/// </summary>
public sealed class AdminAuthorizationTests(TestWebAppFactory factory)
    : IClassFixture<TestWebAppFactory>
{
    private readonly TestWebAppFactory _factory = factory;

    [Fact]
    public async Task Anonymous_request_to_admin_is_rejected_401()
    {
        var client = _factory.CreateClient();
        var res = await client.GetAsync("/api/admin/customers");
        Assert.Equal(HttpStatusCode.Unauthorized, res.StatusCode);
    }

    [Fact]
    public async Task Member_without_the_admin_role_is_forbidden_403()
    {
        var client = _factory.CreateClient();
        var reg = await client.RegisterAsync("plain.member@qbc.test");

        var req = new HttpRequestMessage(HttpMethod.Get, "/api/admin/customers").WithBearer(reg.Token);
        var res = await client.SendAsync(req);

        Assert.Equal(HttpStatusCode.Forbidden, res.StatusCode);
    }

    [Fact]
    public async Task Admin_can_list_customers_200()
    {
        var client = _factory.CreateClient();
        await client.RegisterAsync("owner@qbc.test", first: "Olivia", last: "Owner");
        await _factory.PromoteToAdminAsync("owner@qbc.test");

        // Re-login so the freshly granted role is baked into the token claims.
        var token = await client.LoginAsync("owner@qbc.test", "password123");

        var req = new HttpRequestMessage(HttpMethod.Get, "/api/admin/customers").WithBearer(token);
        var res = await client.SendAsync(req);

        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        var body = await res.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(body.GetProperty("totalCustomers").GetInt32() >= 1);
        Assert.True(body.TryGetProperty("customers", out var customers));
        Assert.Contains(customers.EnumerateArray(),
            c => c.GetProperty("email").GetString() == "owner@qbc.test");
    }

    [Fact]
    public async Task A_configured_owner_email_is_granted_Admin_at_registration_without_a_restart()
    {
        var client = _factory.CreateClient();
        var reg = await client.RegisterAsync(TestWebAppFactory.ConfiguredAdminEmail);

        // The role is present in the register response immediately.
        Assert.Contains(reg.User.GetProperty("roles").EnumerateArray(),
            r => r.GetString() == "Admin");

        // And the token can reach the CRM straight away.
        var req = new HttpRequestMessage(HttpMethod.Get, "/api/admin/customers").WithBearer(reg.Token);
        var res = await client.SendAsync(req);
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
    }

    [Fact]
    public async Task Admin_gets_404_for_an_unknown_customer_id()
    {
        var client = _factory.CreateClient();
        await client.RegisterAsync("owner2@qbc.test");
        await _factory.PromoteToAdminAsync("owner2@qbc.test");
        var token = await client.LoginAsync("owner2@qbc.test", "password123");

        var req = new HttpRequestMessage(HttpMethod.Get, "/api/admin/customers/does-not-exist")
            .WithBearer(token);
        var res = await client.SendAsync(req);

        Assert.Equal(HttpStatusCode.NotFound, res.StatusCode);
    }
}
