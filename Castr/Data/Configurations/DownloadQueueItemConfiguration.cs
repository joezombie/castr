using Castr.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Castr.Data.Configurations;

/// <summary>
/// Entity Framework Core configuration for DownloadQueueItem entity.
/// </summary>
public class DownloadQueueItemConfiguration : IEntityTypeConfiguration<DownloadQueueItem>
{
    public void Configure(EntityTypeBuilder<DownloadQueueItem> builder)
    {
        builder.ToTable("download_queue");
        
        // Primary key
        builder.HasKey(dq => dq.Id);
        
        // Indexes
        builder.HasIndex(dq => new { dq.FeedId, dq.VideoId }).IsUnique();
        builder.HasIndex(dq => new { dq.FeedId, dq.Status });
        builder.HasIndex(dq => dq.Status);
        
        // Relationship configured in FeedConfiguration
    }
}
