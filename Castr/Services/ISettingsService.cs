namespace Castr.Services;

using Castr.Models;

/// <summary>
/// Service for managing application settings and user preferences.
/// Provides cached access to settings from the central database.
/// </summary>
public interface ISettingsService
{
    /// <summary>
    /// Event raised when settings are updated.
    /// </summary>
    event EventHandler<UserSettings>? SettingsChanged;
    
    /// <summary>
    /// Get current user settings (cached).
    /// </summary>
    Task<UserSettings> GetSettingsAsync();
    
    /// <summary>
    /// Save user settings and notify listeners.
    /// </summary>
    Task SaveSettingsAsync(UserSettings settings);
    
    /// <summary>
    /// Reload settings from database (bypasses cache).
    /// </summary>
    Task ReloadSettingsAsync();
}
