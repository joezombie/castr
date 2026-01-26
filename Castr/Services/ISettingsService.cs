namespace Castr.Services;

public interface ISettingsService
{
    Task<bool> GetDarkModeAsync();
    Task SetDarkModeAsync(bool isDarkMode);
    Task<int> GetDefaultPollIntervalAsync();
    Task SetDefaultPollIntervalAsync(int minutes);
    Task<string> GetDefaultAudioQualityAsync();
    Task SetDefaultAudioQualityAsync(string quality);
    Task<string> GetDefaultLanguageAsync();
    Task SetDefaultLanguageAsync(string language);
    Task<string> GetDefaultFileExtensionsAsync();
    Task SetDefaultFileExtensionsAsync(string extensions);
    Task<string> GetDefaultCategoryAsync();
    Task SetDefaultCategoryAsync(string category);
}
