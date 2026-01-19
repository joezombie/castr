using Castr.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Castr.Data.Configurations;

/// <summary>
/// Entity Framework Core configuration for DownloadedVideo entity.
/// </summary>
public class DownloadedVideoConfiguration : IEntityTypeConfiguration<DownloadedVideo>
{
    public void Configure(EntityTypeBuilder<DownloadedVideo> builder)
    {
        builder.ToTable("downloaded_videos");
        
        // Primary key
        builder.HasKey(dv => dv.Id);
        
        // Indexes
        builder.HasIndex(dv => new { dv.FeedId, dv.VideoId }).IsUnique();
        
        // Relationship configured in FeedConfiguration
    }
}
