using RondiTrack.Tests.TestSupport;
using RondiTrack.Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;
using RondiTrack.Infrastructure.Data;
using RondiTrack.Domain.Exceptions;
using RondiTrack.Domain.Entities;
using RondiTrack.Domain.Repositories;
using RondiTrack.Services;

namespace RondiTrack.Tests;

[Collection(PostgresCollection.Name)]
public class MembershipServiceTests
{
    private readonly PostgresApiFactory _factory;

    public MembershipServiceTests(PostgresApiFactory factory) => _factory = factory;

    [Fact]
    public async Task AddMemberAsync_WhenUserAlreadyMember_ThrowsBusinessRuleException()
    {
        // Arrange
        var options = _factory.CreateDbContextOptions();

        await using var dbContext =
            new RondiTrackDbContext(options);

        var userRepository =
            new UserRepository(dbContext);

        var stokvelRepository =
            new StokvelRepository(dbContext);

        var stokvelMemberRepository =
            new EfStokvelMemberRepository(dbContext);

        var user =
            new User(
                $"Membership Test User {Guid.NewGuid()}",
                $"membership-{Guid.NewGuid()}@example.com");

        var stokvel =
            new Stokvel(
                $"Membership Test Stokvel {Guid.NewGuid()}",
                500m);

        await userRepository.AddAsync(user);
        await stokvelRepository.AddAsync(stokvel);

        var service =
            new MembershipService(
                stokvelRepository,
                userRepository,
                stokvelMemberRepository);

        await service.AddMemberAsync(
            stokvel.Id,
            user.Id);

        // Act
        var exception =
            await Assert.ThrowsAsync<BusinessRuleException>(
                () => service.AddMemberAsync(
                    stokvel.Id,
                    user.Id));

        // Assert
        Assert.Equal(
            "User is already a member of this stokvel.",
            exception.Message);
    }
}