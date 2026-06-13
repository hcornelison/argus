using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Argus.Codex;

public static class CodexServiceCollectionExtensions
{
    /// <summary>
    /// Registers the ArgusDbContext (SQL Server), retention options, and RetentionService.
    /// Connection string is read from the "Argus" connection string.
    /// </summary>
    public static IServiceCollection AddCodex(this IServiceCollection services, IConfiguration config)
    {
        var conn = config.GetConnectionString("Argus")
            ?? throw new InvalidOperationException("Missing connection string 'Argus'.");

        services.AddDbContext<ArgusDbContext>(opt =>
            opt.UseSqlServer(conn, sql =>
                sql.MigrationsAssembly(typeof(ArgusDbContext).Assembly.GetName().Name)));

        services.Configure<RetentionOptions>(config.GetSection(RetentionOptions.SectionName));
        services.AddScoped<RetentionService>();

        return services;
    }
}
