using Castr.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Castr.Data.Configurations;

public class DownloadedVideoConfiguration : IEntityTypeConfiguration<DownloadedVideo>
{
    public void Configure(EntityTypeBuilder<DownloadedVideo> builder)
    {
        builder.ToTable("downloaded_videos");

        builder.HasKey(d => d.Id);
        builder.Property(d => d.Id).HasColumnName("id");

        builder.Property(d => d.FeedId).HasColumnName("feed_id").IsRequired();
        builder.Property(d => d.VideoId).HasColumnName("video_id").IsRequired();
        builder.Property(d => d.Filename).HasColumnName("filename");
        builder.Property(d => d.DownloadedAt).HasColumnName("downloaded_at").IsRequired();

        builder.HasOne(d => d.Feed)
            .WithMany(f => f.DownloadedVideos)
            .HasForeignKey(d => d.FeedId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(d => new { d.FeedId, d.VideoId }).IsUnique().HasDatabaseName("idx_downloaded_feed_video");
    }
}
