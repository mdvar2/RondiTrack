namespace RondiTrack.Domain.Entities;

/// <summary>
/// A phone's push address: a Firebase installation id, owned by one member.
/// When someone else signs in on the same phone and registers it, the row
/// moves to them, so a shared phone is told only the news of whoever is
/// signed in now, even if the last sign-out never reached the API.
/// </summary>
public class DeviceInstallation
{
    public const int MaxInstallationIdLength = 255;

    public Guid Id { get; private set; }

    public string InstallationId { get; private set; } = null!;

    public Guid UserId { get; private set; }

    public DateTime RegisteredAtUtc { get; private set; }

    private DeviceInstallation()
    {
    }

    public DeviceInstallation(string installationId, Guid userId, DateTime nowUtc)
    {
        if (string.IsNullOrWhiteSpace(installationId))
            throw new ArgumentException("Installation ID is required.");
        if (userId == Guid.Empty)
            throw new ArgumentException("User ID is required.");

        Id = Guid.NewGuid();
        InstallationId = installationId;
        UserId = userId;
        RegisteredAtUtc = nowUtc;
    }

    /// <summary>The phone is now signed in as <paramref name="userId"/>.</summary>
    public void MoveTo(Guid userId, DateTime nowUtc)
    {
        if (userId == Guid.Empty)
            throw new ArgumentException("User ID is required.");

        UserId = userId;
        RegisteredAtUtc = nowUtc;
    }
}
