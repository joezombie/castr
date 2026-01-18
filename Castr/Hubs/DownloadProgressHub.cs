using Microsoft.AspNetCore.SignalR;

namespace Castr.Hubs;

/// <summary>
/// SignalR hub for real-time download progress updates.
/// </summary>
public class DownloadProgressHub : Hub
{
    private readonly ILogger<DownloadProgressHub> _logger;

    public DownloadProgressHub(ILogger<DownloadProgressHub> logger)
    {
        _logger = logger;
    }

    public async Task DownloadStarted(int feedId, string videoId, string title)
    {
        _logger.LogDebug("Broadcasting download started: {VideoId}", videoId);
        await Clients.All.SendAsync("DownloadStarted", feedId, videoId, title);
    }

    public async Task DownloadProgress(int feedId, string videoId, int percent)
    {
        _logger.LogTrace("Broadcasting download progress: {VideoId} - {Percent}%", videoId, percent);
        await Clients.All.SendAsync("DownloadProgress", feedId, videoId, percent);
    }

    public async Task DownloadCompleted(int feedId, string videoId, string filename)
    {
        _logger.LogDebug("Broadcasting download completed: {VideoId}", videoId);
        await Clients.All.SendAsync("DownloadCompleted", feedId, videoId, filename);
    }

    public async Task DownloadFailed(int feedId, string videoId, string error)
    {
        _logger.LogDebug("Broadcasting download failed: {VideoId}", videoId);
        await Clients.All.SendAsync("DownloadFailed", feedId, videoId, error);
    }

    public async Task ActivityLogged(string activityType, string message)
    {
        _logger.LogDebug("Broadcasting activity: {ActivityType}", activityType);
        await Clients.All.SendAsync("ActivityLogged", activityType, message);
    }
}
