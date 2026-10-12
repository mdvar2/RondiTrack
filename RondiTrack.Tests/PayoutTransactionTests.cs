using RondiTrack.Tests.TestSupport;
using Microsoft.EntityFrameworkCore;
using RondiTrack.Infrastructure.Data;
using RondiTrack.Domain.Entities;
using RondiTrack.Services;
using Microsoft.Extensions.DependencyInjection;
using RondiTrack.Observability;

namespace RondiTrack.Tests;

[Collection(PostgresCollection.Name)]
public class PayoutTransactionTests
{
    private readonly PostgresApiFactory _factory;

    public PayoutTransactionTests(PostgresApiFactory factory) => _factory = factory;

    [Fact]
    public async Task CreatePayoutAsync_PersistsPayoutAndMarksCyclePaidOut()
    {
        // Arrange
        var options = _factory.CreateDbContextOptions();

        var user =
            new User(
                $"Payout User {Guid.NewGuid()}",
                $"{Guid.NewGuid()}@example.com");

        var stokvel =
            new Stokvel(
                $"Payout Stokvel {Guid.NewGuid()}",
                1000m);

        var cycle =
            new ContributionCycle(
                stokvel.Id,
                "2026-10",
                1000m);

        var member =
            new StokvelMember(
                stokvel.Id,
                user.Id);

        await using (var setupContext =
            new RondiTrackDbContext(options))
        {
            await setupContext.Users.AddAsync(
                user);

            await setupContext.Stokvels.AddAsync(
                stokvel);

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
                new PayoutService(actContext, _factory.Services.GetRequiredService<RondiTrackTelemetry>());

            await service.CreatePayoutAsync(
                stokvel.Id,
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
            member.UserId,
            persistedPayout.RecipientUserId);

        Assert.Equal(
            member.StokvelId,
            persistedPayout.StokvelId);

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
        var options = _factory.CreateDbContextOptions();

        var user =
            new User(
                $"Rollback User {Guid.NewGuid()}",
                $"{Guid.NewGuid()}@example.com");

        var stokvel =
            new Stokvel(
                $"Rollback Stokvel {Guid.NewGuid()}",
                1500m);

        var cycle =
            new ContributionCycle(
                stokvel.Id,
                "2026-11",
                1500m);

        var member =
            new StokvelMember(
                stokvel.Id,
                user.Id);

        await using (var setupContext =
            new RondiTrackDbContext(options))
        {
            await setupContext.Users.AddAsync(
                user);

            await setupContext.Stokvels.AddAsync(
                stokvel);

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
                        stokvel.Id,
                        member.UserId,
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
