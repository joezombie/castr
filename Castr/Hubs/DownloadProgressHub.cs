using Microsoft.AspNetCore.SignalR;

namespace Castr.Hubs;

public class DownloadProgressHub : Hub
{
    private readonly ILogger<DownloadProgressHub> _logger;

    public DownloadProgressHub(ILogger<DownloadProgressHub> logger)
    {
        _logger = logger;
    }

    public override async Task OnConnectedAsync()
    {
        _logger.LogDebug("Client connected to DownloadProgressHub: {ConnectionId}", Context.ConnectionId);
        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        _logger.LogDebug("Client disconnected from DownloadProgressHub: {ConnectionId}", Context.ConnectionId);
        await base.OnDisconnectedAsync(exception);
    }

    // Server-side methods to broadcast updates to all clients
    public async Task DownloadStarted(int feedId, string videoId, string title)
    {
        _logger.LogDebug("Broadcasting download started: FeedId={FeedId}, VideoId={VideoId}, Title={Title}", 
            feedId, videoId, title);
        await Clients.All.SendAsync("DownloadStarted", feedId, videoId, title);
    }

    public async Task DownloadProgress(int feedId, string videoId, int percent)
    {
        await Clients.All.SendAsync("DownloadProgress", feedId, videoId, percent);
    }

    public async Task DownloadCompleted(int feedId, string videoId, string filename)
    {
        _logger.LogDebug("Broadcasting download completed: FeedId={FeedId}, VideoId={VideoId}, Filename={Filename}", 
            feedId, videoId, filename);
        await Clients.All.SendAsync("DownloadCompleted", feedId, videoId, filename);
    }

    public async Task DownloadFailed(int feedId, string videoId, string error)
    {
        _logger.LogWarning("Broadcasting download failed: FeedId={FeedId}, VideoId={VideoId}, Error={Error}", 
            feedId, videoId, error);
        await Clients.All.SendAsync("DownloadFailed", feedId, videoId, error);
    }

    public async Task ActivityLogged(string activityType, string message)
    {
        _logger.LogDebug("Broadcasting activity: {ActivityType} - {Message}", activityType, message);
        await Clients.All.SendAsync("ActivityLogged", activityType, message);
    }

    public async Task StatisticsUpdated(int feedsCount, int episodesCount, int activeDownloadsCount)
    {
        await Clients.All.SendAsync("StatisticsUpdated", feedsCount, episodesCount, activeDownloadsCount);
    }
}
