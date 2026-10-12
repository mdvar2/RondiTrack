using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;

namespace RondiTrack.Tests.TestSupport;

// Capture only correlation metadata, never formatted messages, exceptions or payloads.
public sealed class CorrelationLogProvider : ILoggerProvider
{
    public ConcurrentQueue<(string Category, string CorrelationId)> Entries { get; } = new();
    public ILogger CreateLogger(string categoryName) => new CorrelationLogger(this, categoryName);
    public void Dispose() { }

    private sealed class CorrelationLogger(CorrelationLogProvider provider, string category) : ILogger
    {
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => true;
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state,
            Exception? exception, Func<TState, Exception?, string> formatter)
        {
            if (state is not IEnumerable<KeyValuePair<string, object?>> properties) return;
            foreach (var property in properties)
                if (property.Key == "CorrelationId" && property.Value is string id)
                    provider.Entries.Enqueue((category, id));
        }
    }
}
