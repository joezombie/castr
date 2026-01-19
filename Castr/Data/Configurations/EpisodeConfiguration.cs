using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Castr.Data.Entities;

namespace Castr.Data.Configurations;

/// <summary>
/// Entity configuration for Episode entity.
/// Defines relationships, indexes, and constraints.
/// </summary>
public class EpisodeConfiguration : IEntityTypeConfiguration<Episode>
{
    public void Configure(EntityTypeBuilder<Episode> builder)
    {
        builder.ToTable("episodes");
        
        builder.HasKey(e => e.Id);
        
        builder.Property(e => e.Id)
            .HasColumnName("id");
        
        builder.Property(e => e.FeedId)
            .HasColumnName("feed_id")
            .IsRequired();
        
        builder.Property(e => e.Filename)
            .HasColumnName("filename")
            .IsRequired()
            .HasMaxLength(500);
        
        builder.Property(e => e.VideoId)
            .HasColumnName("video_id")
            .HasMaxLength(100);
        
        builder.Property(e => e.YoutubeTitle)
            .HasColumnName("youtube_title")
            .HasMaxLength(500);
        
        builder.Property(e => e.Description)
            .HasColumnName("description");
        
        builder.Property(e => e.ThumbnailUrl)
            .HasColumnName("thumbnail_url")
            .HasMaxLength(2000);
        
        builder.Property(e => e.DisplayOrder)
            .HasColumnName("display_order")
            .IsRequired()
            .HasDefaultValue(0);
        
        builder.Property(e => e.AddedAt)
            .HasColumnName("added_at")
            .IsRequired();
        
        builder.Property(e => e.PublishDate)
            .HasColumnName("publish_date");
        
        builder.Property(e => e.MatchScore)
            .HasColumnName("match_score")
            .HasPrecision(5, 4);
        
        // Indexes for performance
        builder.HasIndex(e => new { e.FeedId, e.Filename })
            .HasDatabaseName("idx_episodes_filename");
        
        builder.HasIndex(e => e.VideoId)
            .HasDatabaseName("idx_episodes_video_id");
        
        builder.HasIndex(e => new { e.FeedId, e.DisplayOrder })
            .HasDatabaseName("idx_episodes_display_order");
        
        // Relationship is configured in FeedConfiguration
    }
}
