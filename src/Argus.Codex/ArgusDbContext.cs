using Argus.Codex.Entities;
using Microsoft.EntityFrameworkCore;

namespace Argus.Codex;

public class ArgusDbContext : DbContext
{
    public ArgusDbContext(DbContextOptions<ArgusDbContext> options) : base(options) { }

    public DbSet<Host> Hosts => Set<Host>();

    protected override void OnModelCreating(ModelBuilder b)
    {
        b.Entity<Host>(e =>
        {
            e.HasIndex(h => h.MachineName);
            e.Property(h => h.MachineName).HasMaxLength(256);
            e.Property(h => h.OperatingSystem).HasMaxLength(256);
            e.Property(h => h.AgentVersion).HasMaxLength(64);
            e.Property(h => h.ApiKeyHash).HasMaxLength(128);
            e.HasIndex(h => h.ApiKeyHash);
        });
    }
}
