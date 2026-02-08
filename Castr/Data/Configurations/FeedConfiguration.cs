using Castr.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace Castr.Data.Configurations;

public class FeedConfiguration : IEntityTypeConfiguration<Feed>
{
    public void Configure(EntityTypeBuilder<Feed> builder)
    {
        builder.ToTable("feeds");

        builder.HasKey(f => f.Id);
        builder.Property(f => f.Id).HasColumnName("id");

        builder.Property(f => f.Name).HasColumnName("name").IsRequired();
        builder.Property(f => f.Title).HasColumnName("title").IsRequired();
        builder.Property(f => f.Description).HasColumnName("description").IsRequired();
        builder.Property(f => f.Directory).HasColumnName("directory").IsRequired();
        builder.Property(f => f.Author).HasColumnName("author");
        builder.Property(f => f.ImageUrl).HasColumnName("image_url");
        builder.Property(f => f.Link).HasColumnName("link");
        builder.Property(f => f.Language).HasColumnName("language").HasDefaultValue("en-us");
        builder.Property(f => f.Category).HasColumnName("category");

        var fileExtensionsConverter = new ValueConverter<string[], string>(
            v => string.Join(",", v),
            v => v.Split(",", StringSplitOptions.RemoveEmptyEntries));
        builder.Property(f => f.FileExtensions)
            .HasColumnName("file_extensions")
            .HasDefaultValueSql("'.mp3'")
            .HasConversion(fileExtensionsConverter);

        builder.Property(f => f.CacheDurationMinutes)
            .HasColumnName("cache_duration_minutes")
            .HasDefaultValue(5);

        builder.Property(f => f.YouTubePlaylistUrl).HasColumnName("youtube_playlist_url");
        builder.Property(f => f.YouTubePollIntervalMinutes).HasColumnName("youtube_poll_interval_minutes").HasDefaultValue(60);
        builder.Property(f => f.YouTubeEnabled).HasColumnName("youtube_enabled").HasDefaultValue(false);
        builder.Property(f => f.YouTubeMaxConcurrentDownloads).HasColumnName("youtube_max_concurrent_downloads").HasDefaultValue(1);
        builder.Property(f => f.YouTubeAudioQuality).HasColumnName("youtube_audio_quality").HasDefaultValue("highest");

        builder.Property(f => f.CreatedAt).HasColumnName("created_at").IsRequired();
        builder.Property(f => f.UpdatedAt).HasColumnName("updated_at").IsRequired();
        builder.Property(f => f.IsActive).HasColumnName("is_active").HasDefaultValue(true);

        builder.HasIndex(f => f.Name).IsUnique().HasDatabaseName("idx_feeds_name");
        builder.HasIndex(f => f.IsActive).HasDatabaseName("idx_feeds_is_active");
    }
}
