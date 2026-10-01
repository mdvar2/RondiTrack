using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using RondiTrack.Data;
using RondiTrack.Exceptions;
using RondiTrack.Repositories;
using RondiTrack.Services;

namespace RondiTrack.Tests;

public class MembershipServiceTests
{
    [Fact]
    public async Task AddMemberAsync_WhenUserAlreadyMember_ThrowsBusinessRuleException()
    {
        // Arrange
        var userRepository = new UserRepository();
        var stokvelRepository = new StokvelRepository();

        var users = await userRepository.GetAllAsync();
        var stokvels = await stokvelRepository.GetAllAsync();

        var user = users.First();
        var stokvel = stokvels.First();

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

        var stokvelMemberRepository =
            new EfStokvelMemberRepository(
                dbContext);

        var existingMember =
            await stokvelMemberRepository
                .GetByStokvelAndUserAsync(
                    stokvel.Id,
                    user.Id);

        if (existingMember is not null)
        {
            await stokvelMemberRepository.RemoveAsync(
                existingMember);
        }

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