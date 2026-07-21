using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace QBC.Api.Data;

/// <summary>
/// Lets EF Core tooling (migrations) construct the context at design time
/// without booting the web host or needing a live database connection.
/// The connection string here is only used to resolve the SQL Server provider's
/// syntax when scaffolding migrations — it is never connected to.
/// </summary>
public sealed class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<AppDbContext>
{
    public AppDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlServer("Server=localhost;Database=ApexAthletic;Trusted_Connection=True;TrustServerCertificate=True")
            .Options;
        return new AppDbContext(options);
    }
}
