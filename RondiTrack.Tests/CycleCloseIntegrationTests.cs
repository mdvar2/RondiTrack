using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using RondiTrack.BackgroundJobs;
using RondiTrack.Domain.Entities;
using RondiTrack.Infrastructure.Data;
using RondiTrack.Observability;
using RondiTrack.Tests.TestSupport;

namespace RondiTrack.Tests;

[Collection(PostgresCollection.Name)]
public sealed class CycleCloseIntegrationTests(PostgresApiFactory factory)
{
    private sealed class FixedClock(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }

    private async Task<(Guid StokvelId, Guid CycleId, Guid UserId)> SeedAsync(
        DateOnly endDate, decimal due = 100.25m)
    {
        await using var db = new RondiTrackDbContext(factory.CreateDbContextOptions());
        var user = new User("Cycle test", $"cycle-{Guid.NewGuid()}@tests.example");
        var stokvel = new Stokvel($"Cycle test {Guid.NewGuid()}", due);
        var cycle = new ContributionCycle(stokvel.Id, "2026-10", due, endDate);
        db.Users.Add(user);
        db.Stokvels.Add(stokvel);
        db.StokvelMembers.Add(new StokvelMember(stokvel.Id, user.Id));
        db.ContributionCycles.Add(cycle);
        await db.SaveChangesAsync();
        return (stokvel.Id, cycle.Id, user.Id);
    }

    private async Task RunAsync(DateTimeOffset utc)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<RondiTrackDbContext>();
        var telemetry = scope.ServiceProvider.GetRequiredService<RondiTrackTelemetry>();
        var processor = new CycleCloseProcessor(db, new FixedClock(utc),
            NullLogger<CycleCloseProcessor>.Instance, telemetry);
        await processor.RunOnceAsync();
    }

    [Fact]
    public async Task JohannesburgMidnight_ClosesOnce_CreatesArrearsInCents_AndNextCycle()
    {
        var seeded = await SeedAsync(new DateOnly(2026, 10, 31));
        await RunAsync(new DateTimeOffset(2026, 10, 31, 21, 59, 0, TimeSpan.Zero));
        await using (var before = new RondiTrackDbContext(factory.CreateDbContextOptions()))
            Assert.Equal("Open", (await before.ContributionCycles.SingleAsync(x => x.Id == seeded.CycleId)).Status);

        await RunAsync(new DateTimeOffset(2026, 10, 31, 22, 0, 0, TimeSpan.Zero));
        await RunAsync(new DateTimeOffset(2026, 10, 31, 22, 0, 0, TimeSpan.Zero));
        await using var db = new RondiTrackDbContext(factory.CreateDbContextOptions());
        Assert.Equal("Closed", (await db.ContributionCycles.SingleAsync(x => x.Id == seeded.CycleId)).Status);
        var arrear = await db.ContributionArrears.SingleAsync(x => x.ContributionCycleId == seeded.CycleId);
        Assert.Equal(10025L, arrear.ShortfallCents);
        var next = await db.ContributionCycles.SingleAsync(x => x.StokvelId == seeded.StokvelId && x.Period == "2026-11");
        Assert.Equal(new DateOnly(2026, 11, 30), next.EndDate);
    }

    [Fact]
    public async Task FutureCycle_IsNotClosed()
    {
        var seeded = await SeedAsync(new DateOnly(2026, 11, 30));
        await RunAsync(new DateTimeOffset(2026, 10, 31, 22, 0, 0, TimeSpan.Zero));
        await using var db = new RondiTrackDbContext(factory.CreateDbContextOptions());
        Assert.Equal("Open", (await db.ContributionCycles.SingleAsync(x => x.Id == seeded.CycleId)).Status);
        Assert.False(await db.ContributionArrears.AnyAsync(x => x.ContributionCycleId == seeded.CycleId));
    }

    [Fact]
    public async Task ConcurrentProcessors_DoNotDuplicateArrears()
    {
        var seeded = await SeedAsync(new DateOnly(2026, 10, 31));
        var now = new DateTimeOffset(2026, 10, 31, 22, 0, 0, TimeSpan.Zero);
        await Task.WhenAll(RunAsync(now), RunAsync(now));
        await using var db = new RondiTrackDbContext(factory.CreateDbContextOptions());
        Assert.Equal(1, await db.ContributionArrears.CountAsync(x => x.ContributionCycleId == seeded.CycleId));
    }
}
