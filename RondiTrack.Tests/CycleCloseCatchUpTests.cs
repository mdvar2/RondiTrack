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
public sealed class CycleCloseCatchUpTests(PostgresApiFactory factory)
{
    private sealed class FixedClock(DateTimeOffset utc) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => utc;
    }

    [Fact]
    public async Task ThreeMissedMonths_CloseInOneRun_AndDoNotDuplicateOnRetry()
    {
        Guid stokvelId;
        await using (var db = new RondiTrackDbContext(factory.CreateDbContextOptions()))
        {
            var member = new User("Catch-up member", $"catchup-{Guid.NewGuid()}@tests.example");
            var stokvel = new Stokvel($"Catch-up {Guid.NewGuid()}", 150m);
            stokvelId = stokvel.Id;
            db.Users.Add(member);
            db.Stokvels.Add(stokvel);
            db.StokvelMembers.Add(new StokvelMember(stokvel.Id, member.Id));
            db.ContributionCycles.Add(new ContributionCycle(
                stokvel.Id, "2026-01", 150m, new DateOnly(2026, 1, 31)));
            await db.SaveChangesAsync();
        }

        async Task Run()
        {
            using var scope = factory.Services.CreateScope();
            var processor = new CycleCloseProcessor(
                scope.ServiceProvider.GetRequiredService<RondiTrackDbContext>(),
                new FixedClock(new DateTimeOffset(2026, 4, 1, 0, 0, 0, TimeSpan.Zero)),
                NullLogger<CycleCloseProcessor>.Instance,
                scope.ServiceProvider.GetRequiredService<RondiTrackTelemetry>());
            await processor.RunOnceAsync();
        }

        await Run();
        await Run();

        await using var check = new RondiTrackDbContext(factory.CreateDbContextOptions());
        var cycles = await check.ContributionCycles
            .Where(c => c.StokvelId == stokvelId)
            .OrderBy(c => c.Period).ToListAsync();
        Assert.Equal(4, cycles.Count);
        Assert.Equal(new[] { "2026-01", "2026-02", "2026-03", "2026-04" },
            cycles.Select(c => c.Period));
        Assert.Equal(3, cycles.Count(c => c.Status == "Closed"));
        Assert.Equal("Open", cycles[^1].Status);
        var arrears = await check.ContributionArrears
            .Where(a => a.StokvelId == stokvelId).ToListAsync();
        Assert.Equal(3, arrears.Count);
        Assert.All(arrears, a => Assert.Equal(15000L, a.ShortfallCents));
    }
}
