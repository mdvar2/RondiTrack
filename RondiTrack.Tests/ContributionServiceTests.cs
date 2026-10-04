using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using RondiTrack.Data;
using RondiTrack.DTOs.Contributions;
using RondiTrack.Exceptions;
using RondiTrack.Idempotency;
using RondiTrack.Models;
using RondiTrack.Repositories;
using RondiTrack.Services;

namespace RondiTrack.Tests;

public class ContributionServiceTests
{
    [Fact]
    public async Task RecordContributionAsync_WhenContributionAlreadyExistsForMemberAndCycle_ThrowsBusinessRuleException()
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

        await using var dbContext =
            new RondiTrackDbContext(options);

        var contributionRepository =
            new TestContributionRepository();

        var cycleRepository =
            new ContributionCycleRepository();

        var stokvelRepository =
            new StokvelRepository(dbContext);

        var userRepository =
            new UserRepository(dbContext);

        var stokvelMemberRepository =
            new EfStokvelMemberRepository(dbContext);

        var idempotencyStore =
            new IdempotencyStore();

        var user =
            new User(
                $"Contribution Test User {Guid.NewGuid()}",
                $"contribution-{Guid.NewGuid()}@example.com");

        var stokvel =
            new Stokvel(
                $"Contribution Test Stokvel {Guid.NewGuid()}",
                500m);

        await userRepository.AddAsync(user);
        await stokvelRepository.AddAsync(stokvel);

        var membership =
            new StokvelMember(
                stokvel.Id,
                user.Id);

        await stokvelMemberRepository.AddAsync(
            membership);

        var cycle =
            new ContributionCycle(
                stokvel.Id,
                "2026-09",
                500m);

        await cycleRepository.AddAsync(cycle);

        var service =
            new ContributionService(
                contributionRepository,
                cycleRepository,
                stokvelRepository,
                userRepository,
                stokvelMemberRepository,
                idempotencyStore);

        var request =
            new RecordContributionRequest(
                500m,
                cycle.Id);

        await service.RecordContributionAsync(
            stokvel.Id,
            user.Id,
            "first-key",
            request);

        // Act
        var exception =
            await Assert.ThrowsAsync<BusinessRuleException>(
                () => service.RecordContributionAsync(
                    stokvel.Id,
                    user.Id,
                    "second-key",
                    request));

        // Assert
        Assert.Equal(
            "A contribution already exists for this member and cycle.",
            exception.Message);
    }

    [Fact]
    public async Task RecordContributionAsync_WhenSameIdempotencyKeyUsedWithDifferentRequest_ThrowsBusinessRuleException()
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

        await using var dbContext =
            new RondiTrackDbContext(options);

        var contributionRepository =
            new TestContributionRepository();

        var cycleRepository =
            new ContributionCycleRepository();

        var stokvelRepository =
            new StokvelRepository(dbContext);

        var userRepository =
            new UserRepository(dbContext);

        var stokvelMemberRepository =
            new EfStokvelMemberRepository(dbContext);

        var idempotencyStore =
            new IdempotencyStore();

        var user =
            new User(
                $"Idempotency Test User {Guid.NewGuid()}",
                $"idempotency-{Guid.NewGuid()}@example.com");

        var stokvel =
            new Stokvel(
                $"Idempotency Test Stokvel {Guid.NewGuid()}",
                500m);

        await userRepository.AddAsync(user);
        await stokvelRepository.AddAsync(stokvel);

        var membership =
            new StokvelMember(
                stokvel.Id,
                user.Id);

        await stokvelMemberRepository.AddAsync(
            membership);

        var cycle =
            new ContributionCycle(
                stokvel.Id,
                "2026-09",
                500m);

        await cycleRepository.AddAsync(cycle);

        var service =
            new ContributionService(
                contributionRepository,
                cycleRepository,
                stokvelRepository,
                userRepository,
                stokvelMemberRepository,
                idempotencyStore);

        var firstRequest =
            new RecordContributionRequest(
                500m,
                cycle.Id);

        await service.RecordContributionAsync(
            stokvel.Id,
            user.Id,
            "payment-001",
            firstRequest);

        var secondRequest =
            new RecordContributionRequest(
                600m,
                cycle.Id);

        // Act
        var exception =
            await Assert.ThrowsAsync<BusinessRuleException>(
                () => service.RecordContributionAsync(
                    stokvel.Id,
                    user.Id,
                    "payment-001",
                    secondRequest));

        // Assert
        Assert.Equal(
            "Idempotency-Key was already used with a different request.",
            exception.Message);
    }
}