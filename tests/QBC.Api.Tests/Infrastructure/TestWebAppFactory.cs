using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.AspNetCore.Mvc.Testing;
using QBC.Api.Data;
using QBC.Api.Services.Square;

namespace QBC.Api.Tests.Infrastructure;

/// <summary>
/// Boots the real API in-process for integration tests, but swaps two things:
///  - SQL Server → an isolated EF Core InMemory store (no database required),
///  - the real Square gateway → <see cref="FakeSquareGateway"/> (no external HTTP).
/// Everything else — Identity, JWT issuing/validation, authorization, controllers,
/// the admin-seeding startup step — runs exactly as in production.
/// </summary>
public sealed class TestWebAppFactory : WebApplicationFactory<Program>
{
    // Unique per factory instance so test classes don't share state.
    private readonly string _dbName = "qbc-tests-" + Guid.NewGuid();

    /// <summary>An email pre-listed in Admin:Emails, so registering it grants Admin.</summary>
    public const string ConfiguredAdminEmail = "configured.owner@qbc.test";

    /// <summary>The fake Square gateway wired into the app; configure it per test.</summary>
    public FakeSquareGateway Square { get; } = new();

    public TestWebAppFactory()
    {
        // Program.cs eagerly validates Jwt:Key (>= 32 chars) at startup. Setting it
        // as an environment variable guarantees it's present regardless of the order
        // configuration sources are applied.
        Environment.SetEnvironmentVariable(
            "Jwt__Key", "qbc-integration-test-signing-key-0123456789-abcdef-ghij");

        // Give each plan a non-empty Square plan-variation id so the checkout path
        // is reachable (the service rejects plans with no configured variation).
        Environment.SetEnvironmentVariable("Square__PlanVariationIds__strength", "var_strength_test");
        Environment.SetEnvironmentVariable("Square__PlanVariationIds__boxing", "var_boxing_test");
        Environment.SetEnvironmentVariable("Square__PlanVariationIds__unlimited", "var_unlimited_test");

        // A known configured-owner email so promote-on-registration can be tested.
        Environment.SetEnvironmentVariable("Admin__Emails__0", ConfiguredAdminEmail);
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");

        builder.ConfigureServices(services =>
        {
            // Replace the SQL Server DbContext with an in-memory one.
            var options = services.SingleOrDefault(
                d => d.ServiceType == typeof(DbContextOptions<AppDbContext>));
            if (options is not null) services.Remove(options);

            services.AddDbContext<AppDbContext>(o => o.UseInMemoryDatabase(_dbName));

            // Replace the real Square gateway (registered via AddHttpClient) with the fake.
            var gateway = services.SingleOrDefault(d => d.ServiceType == typeof(ISquareGateway));
            if (gateway is not null) services.Remove(gateway);

            services.AddSingleton<ISquareGateway>(Square);
        });
    }
}
