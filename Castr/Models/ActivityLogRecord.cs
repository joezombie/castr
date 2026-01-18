namespace Castr.Models;

public class ActivityLogRecord
{
    public int Id { get; set; }
    public int? FeedId { get; set; }
    public required string ActivityType { get; set; }
    public required string Message { get; set; }
    public string? Details { get; set; }
    public DateTime CreatedAt { get; set; }
}
