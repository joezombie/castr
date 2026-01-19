using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Castr.Data.Entities;

namespace Castr.Data.Configurations;

/// <summary>
/// Entity configuration for DownloadQueueItem entity.
/// Defines relationships, indexes, and constraints.
/// </summary>
public class DownloadQueueItemConfiguration : IEntityTypeConfiguration<DownloadQueueItem>
{
    public void Configure(EntityTypeBuilder<DownloadQueueItem> builder)
    {
        builder.ToTable("download_queue");
        
        builder.HasKey(dq => dq.Id);
        
        builder.Property(dq => dq.Id)
            .HasColumnName("id");
        
        builder.Property(dq => dq.FeedId)
            .HasColumnName("feed_id")
            .IsRequired();
        
        builder.Property(dq => dq.VideoId)
            .HasColumnName("video_id")
            .IsRequired()
            .HasMaxLength(100);
        
        builder.Property(dq => dq.VideoTitle)
            .HasColumnName("video_title")
            .HasMaxLength(500);
        
        builder.Property(dq => dq.Status)
            .HasColumnName("status")
            .IsRequired()
            .HasMaxLength(50);
        
        builder.Property(dq => dq.ProgressPercent)
            .HasColumnName("progress_percent")
            .HasDefaultValue(0);
        
        builder.Property(dq => dq.ErrorMessage)
            .HasColumnName("error_message");
        
        builder.Property(dq => dq.QueuedAt)
            .HasColumnName("queued_at")
            .IsRequired();
        
        builder.Property(dq => dq.StartedAt)
            .HasColumnName("started_at");
        
        builder.Property(dq => dq.CompletedAt)
            .HasColumnName("completed_at");
        
        // Indexes for queue queries
        builder.HasIndex(dq => new { dq.FeedId, dq.Status })
            .HasDatabaseName("idx_queue_feed_status");
        
        builder.HasIndex(dq => dq.QueuedAt)
            .HasDatabaseName("idx_queue_queued_at");
        
        // Relationship is configured in FeedConfiguration
    }
}
