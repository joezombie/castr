using Microsoft.EntityFrameworkCore;
using Castr.Data.Entities;

namespace Castr.Data;

/// <summary>
/// Entity Framework Core DbContext for Castr application.
/// Manages all feeds, episodes, downloaded videos, activity logs, and download queue items.
/// </summary>
public class CastrDbContext : DbContext
{
    public CastrDbContext(DbContextOptions<CastrDbContext> options) 
        : base(options) 
    { 
    }

    public DbSet<Feed> Feeds => Set<Feed>();
    public DbSet<Episode> Episodes => Set<Episode>();
    public DbSet<DownloadedVideo> DownloadedVideos => Set<DownloadedVideo>();
    public DbSet<ActivityLog> ActivityLogs => Set<ActivityLog>();
    public DbSet<DownloadQueueItem> DownloadQueue => Set<DownloadQueueItem>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        
        // Apply all entity configurations from the current assembly
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(CastrDbContext).Assembly);
    }
}
