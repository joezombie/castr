using Castr.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Castr.Data.Configurations;

/// <summary>
/// Entity Framework Core configuration for Feed entity.
/// </summary>
public class FeedConfiguration : IEntityTypeConfiguration<Feed>
{
    public void Configure(EntityTypeBuilder<Feed> builder)
    {
        builder.ToTable("feeds");
        
        // Primary key
        builder.HasKey(f => f.Id);
        
        // Indexes
        builder.HasIndex(f => f.Name).IsUnique();
        builder.HasIndex(f => f.IsActive);
        
        // Relationships
        builder.HasMany(f => f.Episodes)
            .WithOne(e => e.Feed)
            .HasForeignKey(e => e.FeedId)
            .OnDelete(DeleteBehavior.Cascade);
            
        builder.HasMany(f => f.DownloadedVideos)
            .WithOne(dv => dv.Feed)
            .HasForeignKey(dv => dv.FeedId)
            .OnDelete(DeleteBehavior.Cascade);
            
        builder.HasMany(f => f.ActivityLogs)
            .WithOne(al => al.Feed)
            .HasForeignKey(al => al.FeedId)
            .OnDelete(DeleteBehavior.Cascade);
            
        builder.HasMany(f => f.DownloadQueue)
            .WithOne(dq => dq.Feed)
            .HasForeignKey(dq => dq.FeedId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
