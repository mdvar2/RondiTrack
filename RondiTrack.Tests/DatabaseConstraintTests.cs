using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using RondiTrack.Data;
using RondiTrack.Models;

namespace RondiTrack.Tests;

public class DatabaseConstraintTests
{
    private static DbContextOptions<RondiTrackDbContext> CreateOptions()
    {
        var configuration = new ConfigurationBuilder()
            .AddUserSecrets<Program>()
            .Build();

        var connectionString =
            configuration.GetConnectionString("RondiTrackDb")
            ?? throw new InvalidOperationException(
                "Connection string 'RondiTrackDb' was not found.");

        return new DbContextOptionsBuilder<RondiTrackDbContext>()
            .UseNpgsql(connectionString)
            .Options;
    }

    [Fact]
    public async Task SaveChanges_DuplicateContribution_BypassingService_IsRejectedByDatabase()
    {
        var options = CreateOptions();

        var user = new User(
            $"Constraint User {Guid.NewGuid()}",
            $"{Guid.NewGuid()}@example.com");

        var stokvel = new Stokvel(
            $"Constraint Stokvel {Guid.NewGuid()}",
            1000m);

        var cycle = new ContributionCycle(
            stokvel.Id,
            $"period-{Guid.NewGuid()}",
            1000m);

        var member = new StokvelMember(stokvel.Id, user.Id);

        await using var context = new RondiTrackDbContext(options);

        await context.Users.AddAsync(user);
        await context.Stokvels.AddAsync(stokvel);
        await context.ContributionCycles.AddAsync(cycle);
        await context.StokvelMembers.AddAsync(member);
        await context.SaveChangesAsync();

        // Deliberately bypass ContributionService. The database unique
        // constraint must remain the final defence against this duplicate.
        await context.Contributions.AddAsync(
            new Contribution(stokvel.Id, user.Id, cycle.Id, 1000m));
        await context.SaveChangesAsync();

        await context.Contributions.AddAsync(
            new Contribution(stokvel.Id, user.Id, cycle.Id, 1000m));

        await Assert.ThrowsAsync<DbUpdateException>(
            () => context.SaveChangesAsync());
    }

    [Fact]
    public async Task SaveChanges_SecondPayoutForSameCycle_BypassingService_IsRejectedByDatabase()
    {
        var options = CreateOptions();

        var firstUser = new User(
            $"Payout User A {Guid.NewGuid()}",
            $"{Guid.NewGuid()}@example.com");
        var secondUser = new User(
            $"Payout User B {Guid.NewGuid()}",
            $"{Guid.NewGuid()}@example.com");

        var stokvel = new Stokvel(
            $"Payout Stokvel {Guid.NewGuid()}",
            1000m);

        var cycle = new ContributionCycle(
            stokvel.Id,
            $"period-{Guid.NewGuid()}",
            1000m);

        await using var context = new RondiTrackDbContext(options);

        await context.Users.AddRangeAsync(firstUser, secondUser);
        await context.Stokvels.AddAsync(stokvel);
        await context.ContributionCycles.AddAsync(cycle);
        await context.StokvelMembers.AddRangeAsync(
            new StokvelMember(stokvel.Id, firstUser.Id),
            new StokvelMember(stokvel.Id, secondUser.Id));
        await context.SaveChangesAsync();

        await context.Payouts.AddAsync(
            new Payout(stokvel.Id, firstUser.Id, cycle.Id, 1000m));
        await context.SaveChangesAsync();

        await context.Payouts.AddAsync(
            new Payout(stokvel.Id, secondUser.Id, cycle.Id, 1000m));

        await Assert.ThrowsAsync<DbUpdateException>(
            () => context.SaveChangesAsync());
    }
}
