using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using QBC.Api.Tests.Infrastructure;
using Xunit;

namespace QBC.Api.Tests;

/// <summary>Register / login / me — the core account flow and its guardrails.</summary>
public sealed class AuthEndpointsTests(TestWebAppFactory factory) : IClassFixture<TestWebAppFactory>
{
    private readonly TestWebAppFactory _factory = factory;

    [Fact]
    public async Task Register_creates_a_plan_less_account_and_returns_a_token_with_no_roles()
    {
        var client = _factory.CreateClient();
        var res = await client.PostAsJsonAsync("/api/auth/register",
            new { email = "new.member@qbc.test", password = "password123", firstName = "New", lastName = "Member" });

        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        var body = await res.Content.ReadFromJsonAsync<JsonElement>();
        Assert.False(string.IsNullOrWhiteSpace(body.GetProperty("token").GetString()));
        var user = body.GetProperty("user");
        Assert.Equal("new.member@qbc.test", user.GetProperty("email").GetString());
        Assert.Empty(user.GetProperty("roles").EnumerateArray()); // free account: no roles
    }

    [Fact]
    public async Task Register_rejects_a_duplicate_email_with_409()
    {
        var client = _factory.CreateClient();
        await client.RegisterAsync("dupe@qbc.test");

        var res = await client.PostAsJsonAsync("/api/auth/register",
            new { email = "dupe@qbc.test", password = "password123", firstName = "A", lastName = "B" });

        Assert.Equal(HttpStatusCode.Conflict, res.StatusCode);
    }

    [Fact]
    public async Task Register_rejects_a_weak_password_with_400()
    {
        var client = _factory.CreateClient();
        var res = await client.PostAsJsonAsync("/api/auth/register",
            new { email = "weak@qbc.test", password = "short", firstName = "A", lastName = "B" });

        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
    }

    [Fact]
    public async Task Login_with_correct_credentials_returns_a_token()
    {
        var client = _factory.CreateClient();
        await client.RegisterAsync("login.ok@qbc.test", "password123");

        var res = await client.PostAsJsonAsync("/api/auth/login",
            new { email = "login.ok@qbc.test", password = "password123" });

        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        var body = await res.Content.ReadFromJsonAsync<JsonElement>();
        Assert.False(string.IsNullOrWhiteSpace(body.GetProperty("token").GetString()));
    }

    [Fact]
    public async Task Login_with_wrong_password_returns_401_and_a_generic_message()
    {
        var client = _factory.CreateClient();
        await client.RegisterAsync("login.bad@qbc.test", "password123");

        var res = await client.PostAsJsonAsync("/api/auth/login",
            new { email = "login.bad@qbc.test", password = "wrongpassword" });

        Assert.Equal(HttpStatusCode.Unauthorized, res.StatusCode);
        var body = await res.Content.ReadFromJsonAsync<JsonElement>();
        // Same message whether the email exists or not — no user enumeration.
        Assert.Equal("Invalid email or password.", body.GetProperty("message").GetString());
    }

    [Fact]
    public async Task Login_for_an_unknown_email_returns_the_same_generic_401()
    {
        var client = _factory.CreateClient();
        var res = await client.PostAsJsonAsync("/api/auth/login",
            new { email = "nobody@qbc.test", password = "password123" });

        Assert.Equal(HttpStatusCode.Unauthorized, res.StatusCode);
        var body = await res.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("Invalid email or password.", body.GetProperty("message").GetString());
    }

    [Fact]
    public async Task Repeated_failed_logins_lock_the_account()
    {
        var client = _factory.CreateClient();
        await client.RegisterAsync("lock.me@qbc.test", "password123");

        // Five failed attempts trips the lockout (MaxFailedAccessAttempts = 5).
        for (var i = 0; i < 5; i++)
        {
            await client.PostAsJsonAsync("/api/auth/login",
                new { email = "lock.me@qbc.test", password = "wrongpassword" });
        }

        // Even the correct password is now refused, with the lockout message.
        var res = await client.PostAsJsonAsync("/api/auth/login",
            new { email = "lock.me@qbc.test", password = "password123" });

        Assert.Equal(HttpStatusCode.Unauthorized, res.StatusCode);
        var body = await res.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Contains("locked", body.GetProperty("message").GetString()!, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Me_requires_authentication()
    {
        var client = _factory.CreateClient();
        var res = await client.GetAsync("/api/auth/me");
        Assert.Equal(HttpStatusCode.Unauthorized, res.StatusCode);
    }

    [Fact]
    public async Task Me_returns_the_current_user_when_authenticated()
    {
        var client = _factory.CreateClient();
        var reg = await client.RegisterAsync("me@qbc.test", first: "Mia", last: "Member");

        var req = new HttpRequestMessage(HttpMethod.Get, "/api/auth/me").WithBearer(reg.Token);
        var res = await client.SendAsync(req);

        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        var body = await res.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("me@qbc.test", body.GetProperty("email").GetString());
        Assert.Equal("Mia", body.GetProperty("firstName").GetString());
    }

    [Fact]
    public async Task Health_endpoint_is_open()
    {
        var client = _factory.CreateClient();
        var res = await client.GetAsync("/health");
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
    }
}
