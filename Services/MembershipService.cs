using RondiTrack.Exceptions;
using RondiTrack.Models;
using RondiTrack.Repositories;

namespace RondiTrack.Services;

public class MembershipService
{
    private readonly IStokvelRepository _stokvelRepository;
    private readonly IUserRepository _userRepository;
    private readonly IStokvelMemberRepository _stokvelMemberRepository;

    public MembershipService(
        IStokvelRepository stokvelRepository,
        IUserRepository userRepository,
        IStokvelMemberRepository stokvelMemberRepository)
    {
        _stokvelRepository = stokvelRepository;
        _userRepository = userRepository;
        _stokvelMemberRepository = stokvelMemberRepository;
    }

    public async Task AddMemberAsync(
        Guid stokvelId,
        Guid userId)
    {
        var stokvel =
            await _stokvelRepository.GetByIdReadOnlyAsync(
                stokvelId);

        if (stokvel is null)
        {
            throw new NotFoundException(
                "Stokvel not found.");
        }

        var user =
            await _userRepository.GetByIdReadOnlyAsync(
                userId);

        if (user is null)
        {
            throw new NotFoundException(
                "User not found.");
        }

        var membershipExists =
            await _stokvelMemberRepository.ExistsAsync(
                stokvelId,
                userId);

        if (membershipExists)
        {
            throw new BusinessRuleException(
                "User is already a member of this stokvel.");
        }

        var stokvelMember =
            new StokvelMember(
                stokvelId,
                userId);

        await _stokvelMemberRepository.AddAsync(
            stokvelMember);
    }

    public async Task RemoveMemberAsync(
        Guid stokvelId,
        Guid userId)
    {
        var stokvel =
            await _stokvelRepository.GetByIdReadOnlyAsync(
                stokvelId);

        if (stokvel is null)
        {
            throw new NotFoundException(
                "Stokvel not found.");
        }

        var existingMembership =
            await _stokvelMemberRepository
                .GetByStokvelAndUserAsync(
                    stokvelId,
                    userId);

        if (existingMembership is null)
        {
            throw new BusinessRuleException(
                "User is not a member of this stokvel.");
        }

        await _stokvelMemberRepository.RemoveAsync(
            existingMembership);
    }
}