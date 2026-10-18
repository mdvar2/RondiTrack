using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using RondiTrack.BackgroundJobs;
using RondiTrack.Domain.Entities;
using RondiTrack.Infrastructure.Data;
using RondiTrack.Tests.TestSupport;

namespace RondiTrack.Tests;

[Collection(PostgresCollection.Name)]
public sealed class HousekeepingRetentionTests(PostgresApiFactory factory)
{
    private sealed class FixedClock(DateTimeOffset utc) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => utc;
    }

    [Fact]
    public async Task DeletesOnlyFamiliesBeyondGrace_AndCascadesTokens()
    {
        var now = new DateTime(2026, 10, 19, 0, 0, 0, DateTimeKind.Utc);
        Guid expiredId, retainedId;
        await using (var db = new RondiTrackDbContext(factory.CreateDbContextOptions()))
        {
            var user = new User("Housekeeping member", $"retention-{Guid.NewGuid()}@tests.example");
            db.Users.Add(user);
            // Expiry is creation plus 30 days. The first expired more than seven days ago.
            var expired = new RefreshTokenFamily(user.Id, now.AddDays(-40));
            var retained = new RefreshTokenFamily(user.Id, now.AddDays(-34));
            expiredId = expired.Id;
            retainedId = retained.Id;
            db.RefreshTokenFamilies.AddRange(expired, retained);
            db.RefreshTokens.Add(new RefreshToken(expired.Id, Guid.NewGuid().ToString("N"), now.AddDays(-40), now.AddDays(-10)));
            db.RefreshTokens.Add(new RefreshToken(retained.Id, Guid.NewGuid().ToString("N"), now.AddDays(-34), now.AddDays(-4)));
            await db.SaveChangesAsync();
        }

        await using (var db = new RondiTrackDbContext(factory.CreateDbContextOptions()))
        {
            var processor = new HousekeepingProcessor(
                db, new FixedClock(new DateTimeOffset(now)),
                Options.Create(new HousekeepingOptions { RefreshTokenGraceDays = 7 }),
                NullLogger<HousekeepingProcessor>.Instance);
            await processor.RunOnceAsync();
            await processor.RunOnceAsync();
        }

        await using var verify = new RondiTrackDbContext(factory.CreateDbContextOptions());
        Assert.False(await verify.RefreshTokenFamilies.AnyAsync(f => f.Id == expiredId));
        Assert.False(await verify.RefreshTokens.AnyAsync(t => t.FamilyId == expiredId));
        Assert.True(await verify.RefreshTokenFamilies.AnyAsync(f => f.Id == retainedId));
        Assert.True(await verify.RefreshTokens.AnyAsync(t => t.FamilyId == retainedId));
    }
}
