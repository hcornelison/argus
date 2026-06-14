using Argus.Codex.Redis;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using StackExchange.Redis;

namespace Argus.Codex;

public static class CodexServiceCollectionExtensions
{
    /// <summary>
    /// Registers ArgusDbContext (SQLite, host registry only), Redis stream service,
    /// and retention options.
    /// </summary>
    public static IServiceCollection AddCodex(this IServiceCollection services, IConfiguration config)
    {
        var conn = config.GetConnectionString("Argus")
            ?? throw new InvalidOperationException("Missing connection string 'Argus'.");

        services.AddDbContext<ArgusDbContext>(opt =>
            opt.UseSqlite(conn, sql =>
                sql.MigrationsAssembly(typeof(ArgusDbContext).Assembly.GetName().Name)));

        var redisConn = config["Redis:ConnectionString"]
            ?? throw new InvalidOperationException("Missing config 'Redis:ConnectionString'.");

        services.AddSingleton<IConnectionMultiplexer>(_ => ConnectionMultiplexer.Connect(redisConn));
        services.AddSingleton<RedisStreamService>();

        services.Configure<RetentionOptions>(config.GetSection(RetentionOptions.SectionName));

        return services;
    }
}
