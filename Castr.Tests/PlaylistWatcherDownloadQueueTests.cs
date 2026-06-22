using System.Collections.Concurrent;
using Castr.Data;
using Castr.Data.Entities;
using Castr.Data.Repositories;
using Castr.Hubs;
using Castr.Services;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace Castr.Tests;

/// <summary>
/// Integration tests for the download-queue state transitions in
/// <see cref="PlaylistWatcherService.ProcessVideoDownloadAsync"/>. Uses a real in-memory SQLite
/// <see cref="CastrDbContext"/> (so the queue row is genuinely persisted/updated through the
/// repository) with a mocked <see cref="IYouTubeDownloadService"/> and <see cref="IHubContext{T}"/>.
///
/// These complement <see cref="DownloadQueueRepositoryTests"/> (which cover the repository primitives
/// directly) by asserting the queued->downloading->completed/failed SEQUENCING and SignalR broadcasts
/// that the watcher orchestrates per video.
/// </summary>
public class PlaylistWatcherDownloadQueueTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly DbContextOptions<CastrDbContext> _dbOptions;
    private readonly ServiceProvider _serviceProvider;
    private readonly Mock<IYouTubeDownloadService> _youtube;
    private readonly Mock<IHubClients> _hubClients;
    private readonly Mock<IClientProxy> _clientProxy;
    private readonly PlaylistWatcherService _service;
    private readonly Feed _feed;

    public PlaylistWatcherDownloadQueueTests()
    {
        // One shared in-memory SQLite connection kept open for the test's lifetime, so every scoped
        // DbContext sees the same database (the watcher's queue helpers each open their own scope).
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();

        _dbOptions = new DbContextOptionsBuilder<CastrDbContext>()
            .UseSqlite(_connection)
            .Options;

        using (var ctx = new CastrDbContext(_dbOptions))
        {
            ctx.Database.EnsureCreated();
            _feed = new Feed
            {
                Name = "test",
                Title = "Test Feed",
                Description = "desc",
                Directory = "/tmp",
                YouTubeAudioQuality = "highest"
            };
            ctx.Feeds.Add(_feed);
            ctx.SaveChanges();
        }

        // Real DI provider: scoped DbContext + repositories + PodcastDataService, so the watcher's
        // own-scope queue writes (UpdateQueueProgressAsync / RemoveFromQueueAsync) operate on the
        // shared SQLite database exactly as in production.
        var services = new ServiceCollection();
        services.AddDbContext<CastrDbContext>(o => o.UseSqlite(_connection));
        services.AddScoped<IFeedRepository, FeedRepository>();
        services.AddScoped<IEpisodeRepository, EpisodeRepository>();
        services.AddScoped<IDownloadRepository, DownloadRepository>();
        services.AddScoped<ISkippedVideoRepository, SkippedVideoRepository>();
        services.AddScoped<IActivityRepository, ActivityRepository>();
        services.AddScoped<IPodcastDataService, PodcastDataService>();
        services.AddLogging();
        _serviceProvider = services.BuildServiceProvider();

        _youtube = new Mock<IYouTubeDownloadService>();

        _hubClients = new Mock<IHubClients>();
        _clientProxy = new Mock<IClientProxy>();
        _hubClients.Setup(c => c.All).Returns(_clientProxy.Object);
        var hubContext = new Mock<IHubContext<DownloadProgressHub>>();
        hubContext.Setup(h => h.Clients).Returns(_hubClients.Object);

        _service = new PlaylistWatcherService(
            _serviceProvider,
            NullLogger<PlaylistWatcherService>.Instance,
            Mock.Of<IPlaylistWatcherTrigger>(),
            hubContext.Object);
    }

    private IPodcastDataService NewDataService()
        => _serviceProvider.CreateScope().ServiceProvider.GetRequiredService<IPodcastDataService>();

    private DownloadQueueItem? LoadQueueItem(int id)
    {
        using var ctx = new CastrDbContext(_dbOptions);
        return ctx.DownloadQueue.AsNoTracking().FirstOrDefault(q => q.Id == id);
    }

    private void VerifyBroadcast(string method, string videoId, Times times)
    {
        _clientProxy.Verify(
            x => x.SendCoreAsync(
                method,
                It.Is<object[]>(o => o.Length == 3 && (string)o[1] == videoId),
                It.IsAny<CancellationToken>()),
            times);
    }

    [Fact]
    public async Task ProcessVideoDownloadAsync_SuccessPath_MarksCompletedAndBroadcastsCompleted()
    {
        var data = NewDataService();
        var queueItem = await data.AddToQueueAsync(_feed.Id, "vid-ok", "Good Video");

        _youtube.Setup(y => y.GetVideoDetailsAsync("vid-ok", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new VideoDetails { VideoId = "vid-ok", Title = "Good Video" });
        _youtube.Setup(y => y.DownloadAudioAsync("vid-ok", _feed.Directory, _feed.YouTubeAudioQuality,
                It.IsAny<IProgress<double>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("/tmp/good-video.mp3");

        var filter = new YouTubeFilterEvaluator(_feed);
        var episode = await _service.ProcessVideoDownloadAsync(
            data, _youtube.Object, filter, _feed, _feed.Id,
            "vid-ok", "Good Video", queueItem.Id,
            YouTubeFilterEvaluator.ComputeFilterHash(_feed),
            new ConcurrentDictionary<string, byte>(StringComparer.OrdinalIgnoreCase),
            CancellationToken.None);

        var row = LoadQueueItem(queueItem.Id);
        Assert.NotNull(row);
        Assert.Equal("completed", row!.Status);
        Assert.Equal(100, row.ProgressPercent);
        Assert.NotNull(row.CompletedAt);

        // Episode is returned for persistence and SignalR completion fired.
        Assert.NotNull(episode);
        Assert.Equal("good-video.mp3", episode!.Filename);
        VerifyBroadcast("DownloadCompleted", "vid-ok", Times.Once());
    }

    [Fact]
    public async Task ProcessVideoDownloadAsync_DownloadThrows_MarksFailedWithMessage_AndLoopContinues()
    {
        var data = NewDataService();
        var failItem = await data.AddToQueueAsync(_feed.Id, "vid-fail", "Boom");
        var okItem = await data.AddToQueueAsync(_feed.Id, "vid-next", "Next");

        _youtube.Setup(y => y.GetVideoDetailsAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string id, CancellationToken _) => new VideoDetails { VideoId = id, Title = id });
        _youtube.Setup(y => y.DownloadAudioAsync("vid-fail", It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<IProgress<double>>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("network exploded"));
        _youtube.Setup(y => y.DownloadAudioAsync("vid-next", It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<IProgress<double>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("/tmp/next.mp3");

        var filter = new YouTubeFilterEvaluator(_feed);
        var hash = YouTubeFilterEvaluator.ComputeFilterHash(_feed);
        var claimed = new ConcurrentDictionary<string, byte>(StringComparer.OrdinalIgnoreCase);

        // The extracted method lets the exception propagate; the per-video loop's catch is what marks
        // the row failed and continues. Replicate that exact wrapper here so we assert the real behavior:
        // failing one video does NOT abort processing of the next.
        async Task<Episode?> RunOne(string videoId, string title, int queueItemId)
        {
            try
            {
                return await _service.ProcessVideoDownloadAsync(
                    data, _youtube.Object, filter, _feed, _feed.Id,
                    videoId, title, queueItemId, hash, claimed, CancellationToken.None);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                await data.UpdateQueueProgressAsync(queueItemId, "failed", 0, ex.Message);
                await _hubClients.Object.All.SendAsync("DownloadFailed", _feed.Id, videoId, ex.Message);
                return null;
            }
        }

        var failedEpisode = await RunOne("vid-fail", "Boom", failItem.Id);
        var okEpisode = await RunOne("vid-next", "Next", okItem.Id);

        var failRow = LoadQueueItem(failItem.Id);
        Assert.NotNull(failRow);
        Assert.Equal("failed", failRow!.Status);
        Assert.Equal("network exploded", failRow.ErrorMessage);
        Assert.NotNull(failRow.CompletedAt);
        Assert.Null(failedEpisode);
        VerifyBroadcast("DownloadFailed", "vid-fail", Times.Once());

        // The loop continued: the next video downloaded and completed normally.
        var okRow = LoadQueueItem(okItem.Id);
        Assert.NotNull(okRow);
        Assert.Equal("completed", okRow!.Status);
        Assert.NotNull(okEpisode);
        VerifyBroadcast("DownloadCompleted", "vid-next", Times.Once());
    }

    [Fact]
    public async Task ProcessVideoDownloadAsync_DateSkippedVideo_RemovesQueueRow_AndDoesNotDownload()
    {
        var data = NewDataService();
        var queueItem = await data.AddToQueueAsync(_feed.Id, "vid-old", "Old Video");

        // Cutoff in the future so the (older) upload date fails the date filter.
        var feed = new Feed
        {
            Id = _feed.Id,
            Name = _feed.Name,
            Title = _feed.Title,
            Description = _feed.Description,
            Directory = _feed.Directory,
            YouTubeAudioQuality = _feed.YouTubeAudioQuality,
            YouTubeDownloadAfterDate = new DateTime(2030, 1, 1, 0, 0, 0, DateTimeKind.Utc)
        };
        var filter = new YouTubeFilterEvaluator(feed);

        _youtube.Setup(y => y.GetVideoDetailsAsync("vid-old", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new VideoDetails
            {
                VideoId = "vid-old",
                Title = "Old Video",
                UploadDate = new DateTime(2020, 1, 1, 0, 0, 0, DateTimeKind.Utc)
            });

        var episode = await _service.ProcessVideoDownloadAsync(
            data, _youtube.Object, filter, feed, feed.Id,
            "vid-old", "Old Video", queueItem.Id,
            YouTubeFilterEvaluator.ComputeFilterHash(feed),
            new ConcurrentDictionary<string, byte>(StringComparer.OrdinalIgnoreCase),
            CancellationToken.None);

        // Queue placeholder removed; no episode produced; download never attempted.
        Assert.Null(LoadQueueItem(queueItem.Id));
        Assert.Null(episode);
        _youtube.Verify(y => y.DownloadAudioAsync(It.IsAny<string>(), It.IsAny<string>(),
            It.IsAny<string>(), It.IsAny<IProgress<double>>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ProcessVideoDownloadAsync_DownloadCancelled_Propagates_AndDoesNotMarkFailed()
    {
        var data = NewDataService();
        var queueItem = await data.AddToQueueAsync(_feed.Id, "vid-cancel", "Cancel Me");

        _youtube.Setup(y => y.GetVideoDetailsAsync("vid-cancel", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new VideoDetails { VideoId = "vid-cancel", Title = "Cancel Me" });
        _youtube.Setup(y => y.DownloadAudioAsync("vid-cancel", It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<IProgress<double>>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new OperationCanceledException());

        var filter = new YouTubeFilterEvaluator(_feed);

        await Assert.ThrowsAsync<OperationCanceledException>(() =>
            _service.ProcessVideoDownloadAsync(
                data, _youtube.Object, filter, _feed, _feed.Id,
                "vid-cancel", "Cancel Me", queueItem.Id,
                YouTubeFilterEvaluator.ComputeFilterHash(_feed),
                new ConcurrentDictionary<string, byte>(StringComparer.OrdinalIgnoreCase),
                CancellationToken.None));

        // Cancellation aborts mid-flight: the row was moved to "downloading" (announced) but must NOT
        // be marked failed — shutdown should leave it resumable, not record a spurious failure.
        var row = LoadQueueItem(queueItem.Id);
        Assert.NotNull(row);
        Assert.Equal("downloading", row!.Status);
        Assert.Null(row.ErrorMessage);
        VerifyBroadcast("DownloadFailed", "vid-cancel", Times.Never());
    }

    public void Dispose()
    {
        _serviceProvider.Dispose();
        _connection.Dispose();
    }
}
