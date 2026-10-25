using RondiTrack.Tests.TestSupport;
using RondiTrack.DTOs.Contributions;
using RondiTrack.Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;
using RondiTrack.Infrastructure.Data;
using RondiTrack.Domain.Queries;
using RondiTrack.Domain.Exceptions;
using RondiTrack.Idempotency;
using RondiTrack.Domain.Entities;
using RondiTrack.Domain.Repositories;
using RondiTrack.Services;
using Microsoft.Extensions.DependencyInjection;
using RondiTrack.Observability;

namespace RondiTrack.Tests;

[Collection(PostgresCollection.Name)]
public class ContributionServiceTests
{
    private readonly PostgresApiFactory _factory;

    public ContributionServiceTests(PostgresApiFactory factory) => _factory = factory;

    [Fact]
    public async Task RecordContributionAsync_WhenContributionAlreadyExistsForMemberAndCycle_ThrowsBusinessRuleException()
    {
        // Arrange
        var options = _factory.CreateDbContextOptions();

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
                idempotencyStore, _factory.Services.GetRequiredService<RondiTrackTelemetry>());

        var request =
            new RecordContributionRequest(
                50000,
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
        var options = _factory.CreateDbContextOptions();

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
                idempotencyStore, _factory.Services.GetRequiredService<RondiTrackTelemetry>());

        var firstRequest =
            new RecordContributionRequest(
                50000,
                cycle.Id);

        await service.RecordContributionAsync(
            stokvel.Id,
            user.Id,
            "payment-001",
            firstRequest);

        var secondRequest =
            new RecordContributionRequest(
                60000,
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
