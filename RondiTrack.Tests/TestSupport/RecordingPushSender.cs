using System.Collections.Concurrent;
using RondiTrack.Push;

namespace RondiTrack.Tests.TestSupport;

/// <summary>
/// Keeps every push instead of sending it, so tests prove who is told and
/// what with no Firebase project. Each test uses installation ids of its
/// own, and reads only those.
/// </summary>
public sealed class RecordingPushSender : IPushSender
{
    public sealed record Sent(string InstallationId, PushMessage Message);

    private readonly ConcurrentQueue<Sent> _sent = new();

    /// <summary>Ids Firebase would report as gone for good.</summary>
    public ConcurrentDictionary<string, bool> Gone { get; } = new();

    /// <summary>Ids whose send throws, as a Firebase outage would.</summary>
    public ConcurrentDictionary<string, bool> Failing { get; } = new();

    /// <summary>Called at the moment of sending, before anything is kept.</summary>
    public Func<IReadOnlyList<string>, Task>? OnSend { get; set; }

    public IReadOnlyList<Sent> To(params string[] installationIds) =>
        _sent.Where(s => installationIds.Contains(s.InstallationId)).ToList();

    public async Task<PushResult> SendAsync(
        IReadOnlyList<string> installationIds,
        PushMessage message,
        CancellationToken cancellationToken)
    {
        if (OnSend is { } onSend)
            await onSend(installationIds);
        if (installationIds.Any(Failing.ContainsKey))
            throw new InvalidOperationException("Firebase is down.");
        foreach (var id in installationIds)
            _sent.Enqueue(new Sent(id, message));
        return new PushResult(installationIds.Where(Gone.ContainsKey).ToList());
    }
}
