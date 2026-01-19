using Castr.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Castr.Data.Configurations;

/// <summary>
/// Entity Framework Core configuration for ActivityLog entity.
/// </summary>
public class ActivityLogConfiguration : IEntityTypeConfiguration<ActivityLog>
{
    public void Configure(EntityTypeBuilder<ActivityLog> builder)
    {
        builder.ToTable("activity_log");
        
        // Primary key
        builder.HasKey(al => al.Id);
        
        // Indexes
        builder.HasIndex(al => new { al.FeedId, al.CreatedAt });
        builder.HasIndex(al => al.ActivityType);
        
        // Relationship configured in FeedConfiguration
    }
}
