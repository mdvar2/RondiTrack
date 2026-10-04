using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using RondiTrack.Data;
using RondiTrack.Exceptions;
using RondiTrack.Models;
using RondiTrack.Repositories;
using RondiTrack.Services;

namespace RondiTrack.Tests;

public class MembershipServiceTests
{
    [Fact]
    public async Task AddMemberAsync_WhenUserAlreadyMember_ThrowsBusinessRuleException()
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