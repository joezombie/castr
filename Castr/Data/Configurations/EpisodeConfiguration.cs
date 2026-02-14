using Castr.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Castr.Data.Configurations;

public class EpisodeConfiguration : IEntityTypeConfiguration<Episode>
{
    public void Configure(EntityTypeBuilder<Episode> builder)
    {
        builder.ToTable("episodes");

        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).HasColumnName("id");

        builder.Property(e => e.FeedId).HasColumnName("feed_id").IsRequired();
        builder.Property(e => e.Filename).HasColumnName("filename").IsRequired();
        builder.Property(e => e.VideoId).HasColumnName("video_id");
        builder.Property(e => e.YoutubeTitle).HasColumnName("youtube_title");
        builder.Property(e => e.Title).HasColumnName("title");
        builder.Property(e => e.Description).HasColumnName("description");
        builder.Property(e => e.ThumbnailUrl).HasColumnName("thumbnail_url");
        builder.Property(e => e.DisplayOrder).HasColumnName("display_order").IsRequired();
        builder.Property(e => e.AddedAt).HasColumnName("added_at").IsRequired();
        builder.Property(e => e.PublishDate).HasColumnName("publish_date");
        builder.Property(e => e.MatchScore).HasColumnName("match_score");
        builder.Property(e => e.DurationSeconds).HasColumnName("duration_seconds");
        builder.Property(e => e.FileSize).HasColumnName("file_size");
        builder.Ignore(e => e.Duration);

        builder.HasOne(e => e.Feed)
            .WithMany(f => f.Episodes)
            .HasForeignKey(e => e.FeedId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(e => e.FeedId).HasDatabaseName("idx_episodes_feed_id");
        builder.HasIndex(e => new { e.FeedId, e.Filename }).IsUnique().HasDatabaseName("idx_episodes_filename");
        builder.HasIndex(e => e.VideoId).HasDatabaseName("idx_episodes_video_id");
        builder.HasIndex(e => new { e.FeedId, e.DisplayOrder }).HasDatabaseName("idx_episodes_display_order");
    }
}
