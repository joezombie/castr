using Microsoft.JSInterop;

namespace Castr.Services;

public class SettingsService : ISettingsService
{
    private readonly IJSRuntime _jsRuntime;
    private readonly ILogger<SettingsService> _logger;

    private const string DarkModeKey = "castr_darkMode";
    private const string PollIntervalKey = "castr_pollInterval";
    private const string AudioQualityKey = "castr_audioQuality";
    private const string LanguageKey = "castr_language";
    private const string FileExtensionsKey = "castr_fileExtensions";
    private const string CategoryKey = "castr_category";

    public SettingsService(IJSRuntime jsRuntime, ILogger<SettingsService> logger)
    {
        _jsRuntime = jsRuntime;
        _logger = logger;
    }

    public async Task<bool> GetDarkModeAsync()
    {
        try
        {
            var value = await _jsRuntime.InvokeAsync<string?>("localStorage.getItem", DarkModeKey);
            return string.IsNullOrEmpty(value) || value == "true"; // Default to dark mode
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to get dark mode setting");
            return true;
        }
    }

    public async Task SetDarkModeAsync(bool isDarkMode)
    {
        try
        {
            await _jsRuntime.InvokeVoidAsync("localStorage.setItem", DarkModeKey, isDarkMode.ToString().ToLower());
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to set dark mode setting");
        }
    }

    public async Task<int> GetDefaultPollIntervalAsync()
    {
        try
        {
            var value = await _jsRuntime.InvokeAsync<string?>("localStorage.getItem", PollIntervalKey);
            return int.TryParse(value, out var interval) ? interval : 60;
        }
        catch
        {
            return 60;
        }
    }

    public async Task SetDefaultPollIntervalAsync(int minutes)
    {
        await _jsRuntime.InvokeVoidAsync("localStorage.setItem", PollIntervalKey, minutes.ToString());
    }

    public async Task<string> GetDefaultAudioQualityAsync()
    {
        try
        {
            var value = await _jsRuntime.InvokeAsync<string?>("localStorage.getItem", AudioQualityKey);
            return value ?? "highest";
        }
        catch
        {
            return "highest";
        }
    }

    public async Task SetDefaultAudioQualityAsync(string quality)
    {
        await _jsRuntime.InvokeVoidAsync("localStorage.setItem", AudioQualityKey, quality);
    }

    public async Task<string> GetDefaultLanguageAsync()
    {
        try
        {
            var value = await _jsRuntime.InvokeAsync<string?>("localStorage.getItem", LanguageKey);
            return value ?? "en-us";
        }
        catch
        {
            return "en-us";
        }
    }

    public async Task SetDefaultLanguageAsync(string language)
    {
        await _jsRuntime.InvokeVoidAsync("localStorage.setItem", LanguageKey, language);
    }

    public async Task<string> GetDefaultFileExtensionsAsync()
    {
        try
        {
            var value = await _jsRuntime.InvokeAsync<string?>("localStorage.getItem", FileExtensionsKey);
            return value ?? ".mp3";
        }
        catch
        {
            return ".mp3";
        }
    }

    public async Task SetDefaultFileExtensionsAsync(string extensions)
    {
        await _jsRuntime.InvokeVoidAsync("localStorage.setItem", FileExtensionsKey, extensions);
    }

    public async Task<string> GetDefaultCategoryAsync()
    {
        try
        {
            var value = await _jsRuntime.InvokeAsync<string?>("localStorage.getItem", CategoryKey);
            return value ?? "Society & Culture";
        }
        catch
        {
            return "Society & Culture";
        }
    }

    public async Task SetDefaultCategoryAsync(string category)
    {
        await _jsRuntime.InvokeVoidAsync("localStorage.setItem", CategoryKey, category);
    }
}
