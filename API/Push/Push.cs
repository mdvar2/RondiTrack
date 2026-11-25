namespace RondiTrack.Push;

/// <summary>
/// One push, as it leaves the API. The lock screen, Google's servers and the
/// phone's logs can read every field, so it carries nothing an id doesn't
/// already say (README "Notifications").
/// </summary>
public sealed record PushMessage(
    string Title,
    string Body,
    IReadOnlyDictionary<string, string> Data);

/// <summary>What a send came to: the ids Firebase says are gone for good.</summary>
public sealed record PushResult(IReadOnlyList<string> GoneInstallationIds)
{
    public static readonly PushResult Nothing = new([]);
}

/// <summary>
/// Sends pushes. Firebase's Admin SDK when a service account key is
/// configured; otherwise a sender that only logs, so everyone can run the
/// API without a Firebase project.
/// </summary>
public interface IPushSender
{
    Task<PushResult> SendAsync(
        IReadOnlyList<string> installationIds,
        PushMessage message,
        CancellationToken cancellationToken);
}

/// <summary>Where the service account key lives (never in the repository).</summary>
public sealed class PushOptions
{
    public const string Section = "Push";

    /// <summary>
    /// The path of the Firebase service account JSON, mounted read-only.
    /// Empty: push is logged, not sent.
    /// </summary>
    public string? ServiceAccountPath { get; set; }
}

/// <summary>The one push the API sends today, and every field of it.</summary>
public static class PushMessages
{
    public const string ContributionUpdateType = "contribution_update";

    /// <summary>
    /// "There's an update on your stokvel": no name, no amount, no stokvel
    /// name, and no decision ("confirmed" is a decision). The data is a type
    /// and two ids; the details are fetched after the tap, through the same
    /// rules as every other request.
    /// </summary>
    public static PushMessage ContributionUpdate(Guid stokvelId, Guid cycleId) => new(
        "RondiTrack",
        "There's an update on your stokvel.",
        new Dictionary<string, string>
        {
            ["type"] = ContributionUpdateType,
            ["stokvelId"] = stokvelId.ToString(),
            ["cycleId"] = cycleId.ToString(),
        });
}
