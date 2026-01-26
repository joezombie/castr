using Microsoft.AspNetCore.SignalR;
using Castr.Hubs;

namespace Castr.Tests.Hubs;

public class DownloadProgressHubTests
{
    private readonly Mock<IHubCallerClients> _mockClients;
    private readonly Mock<IClientProxy> _mockClientProxy;
    private readonly Mock<ILogger<DownloadProgressHub>> _mockLogger;
    private readonly DownloadProgressHub _hub;

    public DownloadProgressHubTests()
    {
        _mockClients = new Mock<IHubCallerClients>();
        _mockClientProxy = new Mock<IClientProxy>();
        _mockLogger = new Mock<ILogger<DownloadProgressHub>>();

        _mockClients.Setup(c => c.All).Returns(_mockClientProxy.Object);

        _hub = new DownloadProgressHub(_mockLogger.Object)
        {
            Clients = _mockClients.Object
        };
    }

    [Fact]
    public async Task BroadcastDownloadStarted_SendsToAllClients()
    {
        // Arrange
        var feedId = 1;
        var videoId = "abc123";
        var title = "Test Video";

        // Act
        await _hub.BroadcastDownloadStarted(feedId, videoId, title);

        // Assert
        _mockClientProxy.Verify(
            x => x.SendCoreAsync(
                "DownloadStarted",
                It.Is<object[]>(o => o.Length == 3 &&
                    (int)o[0] == feedId &&
                    (string)o[1] == videoId &&
                    (string)o[2] == title),
                default),
            Times.Once);
    }

    [Fact]
    public async Task BroadcastDownloadProgress_SendsToAllClients()
    {
        // Arrange
        var feedId = 1;
        var videoId = "abc123";
        var percent = 50;

        // Act
        await _hub.BroadcastDownloadProgress(feedId, videoId, percent);

        // Assert
        _mockClientProxy.Verify(
            x => x.SendCoreAsync(
                "DownloadProgress",
                It.Is<object[]>(o => o.Length == 3 &&
                    (int)o[0] == feedId &&
                    (string)o[1] == videoId &&
                    (int)o[2] == percent),
                default),
            Times.Once);
    }

    [Fact]
    public async Task BroadcastDownloadCompleted_SendsToAllClients()
    {
        // Arrange
        var feedId = 1;
        var videoId = "abc123";
        var filename = "episode.mp3";

        // Act
        await _hub.BroadcastDownloadCompleted(feedId, videoId, filename);

        // Assert
        _mockClientProxy.Verify(
            x => x.SendCoreAsync(
                "DownloadCompleted",
                It.Is<object[]>(o => o.Length == 3),
                default),
            Times.Once);
    }

    [Fact]
    public async Task BroadcastDownloadFailed_SendsToAllClients()
    {
        // Arrange
        var feedId = 1;
        var videoId = "abc123";
        var error = "Download failed";

        // Act
        await _hub.BroadcastDownloadFailed(feedId, videoId, error);

        // Assert
        _mockClientProxy.Verify(
            x => x.SendCoreAsync(
                "DownloadFailed",
                It.Is<object[]>(o => o.Length == 3),
                default),
            Times.Once);
    }

    [Fact]
    public async Task BroadcastActivityLogged_SendsToAllClients()
    {
        // Arrange
        var activityType = "download";
        var message = "Downloaded episode";
        var feedId = 1;

        // Act
        await _hub.BroadcastActivityLogged(activityType, message, feedId);

        // Assert
        _mockClientProxy.Verify(
            x => x.SendCoreAsync(
                "ActivityLogged",
                It.Is<object[]>(o => o.Length == 3),
                default),
            Times.Once);
    }

    [Fact]
    public async Task BroadcastActivityLogged_WithNullFeedId_SendsToAllClients()
    {
        // Arrange
        var activityType = "system";
        var message = "System event";

        // Act
        await _hub.BroadcastActivityLogged(activityType, message, null);

        // Assert
        _mockClientProxy.Verify(
            x => x.SendCoreAsync(
                "ActivityLogged",
                It.Is<object[]>(o => o.Length == 3 && o[2] == null),
                default),
            Times.Once);
    }

    [Fact]
    public async Task BroadcastStatisticsUpdated_SendsToAllClients()
    {
        // Arrange
        var feedsCount = 5;
        var episodesCount = 100;
        var activeDownloads = 2;

        // Act
        await _hub.BroadcastStatisticsUpdated(feedsCount, episodesCount, activeDownloads);

        // Assert
        _mockClientProxy.Verify(
            x => x.SendCoreAsync(
                "StatisticsUpdated",
                It.Is<object[]>(o => o.Length == 3 &&
                    (int)o[0] == feedsCount &&
                    (int)o[1] == episodesCount &&
                    (int)o[2] == activeDownloads),
                default),
            Times.Once);
    }

    [Fact]
    public async Task BroadcastDownloadProgress_WithZeroPercent_Works()
    {
        // Arrange
        var feedId = 1;
        var videoId = "vid";
        var percent = 0;

        // Act
        await _hub.BroadcastDownloadProgress(feedId, videoId, percent);

        // Assert
        _mockClientProxy.Verify(
            x => x.SendCoreAsync("DownloadProgress", It.IsAny<object[]>(), default),
            Times.Once);
    }

    [Fact]
    public async Task BroadcastDownloadProgress_With100Percent_Works()
    {
        // Arrange
        var feedId = 1;
        var videoId = "vid";
        var percent = 100;

        // Act
        await _hub.BroadcastDownloadProgress(feedId, videoId, percent);

        // Assert
        _mockClientProxy.Verify(
            x => x.SendCoreAsync("DownloadProgress", It.IsAny<object[]>(), default),
            Times.Once);
    }
}
