namespace Castr.Models;

/// <summary>
/// Represents user preferences and application settings.
/// Stored in the central database for persistence across sessions.
/// </summary>
public class UserSettings
{
    public int Id { get; set; }
    
    // Application Settings
    public bool DarkMode { get; set; } = true;
    public int DefaultPollingIntervalMinutes { get; set; } = 60;
    public required string DefaultAudioQuality { get; set; } = "highest";
    
    // Feed Defaults
    public required string DefaultLanguage { get; set; } = "en-us";
    public required string DefaultFileExtensions { get; set; } = ".mp3";
    public required string DefaultCategory { get; set; } = "Society & Culture";
    
    // Metadata
    public DateTime UpdatedAt { get; set; }
}
