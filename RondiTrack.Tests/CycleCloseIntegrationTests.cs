
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
        DateOnly endDate,
        decimal due = 100.25m)
    {
        await using var db = new RondiTrackDbContext(
            factory.CreateDbContextOptions());

        var user = new User(
            "Cycle test",
            $"cycle-{Guid.NewGuid()}@tests.example");

        var stokvel = new Stokvel(
            $"Cycle test {Guid.NewGuid()}",
            due);

        var cycle = new ContributionCycle(
            stokvel.Id,
            "2026-10",
            due,
            endDate);

        db.Users.Add(user);
        db.Stokvels.Add(stokvel);

        db.StokvelMembers.Add(
            new StokvelMember(stokvel.Id, user.Id));

        db.ContributionCycles.Add(cycle);

        await db.SaveChangesAsync();

        return (stokvel.Id, cycle.Id, user.Id);
    }

    private async Task RunAsync(DateTimeOffset utc)
    {
        using var scope = factory.Services.CreateScope();

        var db = scope.ServiceProvider
            .GetRequiredService<RondiTrackDbContext>();

        var telemetry = scope.ServiceProvider
            .GetRequiredService<RondiTrackTelemetry>();

        var processor = new CycleCloseProcessor(
            db,
            new FixedClock(utc),
            NullLogger<CycleCloseProcessor>.Instance,
            telemetry);

        await processor.RunOnceAsync();
    }

    // =========================================================
    // TEST 1: JOHANNESBURG MIDNIGHT BOUNDARY
    // =========================================================

    [Fact]
    public async Task JohannesburgMidnight_ClosesOnce_CreatesArrearsInCents_AndNextCycle()
    {
        var seeded = await SeedAsync(
            new DateOnly(2026, 10, 31));

        // 23:59 SAST: cycle must still be open.
        await RunAsync(new DateTimeOffset(
            2026, 10, 31, 21, 59, 0, TimeSpan.Zero));

        await using (var before = new RondiTrackDbContext(
            factory.CreateDbContextOptions()))
        {
            var cycle = await before.ContributionCycles
                .SingleAsync(x => x.Id == seeded.CycleId);

            Assert.Equal("Open", cycle.Status);
        }

        // 00:00 SAST on 1 November: cycle becomes overdue.
        var midnight = new DateTimeOffset(
            2026, 10, 31, 22, 0, 0, TimeSpan.Zero);

        await RunAsync(midnight);

        // Repeating the job must not create duplicates.
        await RunAsync(midnight);

        await using var db = new RondiTrackDbContext(
            factory.CreateDbContextOptions());

        var closedCycle = await db.ContributionCycles
            .SingleAsync(x => x.Id == seeded.CycleId);

        Assert.Equal("Closed", closedCycle.Status);

        var arrear = await db.ContributionArrears
            .SingleAsync(x =>
                x.ContributionCycleId == seeded.CycleId);

        Assert.Equal(10025L, arrear.ShortfallCents);

        var next = await db.ContributionCycles
            .SingleAsync(x =>
                x.StokvelId == seeded.StokvelId &&
                x.Period == "2026-11");

        Assert.Equal(
            new DateOnly(2026, 11, 30),
            next.EndDate);

        Assert.Equal("Open", next.Status);
    }

    // =========================================================
    // TEST 2: FUTURE CYCLES MUST REMAIN OPEN
    // =========================================================

    [Fact]
    public async Task FutureCycle_IsNotClosed()
    {
        var seeded = await SeedAsync(
            new DateOnly(2026, 11, 30));

        await RunAsync(new DateTimeOffset(
            2026, 10, 31, 22, 0, 0, TimeSpan.Zero));

        await using var db = new RondiTrackDbContext(
            factory.CreateDbContextOptions());

        var cycle = await db.ContributionCycles
            .SingleAsync(x => x.Id == seeded.CycleId);

        Assert.Equal("Open", cycle.Status);

        Assert.False(
            await db.ContributionArrears.AnyAsync(
                x => x.ContributionCycleId == seeded.CycleId));
    }

    // =========================================================
    // TEST 3: CONCURRENT PROCESSORS MUST NOT DUPLICATE ARREARS
    // =========================================================

    [Fact]
    public async Task ConcurrentProcessors_DoNotDuplicateArrears()
    {
        var seeded = await SeedAsync(
            new DateOnly(2026, 10, 31));

        var now = new DateTimeOffset(
            2026, 10, 31, 22, 0, 0, TimeSpan.Zero);

        // Simulate two background processors running together.
        await Task.WhenAll(
            RunAsync(now),
            RunAsync(now));

        await using var db = new RondiTrackDbContext(
            factory.CreateDbContextOptions());

        Assert.Equal(
            1,
            await db.ContributionArrears.CountAsync(
                x => x.ContributionCycleId == seeded.CycleId));

        var cycle = await db.ContributionCycles
            .SingleAsync(x => x.Id == seeded.CycleId);

        Assert.Equal("Closed", cycle.Status);

        Assert.Equal(
            1,
            await db.ContributionCycles.CountAsync(
                x => x.StokvelId == seeded.StokvelId &&
                     x.Period == "2026-11"));
    }

    // =========================================================
    // TEST 4: THREE OVERDUE CYCLES MUST CATCH UP
    // =========================================================

    [Fact]
    public async Task ThreeOverdueCycles_CatchUpWithoutDuplicatingArrears()
    {
        var seeded = await SeedAsync(
            new DateOnly(2026, 10, 31));

        // 00:00 SAST on 1 January 2027.
        // October, November and December are overdue.
        var now = new DateTimeOffset(
            2026, 12, 31, 22, 0, 0, TimeSpan.Zero);

        await RunAsync(now);

        await using (var db = new RondiTrackDbContext(
            factory.CreateDbContextOptions()))
        {
            var cycles = await db.ContributionCycles
                .Where(c => c.StokvelId == seeded.StokvelId)
                .OrderBy(c => c.EndDate)
                .ToListAsync();

            // Three closed cycles and one new open cycle.
            Assert.Equal(4, cycles.Count);

            Assert.Equal(
                new[]
                {
                    "2026-10",
                    "2026-11",
                    "2026-12",
                    "2027-01"
                },
                cycles.Select(c => c.Period));

            Assert.All(
                cycles.Take(3),
                c => Assert.Equal("Closed", c.Status));

            Assert.Equal("Open", cycles[3].Status);

            Assert.Equal(
                new DateOnly(2027, 1, 31),
                cycles[3].EndDate);

            // One unpaid member across three overdue cycles.
            var arrears = await db.ContributionArrears
                .Where(a => a.StokvelId == seeded.StokvelId)
                .ToListAsync();

            Assert.Equal(3, arrears.Count);

            Assert.All(
                arrears,
                a => Assert.Equal(10025L, a.ShortfallCents));

            Assert.Equal(
                3,
                arrears.Select(a => a.ContributionCycleId)
                    .Distinct()
                    .Count());
        }

        // A second execution must not duplicate financial records.
        await RunAsync(now);

        await using var verification = new RondiTrackDbContext(
            factory.CreateDbContextOptions());

        Assert.Equal(
            4,
            await verification.ContributionCycles.CountAsync(
                c => c.StokvelId == seeded.StokvelId));

        Assert.Equal(
            3,
            await verification.ContributionArrears.CountAsync(
                a => a.StokvelId == seeded.StokvelId));
    }

    // =========================================================
    // TEST 5: FAILURE IN ONE STOKVEL MUST NOT BLOCK ANOTHER
    // =========================================================

    [Fact]
    public async Task InvalidStokvel_DoesNotPreventAnotherStokvelClosing()
    {
        var bad = await SeedAsync(
            new DateOnly(2026, 10, 31));

        var good = await SeedAsync(
            new DateOnly(2026, 10, 31));

        // Create an existing arrears record while the
        // first stokvel's contribution cycle remains open.
        // Its processing will encounter a uniqueness violation.
        await using (var db = new RondiTrackDbContext(
            factory.CreateDbContextOptions()))
        {
            db.ContributionArrears.Add(
                new ContributionArrear(
                    bad.StokvelId,
                    bad.UserId,
                    bad.CycleId,
                    10025L));

            await db.SaveChangesAsync();
        }

        var now = new DateTimeOffset(
            2026, 10, 31, 22, 0, 0, TimeSpan.Zero);

        // The worker should handle the bad stokvel's failure
        // and continue processing the good stokvel.
        await RunAsync(now);

        await using var verification = new RondiTrackDbContext(
            factory.CreateDbContextOptions());

        var badCycle = await verification.ContributionCycles
            .SingleAsync(c => c.Id == bad.CycleId);

        var goodCycle = await verification.ContributionCycles
            .SingleAsync(c => c.Id == good.CycleId);

        // The failed stokvel must not be partially closed.
        Assert.Equal("Open", badCycle.Status);

        // The valid stokvel must close successfully.
        Assert.Equal("Closed", goodCycle.Status);

        Assert.Equal(
            1,
            await verification.ContributionArrears.CountAsync(
                a => a.StokvelId == bad.StokvelId));

        Assert.Equal(
            1,
            await verification.ContributionArrears.CountAsync(
                a => a.StokvelId == good.StokvelId));

        // The valid stokvel must receive its next cycle.
        Assert.True(
            await verification.ContributionCycles.AnyAsync(
                c => c.StokvelId == good.StokvelId &&
                     c.Period == "2026-11"));

        // The failed stokvel must not create a successor.
        Assert.False(
            await verification.ContributionCycles.AnyAsync(
                c => c.StokvelId == bad.StokvelId &&
                     c.Period == "2026-11"));
    }
}
