namespace Castr.Tests.Services;

public class PlaylistWatcherServiceTests
{
    // Helper: project a video id to itself so we can test the selection logic with plain strings
    private static string Id(string id) => id;

    [Fact]
    public void SelectNewVideosToDownload_CapsAtMaxPerPoll()
    {
        // Playlist returned newest-first, none downloaded yet
        var playlist = Enumerable.Range(1, 10).Select(i => $"v{i}").ToList();

        var result = PlaylistWatcherService.SelectNewVideosToDownload(
            playlist, Id, new HashSet<string>(), maxPerPoll: 5);

        Assert.Equal(5, result.Count);
    }

    [Fact]
    public void SelectNewVideosToDownload_ExcludesDownloaded_AndReturnsOldestFirst()
    {
        // Playlist is newest-first; "old" already downloaded
        var playlist = new List<string> { "new3", "new2", "new1", "old" };
        var downloaded = new HashSet<string> { "old" };

        var result = PlaylistWatcherService.SelectNewVideosToDownload(
            playlist, Id, downloaded, maxPerPoll: 5);

        // Oldest-first, excluding the already-downloaded "old"
        Assert.Equal(new[] { "new1", "new2", "new3" }, result);
    }

    [Fact]
    public void SelectNewVideosToDownload_ReturnsAll_WhenFewerThanCap()
    {
        var playlist = new List<string> { "a", "b", "c" };

        var result = PlaylistWatcherService.SelectNewVideosToDownload(
            playlist, Id, new HashSet<string>(), maxPerPoll: 5);

        Assert.Equal(3, result.Count);
    }

    [Fact]
    public void SelectNewVideosToDownload_CapAppliesToOldestNewVideos()
    {
        // 8 new videos (newest-first v8..v1); cap 5 should take the 5 OLDEST: v1..v5
        var playlist = new List<string> { "v8", "v7", "v6", "v5", "v4", "v3", "v2", "v1" };

        var result = PlaylistWatcherService.SelectNewVideosToDownload(
            playlist, Id, new HashSet<string>(), maxPerPoll: 5);

        Assert.Equal(new[] { "v1", "v2", "v3", "v4", "v5" }, result);
    }
}
