using Castr.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Castr.Data.Configurations;

public class SkippedVideoConfiguration : IEntityTypeConfiguration<SkippedVideo>
{
    public void Configure(EntityTypeBuilder<SkippedVideo> builder)
    {
        builder.ToTable("skipped_videos");

        builder.HasKey(s => s.Id);
        builder.Property(s => s.Id).HasColumnName("id");

        builder.Property(s => s.FeedId).HasColumnName("feed_id").IsRequired();
        builder.Property(s => s.VideoId).HasColumnName("video_id").IsRequired();
        builder.Property(s => s.SkipReason).HasColumnName("skip_reason").IsRequired();
        builder.Property(s => s.FilterHash).HasColumnName("filter_hash").IsRequired();
        builder.Property(s => s.SkippedAt).HasColumnName("skipped_at").IsRequired();

        builder.HasOne(s => s.Feed)
            .WithMany(f => f.SkippedVideos)
            .HasForeignKey(s => s.FeedId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(s => new { s.FeedId, s.VideoId }).IsUnique().HasDatabaseName("idx_skipped_feed_video");
    }
}
