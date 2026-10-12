using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;

namespace RondiTrack.Tests.TestSupport;

public sealed record CapturedLog(string Category, LogLevel Level, string Message,
    IReadOnlyDictionary<string, object?> Properties, Exception? Exception,
    IReadOnlyList<CapturedScope> Scopes);

public sealed record CapturedScope(string Rendered, IReadOnlyDictionary<string, object?> Properties);

// In-memory test evidence only. Never serialize raw log entries into documentation.
public sealed class CapturedLogProvider : ILoggerProvider, ISupportExternalScope
{
    private IExternalScopeProvider _scopes = new LoggerExternalScopeProvider();
    public ConcurrentQueue<CapturedLog> Entries { get; } = new();
    public ILogger CreateLogger(string categoryName) => new CaptureLogger(this, categoryName);
    public void SetScopeProvider(IExternalScopeProvider scopeProvider) => _scopes = scopeProvider;
    public void Dispose() { }

    private sealed class CaptureLogger(CapturedLogProvider provider, string category) : ILogger
    {
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => provider._scopes.Push(state);
        public bool IsEnabled(LogLevel logLevel) => true;
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state,
            Exception? exception, Func<TState, Exception?, string> formatter)
        {
            var properties = state is IEnumerable<KeyValuePair<string, object?>> values
                ? values.ToDictionary(pair => pair.Key, pair => pair.Value)
                : new Dictionary<string, object?>();
            var scopes = new List<CapturedScope>();
            provider._scopes.ForEachScope((scope, captured) =>
            {
                var scopeProperties = scope is IEnumerable<KeyValuePair<string, object?>> scopeValues
                    ? scopeValues.ToDictionary(pair => pair.Key, pair => pair.Value)
                    : new Dictionary<string, object?>();
                captured.Add(new(scope?.ToString() ?? string.Empty, scopeProperties));
            }, scopes);
            provider.Entries.Enqueue(new(category, logLevel, formatter(state, exception), properties, exception, scopes));
        }
    }
}
