using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using RondiTrack.Infrastructure.Data;

namespace RondiTrack.BackgroundJobs;

public sealed class HousekeepingProcessor(
    RondiTrackDbContext db,
    TimeProvider clock,
    IOptions<HousekeepingOptions> options,
    ILogger<HousekeepingProcessor> logger)
{
    public async Task RunOnceAsync(CancellationToken cancellationToken = default)
    {
        var now = clock.GetUtcNow().UtcDateTime;
        var cutoff = now.AddDays(-options.Value.RefreshTokenGraceDays);

        // Keep expired token hashes for a grace period to aid replay investigations.
        // Remove expired families as well as their tokens (cascade), using set-based SQL.
        var removed = await db.RefreshTokenFamilies
            .Where(family => family.ExpiresAtUtc < cutoff)
            .ExecuteDeleteAsync(cancellationToken);

        // The current idempotency store is in-memory and scoped, not persisted.
        // It has no database rows for ExecuteDeleteAsync to remove.
        logger.LogInformation("Expired refresh-token families removed: {Count}", removed);
    }
}
