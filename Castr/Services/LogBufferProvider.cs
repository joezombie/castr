using System.Collections.Concurrent;

namespace Castr.Services;

/// <summary>
/// Custom <see cref="ILoggerProvider"/> that captures rendered log entries into a
/// shared <see cref="LogBufferService"/> ring buffer for display in the dashboard
/// Logs panel.
///
/// A minimum level of <see cref="LogLevel.Information"/> is enforced so that the
/// extremely noisy EF Core <c>Debug</c> SQL output does not flood the buffer.
/// </summary>
[ProviderAlias("LogBuffer")]
public sealed class LogBufferProvider : ILoggerProvider
{
    private readonly LogBufferService _buffer;
    private readonly LogLevel _minLevel;
    private readonly ConcurrentDictionary<string, BufferLogger> _loggers = new();

    public LogBufferProvider(LogBufferService buffer, LogLevel minLevel = LogLevel.Information)
    {
        _buffer = buffer;
        _minLevel = minLevel;
    }

    // EF Core emits very high-volume SQL command logging at Information level
    // (in addition to the Debug SQL spam). Drop those categories so the dashboard
    // Logs panel shows meaningful application/HTTP logs rather than raw SQL.
    private static readonly string[] ExcludedCategoryPrefixes =
    {
        "Microsoft.EntityFrameworkCore.Database",
        "Microsoft.EntityFrameworkCore.Infrastructure",
    };

    public ILogger CreateLogger(string categoryName)
    {
        var effectiveMin = IsExcluded(categoryName) ? LogLevel.Warning : _minLevel;
        return _loggers.GetOrAdd(categoryName, name => new BufferLogger(name, _buffer, effectiveMin));
    }

    private static bool IsExcluded(string category)
    {
        foreach (var prefix in ExcludedCategoryPrefixes)
        {
            if (category.StartsWith(prefix, StringComparison.Ordinal))
            {
                return true;
            }
        }
        return false;
    }

    public void Dispose() => _loggers.Clear();

    private sealed class BufferLogger : ILogger
    {
        private readonly string _category;
        private readonly LogBufferService _buffer;
        private readonly LogLevel _minLevel;

        public BufferLogger(string category, LogBufferService buffer, LogLevel minLevel)
        {
            _category = category;
            _buffer = buffer;
            _minLevel = minLevel;
        }

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) =>
            logLevel != LogLevel.None && logLevel >= _minLevel;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            if (!IsEnabled(logLevel))
            {
                return;
            }

            var message = formatter(state, exception);
            if (exception is not null)
            {
                message = $"{message} {exception.GetType().Name}: {exception.Message}";
            }

            _buffer.Add(new LogEntry(DateTime.UtcNow, logLevel, _category, message));
        }
    }
}
