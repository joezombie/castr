using System.Collections.Concurrent;
using Microsoft.AspNetCore.SignalR;
using Castr.Hubs;

namespace Castr.Services;

/// <summary>
/// A single captured log entry, as exposed to the dashboard.
/// </summary>
public record LogEntry(
    DateTime Timestamp,
    LogLevel Level,
    string Category,
    string Message);

/// <summary>
/// Thread-safe, bounded in-memory ring buffer that retains the most recent log
/// entries captured by <see cref="LogBufferProvider"/>. Registered as a singleton
/// so all loggers and the dashboard share one buffer.
///
/// When a SignalR hub context is available, newly captured entries are pushed to
/// connected dashboard clients on the "LogEntry" event for live tailing.
/// </summary>
public class LogBufferService
{
    public const int Capacity = 1000;

    private readonly ConcurrentQueue<LogEntry> _entries = new();
    private long _count;
    private readonly object _trimLock = new();

    // Resolved lazily to avoid a DI cycle (the logging provider is built before
    // the SignalR services). May remain null in tests / if SignalR is absent.
    private IHubContext<LogStreamHub>? _hubContext;

    public void AttachHub(IHubContext<LogStreamHub> hubContext) => _hubContext = hubContext;

    /// <summary>
    /// Adds an entry to the ring buffer, evicting the oldest entries once the
    /// capacity is exceeded, then broadcasts it to live tail clients.
    /// </summary>
    public void Add(LogEntry entry)
    {
        _entries.Enqueue(entry);
        Interlocked.Increment(ref _count);

        // Trim back to capacity. Guarded so concurrent producers don't over-trim;
        // each dequeue is paired with an atomic decrement so the counter never drifts.
        if (Interlocked.Read(ref _count) > Capacity)
        {
            lock (_trimLock)
            {
                while (Interlocked.Read(ref _count) > Capacity && _entries.TryDequeue(out _))
                {
                    Interlocked.Decrement(ref _count);
                }
            }
        }

        // Fire-and-forget broadcast; never let logging failures bubble up.
        var hub = _hubContext;
        if (hub is not null)
        {
            _ = BroadcastAsync(hub, entry);
        }
    }

    private static async Task BroadcastAsync(IHubContext<LogStreamHub> hub, LogEntry entry)
    {
        try
        {
            await hub.Clients.All.SendAsync(
                "LogEntry",
                entry.Timestamp,
                (int)entry.Level,
                entry.Category,
                entry.Message);
        }
        catch
        {
            // Broadcasting log lines must not crash the app or recurse into logging.
        }
    }

    /// <summary>Returns a snapshot of the currently buffered entries, oldest first.</summary>
    public IReadOnlyList<LogEntry> Snapshot() => _entries.ToArray();
}
