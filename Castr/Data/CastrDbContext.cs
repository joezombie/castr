using Castr.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace Castr.Data;

/// <summary>
/// Entity Framework Core DbContext for Castr application.
/// Manages all entities and applies configurations.
/// </summary>
public class CastrDbContext : DbContext
{
    public CastrDbContext(DbContextOptions<CastrDbContext> options) : base(options)
    {
    }

    // DbSets for all entities
    public DbSet<Feed> Feeds => Set<Feed>();
    public DbSet<Episode> Episodes => Set<Episode>();
    public DbSet<DownloadedVideo> DownloadedVideos => Set<DownloadedVideo>();
    public DbSet<ActivityLog> ActivityLogs => Set<ActivityLog>();
    public DbSet<DownloadQueueItem> DownloadQueue => Set<DownloadQueueItem>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Apply all configurations from the assembly
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(CastrDbContext).Assembly);
    }
}
