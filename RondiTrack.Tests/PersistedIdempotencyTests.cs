using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using RondiTrack.BackgroundJobs;
using RondiTrack.DTOs.Contributions;
using RondiTrack.Idempotency;
using RondiTrack.Infrastructure.Data;
using RondiTrack.Tests.TestSupport;

namespace RondiTrack.Tests;

[Collection(PostgresCollection.Name)]
public sealed class PersistedIdempotencyTests(PostgresApiFactory factory)
{
    private sealed class FixedClock(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }

    [Fact]
    public async Task ResponseSurvivesNewScope_AndExpiresOnlyAfterRetention()
    {
        var key = "durable-" + Guid.NewGuid().ToString("N");
        var now = new DateTimeOffset(2026, 10, 19, 0, 0, 0, TimeSpan.Zero);
        var response = new ContributionResponse(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            125m, now.UtcDateTime, 1u);

        await using (var db = new RondiTrackDbContext(factory.CreateDbContextOptions()))
        {
            var store = new PostgresIdempotencyStore(db, new FixedClock(now));
            await store.SaveAsync(new IdempotencyRecord(key, "hash", 201, response));
        }

        await using (var db = new RondiTrackDbContext(factory.CreateDbContextOptions()))
        {
            var store = new PostgresIdempotencyStore(db, new FixedClock(now.AddHours(23)));
            var restored = await store.GetAsync(key);
            Assert.NotNull(restored);
            Assert.Equal(response, restored.ResponseBody);
            var cleanup = new HousekeepingProcessor(db, new FixedClock(now.AddHours(23)),
                Options.Create(new HousekeepingOptions { IdempotencyRetentionHours = 24 }),
                NullLogger<HousekeepingProcessor>.Instance);
            await cleanup.RunOnceAsync();
            Assert.NotNull(await store.GetAsync(key));
        }

        await using (var db = new RondiTrackDbContext(factory.CreateDbContextOptions()))
        {
            var cleanup = new HousekeepingProcessor(db, new FixedClock(now.AddHours(25)),
                Options.Create(new HousekeepingOptions { IdempotencyRetentionHours = 24 }),
                NullLogger<HousekeepingProcessor>.Instance);
            await cleanup.RunOnceAsync();
            var store = new PostgresIdempotencyStore(db, new FixedClock(now.AddHours(25)));
            Assert.Null(await store.GetAsync(key));
        }
    }
}
