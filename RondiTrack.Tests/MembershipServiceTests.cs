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

        var service = new MembershipService(
            stokvelRepository,
            userRepository);

        await service.AddMemberAsync(stokvel.Id, user.Id);

        // Act
        var exception = await Assert.ThrowsAsync<BusinessRuleException>(
            () => service.AddMemberAsync(stokvel.Id, user.Id));

        // Assert
        Assert.Equal(
            "User is already a member of this stokvel.",
            exception.Message);
    }
}