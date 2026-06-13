using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Argus.Codex;

/// <summary>
/// Enables `dotnet ef migrations add ...` without a running host.
/// Uses the ARGUS_CONNECTION env var if set, otherwise a localhost default.
/// </summary>
public class ArgusDbContextFactory : IDesignTimeDbContextFactory<ArgusDbContext>
{
    public ArgusDbContext CreateDbContext(string[] args)
    {
        var conn = Environment.GetEnvironmentVariable("ARGUS_CONNECTION")
            ?? "Server=localhost,1433;Database=Argus;User Id=sa;Password=Your_password123;TrustServerCertificate=True";

        var options = new DbContextOptionsBuilder<ArgusDbContext>()
            .UseSqlServer(conn, sql => sql.MigrationsAssembly(typeof(ArgusDbContext).Assembly.GetName().Name))
            .Options;

        return new ArgusDbContext(options);
    }
}
