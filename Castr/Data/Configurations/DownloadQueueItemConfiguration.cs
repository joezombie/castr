using Castr.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Castr.Data.Configurations;

public class DownloadQueueItemConfiguration : IEntityTypeConfiguration<DownloadQueueItem>
{
    public void Configure(EntityTypeBuilder<DownloadQueueItem> builder)
    {
        builder.ToTable("download_queue");

        builder.HasKey(q => q.Id);
        builder.Property(q => q.Id).HasColumnName("id");

        builder.Property(q => q.FeedId).HasColumnName("feed_id").IsRequired();
        builder.Property(q => q.VideoId).HasColumnName("video_id").IsRequired();
        builder.Property(q => q.VideoTitle).HasColumnName("video_title");
        builder.Property(q => q.Status).HasColumnName("status").IsRequired().HasDefaultValue("queued");
        builder.Property(q => q.ProgressPercent).HasColumnName("progress_percent").HasDefaultValue(0);
        builder.Property(q => q.ErrorMessage).HasColumnName("error_message");
        builder.Property(q => q.QueuedAt).HasColumnName("queued_at").IsRequired();
        builder.Property(q => q.StartedAt).HasColumnName("started_at");
        builder.Property(q => q.CompletedAt).HasColumnName("completed_at");

        builder.HasOne(q => q.Feed)
            .WithMany(f => f.DownloadQueue)
            .HasForeignKey(q => q.FeedId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(q => new { q.FeedId, q.VideoId }).IsUnique().HasDatabaseName("idx_queue_feed_video");
        builder.HasIndex(q => new { q.FeedId, q.Status }).HasDatabaseName("idx_queue_feed_status");
        builder.HasIndex(q => q.Status).HasDatabaseName("idx_queue_status");
    }
}
