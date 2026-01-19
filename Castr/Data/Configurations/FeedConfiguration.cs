using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Castr.Data.Entities;

namespace Castr.Data.Configurations;

/// <summary>
/// Entity configuration for Feed entity.
/// Defines relationships, indexes, and constraints.
/// </summary>
public class FeedConfiguration : IEntityTypeConfiguration<Feed>
{
    public void Configure(EntityTypeBuilder<Feed> builder)
    {
        builder.ToTable("feeds");
        
        builder.HasKey(f => f.Id);
        
        builder.Property(f => f.Id)
            .HasColumnName("id");
        
        builder.Property(f => f.Name)
            .HasColumnName("name")
            .IsRequired()
            .HasMaxLength(100);
        
        builder.Property(f => f.Title)
            .HasColumnName("title")
            .IsRequired()
            .HasMaxLength(500);
        
        builder.Property(f => f.Description)
            .HasColumnName("description")
            .IsRequired();
        
        builder.Property(f => f.Directory)
            .HasColumnName("directory")
            .IsRequired()
            .HasMaxLength(1000);
        
        builder.Property(f => f.Author)
            .HasColumnName("author")
            .HasMaxLength(200);
        
        builder.Property(f => f.ImageUrl)
            .HasColumnName("image_url")
            .HasMaxLength(2000);
        
        builder.Property(f => f.Link)
            .HasColumnName("link")
            .HasMaxLength(2000);
        
        builder.Property(f => f.Language)
            .HasColumnName("language")
            .HasMaxLength(10)
            .HasDefaultValue("en-us");
        
        builder.Property(f => f.Category)
            .HasColumnName("category")
            .HasMaxLength(100);
        
        builder.Property(f => f.FileExtensions)
            .HasColumnName("file_extensions")
            .HasMaxLength(200)
            .HasDefaultValue(".mp3");
        
        builder.Property(f => f.YouTubePlaylistUrl)
            .HasColumnName("youtube_playlist_url")
            .HasMaxLength(2000);
        
        builder.Property(f => f.YouTubePollIntervalMinutes)
            .HasColumnName("youtube_poll_interval_minutes")
            .HasDefaultValue(60);
        
        builder.Property(f => f.YouTubeEnabled)
            .HasColumnName("youtube_enabled")
            .HasDefaultValue(false);
        
        builder.Property(f => f.YouTubeMaxConcurrentDownloads)
            .HasColumnName("youtube_max_concurrent_downloads")
            .HasDefaultValue(1);
        
        builder.Property(f => f.YouTubeAudioQuality)
            .HasColumnName("youtube_audio_quality")
            .HasMaxLength(50)
            .HasDefaultValue("highest");
        
        builder.Property(f => f.CreatedAt)
            .HasColumnName("created_at")
            .IsRequired();
        
        builder.Property(f => f.UpdatedAt)
            .HasColumnName("updated_at")
            .IsRequired();
        
        builder.Property(f => f.IsActive)
            .HasColumnName("is_active")
            .HasDefaultValue(true);
        
        // Unique constraint on Name
        builder.HasIndex(f => f.Name)
            .IsUnique()
            .HasDatabaseName("idx_feeds_name");
        
        // Index on IsActive for filtering
        builder.HasIndex(f => f.IsActive)
            .HasDatabaseName("idx_feeds_active");
        
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
