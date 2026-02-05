using Castr.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Castr.Data.Configurations;

public class ActivityLogConfiguration : IEntityTypeConfiguration<ActivityLog>
{
    public void Configure(EntityTypeBuilder<ActivityLog> builder)
    {
        builder.ToTable("activity_log");

        builder.HasKey(a => a.Id);
        builder.Property(a => a.Id).HasColumnName("id");

        builder.Property(a => a.FeedId).HasColumnName("feed_id");
        builder.Property(a => a.ActivityType).HasColumnName("activity_type").IsRequired();
        builder.Property(a => a.Message).HasColumnName("message").IsRequired();
        builder.Property(a => a.Details).HasColumnName("details");
        builder.Property(a => a.CreatedAt).HasColumnName("created_at").IsRequired();

        builder.HasOne(a => a.Feed)
            .WithMany(f => f.ActivityLogs)
            .HasForeignKey(a => a.FeedId)
            .OnDelete(DeleteBehavior.Cascade)
            .IsRequired(false);

        builder.HasIndex(a => new { a.FeedId, a.CreatedAt }).HasDatabaseName("idx_activity_feed_created");
        builder.HasIndex(a => a.ActivityType).HasDatabaseName("idx_activity_type");
    }
}
