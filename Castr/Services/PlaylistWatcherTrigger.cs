using System.Threading.Channels;

namespace Castr.Services;

/// <summary>
/// Allows UI components to trigger immediate playlist processing for a feed.
/// </summary>
public interface IPlaylistWatcherTrigger
{
    void TriggerFeedProcessing(string feedName);
    IAsyncEnumerable<string> ReadTriggersAsync(CancellationToken cancellationToken);
}

public class PlaylistWatcherTrigger : IPlaylistWatcherTrigger
{
    private readonly Channel<string> _channel = Channel.CreateUnbounded<string>();

    public void TriggerFeedProcessing(string feedName)
    {
        _channel.Writer.TryWrite(feedName);
    }

    public IAsyncEnumerable<string> ReadTriggersAsync(CancellationToken cancellationToken)
    {
        return _channel.Reader.ReadAllAsync(cancellationToken);
    }
}
