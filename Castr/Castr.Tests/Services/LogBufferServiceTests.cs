namespace Castr.Tests.Services;

public class LogBufferServiceTests
{
    private static LogEntry Entry(string message, LogLevel level = LogLevel.Information, string category = "Castr.Services.Test")
        => new(DateTime.UtcNow, level, category, message);

    [Fact]
    public void Add_ThenSnapshot_ReturnsEntryWithLevelAndCategory()
    {
        var buffer = new LogBufferService();

        buffer.Add(new LogEntry(DateTime.UtcNow, LogLevel.Warning, "Castr.Services.YouTubeDownloadService", "hello"));

        var snapshot = buffer.Snapshot();
        Assert.Single(snapshot);
        Assert.Equal(LogLevel.Warning, snapshot[0].Level);
        Assert.Equal("Castr.Services.YouTubeDownloadService", snapshot[0].Category);
        Assert.Equal("hello", snapshot[0].Message);
    }

    [Fact]
    public void Buffer_IsBounded_ToCapacity()
    {
        var buffer = new LogBufferService();

        for (int i = 0; i < LogBufferService.Capacity + 250; i++)
        {
            buffer.Add(Entry($"line {i}"));
        }

        var snapshot = buffer.Snapshot();
        Assert.Equal(LogBufferService.Capacity, snapshot.Count);

        // Oldest entries evicted; newest retained (oldest-first ordering).
        Assert.Equal("line 250", snapshot[0].Message);
        Assert.Equal($"line {LogBufferService.Capacity + 249}", snapshot[^1].Message);
    }

    [Fact]
    public async Task Add_IsThreadSafe_AndStaysBounded()
    {
        var buffer = new LogBufferService();
        const int producers = 8;
        const int perProducer = 1000;

        var tasks = Enumerable.Range(0, producers).Select(p => Task.Run(() =>
        {
            for (int i = 0; i < perProducer; i++)
            {
                buffer.Add(Entry($"p{p}-{i}"));
            }
        }));

        await Task.WhenAll(tasks);

        var snapshot = buffer.Snapshot();
        Assert.Equal(LogBufferService.Capacity, snapshot.Count);
    }
}
