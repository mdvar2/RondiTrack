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
        var contributionRepository = new ContributionRepository();
        var cycleRepository = new ContributionCycleRepository();
        var stokvelRepository = new StokvelRepository();
        var userRepository = new UserRepository();
        var idempotencyStore = new IdempotencyStore();

        var users = await userRepository.GetAllAsync();
        var stokvels = await stokvelRepository.GetAllAsync();

        var user = users.First();
        var stokvel = stokvels.First();

        stokvel.AddMember(user);

        var cycle = new ContributionCycle(
            stokvel.Id,
            "2026-09",
            500m);

        await cycleRepository.AddAsync(cycle);

        var service = new ContributionService(
            contributionRepository,
            cycleRepository,
            stokvelRepository,
            userRepository,
            idempotencyStore);

        var request = new RecordContributionRequest(
            500m,
            cycle.Id);

        await service.RecordContributionAsync(
            stokvel.Id,
            user.Id,
            "first-key",
            request);

        // Act
        var exception = await Assert.ThrowsAsync<BusinessRuleException>(
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
        var contributionRepository = new ContributionRepository();
        var cycleRepository = new ContributionCycleRepository();
        var stokvelRepository = new StokvelRepository();
        var userRepository = new UserRepository();
        var idempotencyStore = new IdempotencyStore();

        var users = await userRepository.GetAllAsync();
        var stokvels = await stokvelRepository.GetAllAsync();

        var user = users.First();
        var stokvel = stokvels.First();

        stokvel.AddMember(user);

        var cycle = new ContributionCycle(
            stokvel.Id,
            "2026-09",
            500m);

        await cycleRepository.AddAsync(cycle);

        var service = new ContributionService(
            contributionRepository,
            cycleRepository,
            stokvelRepository,
            userRepository,
            idempotencyStore);

        var firstRequest = new RecordContributionRequest(
            500m,
            cycle.Id);

        await service.RecordContributionAsync(
            stokvel.Id,
            user.Id,
            "payment-001",
            firstRequest);

        var secondRequest = new RecordContributionRequest(
            600m, // Different amount
            cycle.Id);

        // Act
        var exception = await Assert.ThrowsAsync<BusinessRuleException>(
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