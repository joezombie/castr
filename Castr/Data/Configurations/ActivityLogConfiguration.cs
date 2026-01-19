using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Castr.Data.Entities;

namespace Castr.Data.Configurations;

/// <summary>
/// Entity configuration for ActivityLog entity.
/// Defines relationships, indexes, and constraints.
/// </summary>
public class ActivityLogConfiguration : IEntityTypeConfiguration<ActivityLog>
{
    public void Configure(EntityTypeBuilder<ActivityLog> builder)
    {
        builder.ToTable("activity_log");
        
        builder.HasKey(al => al.Id);
        
        builder.Property(al => al.Id)
            .HasColumnName("id");
        
        builder.Property(al => al.FeedId)
            .HasColumnName("feed_id");
        
        builder.Property(al => al.ActivityType)
            .HasColumnName("activity_type")
            .IsRequired()
            .HasMaxLength(50);
        
        builder.Property(al => al.Message)
            .HasColumnName("message")
            .IsRequired()
            .HasMaxLength(1000);
        
        builder.Property(al => al.Details)
            .HasColumnName("details");
        
        builder.Property(al => al.CreatedAt)
            .HasColumnName("created_at")
            .IsRequired();
        
        // Indexes for dashboard queries
        builder.HasIndex(al => new { al.FeedId, al.CreatedAt })
            .HasDatabaseName("idx_activity_feed_created");
        
        builder.HasIndex(al => al.ActivityType)
            .HasDatabaseName("idx_activity_type");
        
        // Relationship is configured in FeedConfiguration
    }
}
