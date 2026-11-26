using FirebaseAdmin;
using FirebaseAdmin.Messaging;
using Google.Apis.Auth.OAuth2;

namespace RondiTrack.Push;

/// <summary>
/// Used when no service account key is configured: logs how many phones
/// would have been told, and never what.
/// </summary>
public sealed class LoggingPushSender(ILogger<LoggingPushSender> logger) : IPushSender
{
    public Task<PushResult> SendAsync(
        IReadOnlyList<string> installationIds,
        PushMessage message,
        CancellationToken cancellationToken)
    {
        logger.LogInformation(
            "Push not configured: a {PushType} push to {Count} phone(s) was not sent.",
            message.Data.GetValueOrDefault("type"),
            installationIds.Count);
        return Task.FromResult(PushResult.Nothing);
    }
}

/// <summary>
/// Firebase Cloud Messaging through the Admin SDK, addressed by Firebase
/// installation id (<see cref="Message.Fid"/>), on the app's own channel.
/// </summary>
public sealed class FirebasePushSender : IPushSender
{
    /// <summary>The app's channel: private on the lock screen.</summary>
    public const string ChannelId = "stokvel_updates";

    private readonly FirebaseMessaging _messaging;
    private readonly ILogger<FirebasePushSender> _logger;

    public FirebasePushSender(string serviceAccountPath, ILogger<FirebasePushSender> logger)
    {
        var app = FirebaseApp.GetInstance("ronditrack-push") ?? FirebaseApp.Create(
            new AppOptions
            {
                Credential = CredentialFactory
                    .FromFile<ServiceAccountCredential>(serviceAccountPath)
                    .ToGoogleCredential(),
            },
            "ronditrack-push");
        _messaging = FirebaseMessaging.GetMessaging(app);
        _logger = logger;
    }

    public async Task<PushResult> SendAsync(
        IReadOnlyList<string> installationIds,
        PushMessage message,
        CancellationToken cancellationToken)
    {
        if (installationIds.Count == 0)
            return PushResult.Nothing;

        var now = DateTime.UtcNow;
        var messages = installationIds.Select(id => BuildMessage(id, message, now)).ToList();

        var response = await _messaging.SendEachAsync(messages, cancellationToken);
        var gone = new List<string>();
        for (var i = 0; i < response.Responses.Count; i++)
        {
            var result = response.Responses[i];
            if (result.IsSuccess)
                continue;
            if (result.Exception?.MessagingErrorCode is MessagingErrorCode.Unregistered
                or MessagingErrorCode.SenderIdMismatch)
            {
                gone.Add(installationIds[i]);
            }
            else
            {
                _logger.LogWarning(
                    "A push failed with {ErrorCode}.",
                    result.Exception?.MessagingErrorCode);
            }
        }

        return new PushResult(gone);
    }

    /// <summary>
    /// One push to one installation, as Firebase receives it: on the app's
    /// channel, private on the lock screen, timed now.
    /// </summary>
    public static Message BuildMessage(string installationId, PushMessage message, DateTime nowUtc) => new()
    {
        Fid = installationId,
        Notification = new Notification { Title = message.Title, Body = message.Body },
        Data = message.Data,
        Android = new AndroidConfig
        {
            Priority = Priority.High,
            Notification = new AndroidNotification
            {
                ChannelId = ChannelId,
                Visibility = NotificationVisibility.PRIVATE,
                // Without it the Admin SDK sends .NET's default date (year 1),
                // which a phone shows as "1/01/03".
                EventTimestamp = nowUtc,
            },
        },
    };
}
