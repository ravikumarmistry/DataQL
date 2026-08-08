using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;

namespace DataQL.SqlServer.Tests.Infrastructure;

/// <summary>
/// In-memory logger provider used by SqlServer e2e harness to exercise query logging.
/// </summary>
internal sealed class CollectingLoggerProvider : ILoggerProvider
{
    private readonly ConcurrentQueue<string> _messages = new();

    public IReadOnlyList<string> Messages => _messages.ToArray();

    public ILogger CreateLogger(string categoryName) => new CollectingLogger(categoryName, _messages);

    public void Dispose()
    {
    }

    private sealed class CollectingLogger(
        string categoryName,
        ConcurrentQueue<string> messages) : ILogger
    {
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => logLevel >= LogLevel.Information;

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

            messages.Enqueue($"{categoryName}|{formatter(state, exception)}");
        }
    }
}
