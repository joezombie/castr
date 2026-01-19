using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Castr.Data.Entities;

namespace Castr.Data.Configurations;

/// <summary>
/// Entity configuration for DownloadedVideo entity.
/// Defines relationships, indexes, and constraints.
/// </summary>
public class DownloadedVideoConfiguration : IEntityTypeConfiguration<DownloadedVideo>
{
    public void Configure(EntityTypeBuilder<DownloadedVideo> builder)
    {
        builder.ToTable("downloaded_videos");
        
        builder.HasKey(dv => dv.Id);
        
        builder.Property(dv => dv.Id)
            .HasColumnName("id");
        
        builder.Property(dv => dv.FeedId)
            .HasColumnName("feed_id")
            .IsRequired();
        
        builder.Property(dv => dv.VideoId)
            .HasColumnName("video_id")
            .IsRequired()
            .HasMaxLength(100);
        
        builder.Property(dv => dv.Filename)
            .HasColumnName("filename")
            .HasMaxLength(500);
        
        builder.Property(dv => dv.DownloadedAt)
            .HasColumnName("downloaded_at")
            .IsRequired();
        
        // Unique constraint on FeedId + VideoId to prevent duplicates
        builder.HasIndex(dv => new { dv.FeedId, dv.VideoId })
            .IsUnique()
            .HasDatabaseName("idx_downloaded_feed_video");
        
        // Relationship is configured in FeedConfiguration
    }
}
