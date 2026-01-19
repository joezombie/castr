using Castr.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Castr.Data.Configurations;

/// <summary>
/// Entity Framework Core configuration for Episode entity.
/// </summary>
public class EpisodeConfiguration : IEntityTypeConfiguration<Episode>
{
    public void Configure(EntityTypeBuilder<Episode> builder)
    {
        builder.ToTable("episodes");
        
        // Primary key
        builder.HasKey(e => e.Id);
        
        // Indexes
        builder.HasIndex(e => new { e.FeedId, e.Filename }).IsUnique();
        builder.HasIndex(e => e.VideoId);
        builder.HasIndex(e => new { e.FeedId, e.DisplayOrder });
        
        // Relationship configured in FeedConfiguration
    }
}
