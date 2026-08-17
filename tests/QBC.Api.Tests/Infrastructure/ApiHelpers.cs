using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using QBC.Api.Models;
using QBC.Api.Options;

namespace QBC.Api.Tests.Infrastructure;

/// <summary>Small conveniences shared across the integration tests.</summary>
public static class ApiHelpers
{
    public sealed record Registered(string Token, string UserId, JsonElement User);

    public static async Task<Registered> RegisterAsync(
        this HttpClient client, string email, string password = "password123",
        string first = "Test", string last = "User")
    {
        var res = await client.PostAsJsonAsync("/api/auth/register",
            new { email, password, firstName = first, lastName = last });
        res.EnsureSuccessStatusCode();
        var body = await res.Content.ReadFromJsonAsync<JsonElement>();
        return new Registered(
            body.GetProperty("token").GetString()!,
            body.GetProperty("user").GetProperty("id").GetString()!,
            body.GetProperty("user"));
    }

    public static async Task<string> LoginAsync(
        this HttpClient client, string email, string password)
    {
        var res = await client.PostAsJsonAsync("/api/auth/login", new { email, password });
        res.EnsureSuccessStatusCode();
        var body = await res.Content.ReadFromJsonAsync<JsonElement>();
        return body.GetProperty("token").GetString()!;
    }

    /// <summary>Adds a bearer token to a request message.</summary>
    public static HttpRequestMessage WithBearer(this HttpRequestMessage req, string token)
    {
        req.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
        return req;
    }

    /// <summary>Sets a default bearer token on the client for subsequent requests.</summary>
    public static HttpClient Authorize(this HttpClient client, string token)
    {
        client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
        return client;
    }

    /// <summary>Grants the Admin role to an existing user (mirrors the startup seeder).</summary>
    public static async Task PromoteToAdminAsync(this TestWebAppFactory factory, string email)
    {
        using var scope = factory.Services.CreateScope();
        var roles = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();

        if (!await roles.RoleExistsAsync(AdminOptions.RoleName))
            await roles.CreateAsync(new IdentityRole(AdminOptions.RoleName));

        var user = await users.FindByEmailAsync(email);
        await users.AddToRoleAsync(user!, AdminOptions.RoleName);
    }
}
