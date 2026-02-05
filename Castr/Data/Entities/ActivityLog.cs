using System.ComponentModel.DataAnnotations;

namespace Castr.Data.Entities;

public class ActivityLog
{
    public int Id { get; set; }

    public int? FeedId { get; set; }

    [Required, MaxLength(50)]
    public string ActivityType { get; set; } = string.Empty;

    [Required]
    public string Message { get; set; } = string.Empty;

    public string? Details { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public Feed? Feed { get; set; }
}
