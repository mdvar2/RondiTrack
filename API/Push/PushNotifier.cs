using Microsoft.EntityFrameworkCore;
using RondiTrack.Infrastructure.Data;

namespace RondiTrack.Push;

/// <summary>
/// Tells a member's phones that something changed. Called only after the
/// change is saved, and never fails the request that saved it: a push that
/// fails is logged, and ids Firebase reports as gone for good are deleted.
/// </summary>
public sealed class PushNotifier(
    RondiTrackDbContext db,
    IPushSender sender,
    ILogger<PushNotifier> logger)
{
    public async Task ContributionUpdatedAsync(
        Guid memberId,
        Guid stokvelId,
        Guid cycleId,
        CancellationToken cancellationToken)
    {
        try
        {
            // The API decides who is told, from its own data: the phones
            // registered to this member now.
            var phones = await db.DeviceInstallations
                .Where(d => d.UserId == memberId)
                .Select(d => d.InstallationId)
                .ToListAsync(cancellationToken);
            if (phones.Count == 0)
                return;

            var result = await sender.SendAsync(
                phones,
                PushMessages.ContributionUpdate(stokvelId, cycleId),
                cancellationToken);

            if (result.GoneInstallationIds.Count > 0)
            {
                await db.DeviceInstallations
                    .Where(d => result.GoneInstallationIds.Contains(d.InstallationId))
                    .ExecuteDeleteAsync(cancellationToken);
            }
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            logger.LogWarning(exception, "A contribution push failed; the change itself is saved.");
        }
    }
}
