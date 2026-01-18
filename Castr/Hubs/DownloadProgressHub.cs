using Microsoft.AspNetCore.SignalR;

namespace Castr.Hubs;

/// <summary>
/// SignalR hub for real-time download progress and activity updates.
/// </summary>
public class DownloadProgressHub : Hub
{
    private readonly ILogger<DownloadProgressHub> _logger;

    public DownloadProgressHub(ILogger<DownloadProgressHub> logger)
    {
        _logger = logger;
    }

    public override async Task OnConnectedAsync()
    {
        _logger.LogDebug("Client connected: {ConnectionId}", Context.ConnectionId);
        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        _logger.LogDebug("Client disconnected: {ConnectionId}", Context.ConnectionId);
        await base.OnDisconnectedAsync(exception);
    }

    /// <summary>
    /// Broadcasts that a download has started.
    /// </summary>
    public async Task BroadcastDownloadStarted(int feedId, string videoId, string title)
    {
        await Clients.All.SendAsync("DownloadStarted", feedId, videoId, title);
    }

    /// <summary>
    /// Broadcasts download progress update.
    /// </summary>
    public async Task BroadcastDownloadProgress(int feedId, string videoId, int percent)
    {
        await Clients.All.SendAsync("DownloadProgress", feedId, videoId, percent);
    }

    /// <summary>
    /// Broadcasts that a download has completed.
    /// </summary>
    public async Task BroadcastDownloadCompleted(int feedId, string videoId, string filename)
    {
        await Clients.All.SendAsync("DownloadCompleted", feedId, videoId, filename);
    }

    /// <summary>
    /// Broadcasts that a download has failed.
    /// </summary>
    public async Task BroadcastDownloadFailed(int feedId, string videoId, string error)
    {
        await Clients.All.SendAsync("DownloadFailed", feedId, videoId, error);
    }

    /// <summary>
    /// Broadcasts a new activity log entry.
    /// </summary>
    public async Task BroadcastActivityLogged(string activityType, string message, int? feedId = null)
    {
        await Clients.All.SendAsync("ActivityLogged", activityType, message, feedId);
    }

    /// <summary>
    /// Broadcasts updated statistics.
    /// </summary>
    public async Task BroadcastStatisticsUpdated(int feedsCount, int episodesCount, int activeDownloadsCount)
    {
        await Clients.All.SendAsync("StatisticsUpdated", feedsCount, episodesCount, activeDownloadsCount);
    }
}
