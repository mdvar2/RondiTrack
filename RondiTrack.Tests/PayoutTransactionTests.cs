using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using RondiTrack.Data;
using RondiTrack.Models;
using RondiTrack.Services;

namespace RondiTrack.Tests;

public class PayoutTransactionTests
{
    [Fact]
    public async Task CreatePayoutAsync_PersistsPayoutAndMarksCyclePaidOut()
    {
        // Arrange
        var configuration =
            new ConfigurationBuilder()
                .AddUserSecrets<Program>()
                .Build();

        var connectionString =
            configuration.GetConnectionString(
                "RondiTrackDb")
            ?? throw new InvalidOperationException(
                "Connection string 'RondiTrackDb' was not found.");

        var options =
            new DbContextOptionsBuilder<RondiTrackDbContext>()
                .UseNpgsql(connectionString)
                .Options;

        var stokvelId = Guid.NewGuid();
        var userId = Guid.NewGuid();

        var cycle =
            new ContributionCycle(
                stokvelId,
                "2026-10",
                1000m);

        var member =
            new StokvelMember(
                stokvelId,
                userId);

        await using (var setupContext =
            new RondiTrackDbContext(options))
        {
            await setupContext.ContributionCycles.AddAsync(
                cycle);

            await setupContext.StokvelMembers.AddAsync(
                member);

            await setupContext.SaveChangesAsync();
        }

        // Act
        await using (var actContext =
            new RondiTrackDbContext(options))
        {
            var service =
                new PayoutService(actContext);

            await service.CreatePayoutAsync(
                stokvelId,
                cycle.Id,
                1000m);
        }

        // Assert by re-querying PostgreSQL
        await using var assertContext =
            new RondiTrackDbContext(options);

        var persistedPayout =
            await assertContext.Payouts
                .AsNoTracking()
                .FirstOrDefaultAsync(
                    payout =>
                        payout.ContributionCycleId ==
                            cycle.Id);

        var persistedCycle =
            await assertContext.ContributionCycles
                .AsNoTracking()
                .FirstOrDefaultAsync(
                    storedCycle =>
                        storedCycle.Id ==
                            cycle.Id);

        Assert.NotNull(persistedPayout);

        Assert.Equal(
            member.Id,
            persistedPayout.StokvelMemberId);

        Assert.Equal(
            1000m,
            persistedPayout.Amount);

        Assert.NotNull(persistedCycle);

        Assert.Equal(
            "PaidOut",
            persistedCycle.Status);
    }

    [Fact]
    public async Task PayoutTransaction_WhenFailureOccursPartway_RollsBackAllChanges()
    {
        // Arrange
        var configuration =
            new ConfigurationBuilder()
                .AddUserSecrets<Program>()
                .Build();

        var connectionString =
            configuration.GetConnectionString(
                "RondiTrackDb")
            ?? throw new InvalidOperationException(
                "Connection string 'RondiTrackDb' was not found.");

        var options =
            new DbContextOptionsBuilder<RondiTrackDbContext>()
                .UseNpgsql(connectionString)
                .Options;

        var stokvelId = Guid.NewGuid();
        var userId = Guid.NewGuid();

        var cycle =
            new ContributionCycle(
                stokvelId,
                "2026-11",
                1500m);

        var member =
            new StokvelMember(
                stokvelId,
                userId);

        await using (var setupContext =
            new RondiTrackDbContext(options))
        {
            await setupContext.ContributionCycles.AddAsync(
                cycle);

            await setupContext.StokvelMembers.AddAsync(
                member);

            await setupContext.SaveChangesAsync();
        }

        // Act - deliberately fail after the payout has been written
        await using (var actContext =
            new RondiTrackDbContext(options))
        {
            await using var transaction =
                await actContext.Database
                    .BeginTransactionAsync();

            try
            {
                var payout =
                    new Payout(
                        stokvelId,
                        member.Id,
                        cycle.Id,
                        1500m);

                await actContext.Payouts.AddAsync(
                    payout);

                await actContext.SaveChangesAsync();

                // Simulate a failure after the first write,
                // before the cycle can be marked as paid out.
                throw new InvalidOperationException(
                    "Simulated payout failure.");
            }
            catch (InvalidOperationException)
            {
                await transaction.RollbackAsync();
            }
        }

        // Assert by re-querying PostgreSQL with a fresh context
        await using var assertContext =
            new RondiTrackDbContext(options);

        var persistedPayout =
            await assertContext.Payouts
                .AsNoTracking()
                .FirstOrDefaultAsync(
                    payout =>
                        payout.ContributionCycleId ==
                            cycle.Id);

        var persistedCycle =
            await assertContext.ContributionCycles
                .AsNoTracking()
                .FirstOrDefaultAsync(
                    storedCycle =>
                        storedCycle.Id ==
                            cycle.Id);

        Assert.Null(persistedPayout);

        Assert.NotNull(persistedCycle);

        Assert.Equal(
            "Open",
            persistedCycle.Status);
    }
}