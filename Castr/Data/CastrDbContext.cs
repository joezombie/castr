using Castr.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace Castr.Data;

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
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(CastrDbContext).Assembly);
    }
}
