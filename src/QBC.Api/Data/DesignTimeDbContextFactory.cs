using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace QBC.Api.Data;

/// <summary>
/// Lets EF Core tooling (migrations) construct the context at design time
/// without booting the web host. EF tooling uses this factory INSTEAD of
/// appsettings.json, so this connection string is what `dotnet ef database
/// update` actually connects to — set it to your real target database.
/// </summary>
public sealed class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<AppDbContext>
{
    public AppDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlServer("Server=localhost;Database=qbcdata;Trusted_Connection=True;TrustServerCertificate=True")
            .Options;
        return new AppDbContext(options);
    }
}
