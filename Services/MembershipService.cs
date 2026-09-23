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

    public async Task<MembershipResult> AddMemberAsync(
        Guid stokvelId,
        Guid userId)
    {
        var stokvel =
            await _stokvelRepository.GetByIdAsync(stokvelId);

        if (stokvel is null)
            return MembershipResult.StokvelNotFound;

        var user =
            await _userRepository.GetByIdAsync(userId);

        if (user is null)
            return MembershipResult.UserNotFound;

        if (stokvel.Members.Any(member => member.Id == userId))
            return MembershipResult.AlreadyMember;

        stokvel.AddMember(user);

        return MembershipResult.Success;
    }

    public async Task<MembershipResult> RemoveMemberAsync(
        Guid stokvelId,
        Guid userId)
    {
        var stokvel =
            await _stokvelRepository.GetByIdAsync(stokvelId);

        if (stokvel is null)
            return MembershipResult.StokvelNotFound;

        if (!stokvel.Members.Any(member => member.Id == userId))
            return MembershipResult.NotMember;

        stokvel.RemoveMember(userId);

        return MembershipResult.Success;
    }
}