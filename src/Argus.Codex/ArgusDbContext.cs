using Argus.Codex.Entities;
using Microsoft.EntityFrameworkCore;

namespace Argus.Codex;

public class ArgusDbContext : DbContext
{
    public ArgusDbContext(DbContextOptions<ArgusDbContext> options) : base(options) { }

    public DbSet<Host> Hosts => Set<Host>();
    public DbSet<MetricSample> MetricSamples => Set<MetricSample>();
    public DbSet<DiskSample> DiskSamples => Set<DiskSample>();
    public DbSet<ProcessSample> ProcessSamples => Set<ProcessSample>();
    public DbSet<EventLogEntry> EventLogEntries => Set<EventLogEntry>();
    public DbSet<LogEntry> LogEntries => Set<LogEntry>();

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

        b.Entity<MetricSample>(e =>
        {
            e.HasIndex(m => new { m.HostId, m.TimestampUtc });
            e.HasOne(m => m.Host).WithMany(h => h.MetricSamples)
                .HasForeignKey(m => m.HostId).OnDelete(DeleteBehavior.Cascade);
        });

        b.Entity<DiskSample>(e =>
        {
            e.HasIndex(d => new { d.HostId, d.TimestampUtc });
            e.Property(d => d.Mount).HasMaxLength(512);
            e.HasOne(d => d.Host).WithMany(h => h.DiskSamples)
                .HasForeignKey(d => d.HostId).OnDelete(DeleteBehavior.Cascade);
        });

        b.Entity<ProcessSample>(e =>
        {
            e.HasIndex(p => new { p.HostId, p.TimestampUtc });
            e.Property(p => p.Name).HasMaxLength(512);
            e.HasOne(p => p.Host).WithMany(h => h.ProcessSamples)
                .HasForeignKey(p => p.HostId).OnDelete(DeleteBehavior.Cascade);
        });

        b.Entity<EventLogEntry>(e =>
        {
            e.HasIndex(x => new { x.HostId, x.TimestampUtc });
            e.HasIndex(x => x.Level);
            e.Property(x => x.Channel).HasMaxLength(512);
            e.Property(x => x.Source).HasMaxLength(512);
            e.Property(x => x.Level).HasMaxLength(32);
            e.HasOne(x => x.Host).WithMany(h => h.EventLogEntries)
                .HasForeignKey(x => x.HostId).OnDelete(DeleteBehavior.Cascade);
        });

        b.Entity<LogEntry>(e =>
        {
            e.HasIndex(l => new { l.HostId, l.TimestampUtc });
            e.HasIndex(l => new { l.FilePath, l.TimestampUtc });
            e.Property(l => l.FilePath).HasMaxLength(1024);
            e.Property(l => l.Level).HasMaxLength(32);
            e.HasOne(l => l.Host).WithMany(h => h.LogEntries)
                .HasForeignKey(l => l.HostId).OnDelete(DeleteBehavior.Cascade);
        });
    }
}
