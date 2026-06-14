using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Argus.Codex;

/// <summary>
/// Enables `dotnet ef migrations add ...` without a running host.
/// Uses the ARGUS_CONNECTION env var if set, otherwise a local SQLite file.
/// </summary>
public class ArgusDbContextFactory : IDesignTimeDbContextFactory<ArgusDbContext>
{
    public ArgusDbContext CreateDbContext(string[] args)
    {
        var conn = Environment.GetEnvironmentVariable("ARGUS_CONNECTION")
            ?? "Data Source=argus.db";

        var options = new DbContextOptionsBuilder<ArgusDbContext>()
            .UseSqlite(conn, sql => sql.MigrationsAssembly(typeof(ArgusDbContext).Assembly.GetName().Name))
            .Options;

        return new ArgusDbContext(options);
    }
}
