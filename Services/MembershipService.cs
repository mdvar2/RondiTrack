using RondiTrack.Exceptions;
using RondiTrack.Repositories;

namespace RondiTrack.Services;

public class MembershipService
{
    private readonly IStokvelRepository _stokvelRepository;
    private readonly IUserRepository _userRepository;

    public MembershipService(
        IStokvelRepository stokvelRepository,
        IUserRepository userRepository)
    {
        _stokvelRepository = stokvelRepository;
        _userRepository = userRepository;
    }

    public async Task AddMemberAsync(
        Guid stokvelId,
        Guid userId)
    {
        var stokvel =
            await _stokvelRepository.GetByIdAsync(stokvelId);

        if (stokvel is null)
            throw new NotFoundException("Stokvel not found.");

        var user =
            await _userRepository.GetByIdAsync(userId);

        if (user is null)
            throw new NotFoundException("User not found.");

        if (stokvel.Members.Any(member => member.Id == userId))
        {
            throw new BusinessRuleException(
                "User is already a member of this stokvel.");
        }

        stokvel.AddMember(user);
    }

    public async Task RemoveMemberAsync(
        Guid stokvelId,
        Guid userId)
    {
        var stokvel =
            await _stokvelRepository.GetByIdAsync(stokvelId);

        if (stokvel is null)
            throw new NotFoundException("Stokvel not found.");

        if (!stokvel.Members.Any(member => member.Id == userId))
        {
            throw new BusinessRuleException(
                "User is not a member of this stokvel.");
        }

        stokvel.RemoveMember(userId);
    }
}