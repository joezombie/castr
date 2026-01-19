namespace Castr.Services;

using Castr.Models;

/// <summary>
/// Implementation of settings service with caching and change notification.
/// </summary>
public class SettingsService : ISettingsService, IDisposable
{
    private readonly ICentralDatabaseService _database;
    private readonly ILogger<SettingsService> _logger;
    private readonly SemaphoreSlim _lock = new(1, 1);
    private UserSettings? _cachedSettings;
    private bool _disposed;
    
    public event EventHandler<UserSettings>? SettingsChanged;
    
    public SettingsService(
        ICentralDatabaseService database,
        ILogger<SettingsService> logger)
    {
        _database = database;
        _logger = logger;
    }
    
    public async Task<UserSettings> GetSettingsAsync()
    {
        if (_cachedSettings != null)
            return _cachedSettings;
            
        await _lock.WaitAsync();
        try
        {
            if (_cachedSettings != null)
                return _cachedSettings;
                
            _cachedSettings = await _database.GetUserSettingsAsync();
            _logger.LogDebug("Settings loaded from database");
            return _cachedSettings;
        }
        finally
        {
            _lock.Release();
        }
    }
    
    public async Task SaveSettingsAsync(UserSettings settings)
    {
        await _lock.WaitAsync();
        try
        {
            await _database.SaveUserSettingsAsync(settings);
            _cachedSettings = settings;
            _logger.LogInformation("Settings saved successfully");
            
            // Notify listeners (wrapped in try-catch to ensure cache consistency)
            try
            {
                SettingsChanged?.Invoke(this, settings);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error notifying settings change listeners");
            }
        }
        finally
        {
            _lock.Release();
        }
    }
    
    public async Task ReloadSettingsAsync()
    {
        await _lock.WaitAsync();
        try
        {
            _cachedSettings = await _database.GetUserSettingsAsync();
            _logger.LogDebug("Settings reloaded from database");
            
            // Notify listeners
            SettingsChanged?.Invoke(this, _cachedSettings);
        }
        finally
        {
            _lock.Release();
        }
    }
    
    public void Dispose()
    {
        if (_disposed)
            return;
            
        _lock.Dispose();
        _disposed = true;
        GC.SuppressFinalize(this);
    }
}
