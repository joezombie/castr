using System.Threading.Channels;

namespace Castr.Services;

/// <summary>
/// A request to immediately process a feed, optionally enriching episode metadata
/// (real upload dates via per-video fetches) — used by Clear &amp; Resync.
/// </summary>
public readonly record struct FeedProcessingTrigger(string FeedName, bool EnrichMetadata);

/// <summary>
/// Allows UI components to trigger immediate playlist processing for a feed.
/// </summary>
public interface IPlaylistWatcherTrigger
{
    void TriggerFeedProcessing(string feedName, bool enrichMetadata = false);
    IAsyncEnumerable<FeedProcessingTrigger> ReadTriggersAsync(CancellationToken cancellationToken);
}

public class PlaylistWatcherTrigger : IPlaylistWatcherTrigger
{
    private readonly Channel<FeedProcessingTrigger> _channel = Channel.CreateUnbounded<FeedProcessingTrigger>();

    public void TriggerFeedProcessing(string feedName, bool enrichMetadata = false)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(feedName);
        _channel.Writer.TryWrite(new FeedProcessingTrigger(feedName, enrichMetadata));
    }

    public IAsyncEnumerable<FeedProcessingTrigger> ReadTriggersAsync(CancellationToken cancellationToken)
    {
        return _channel.Reader.ReadAllAsync(cancellationToken);
    }
}
