using RondiTrack.Common;
using RondiTrack.DTOs.Stokvels;
using RondiTrack.Models;

namespace RondiTrack.Repositories;

public interface IStokvelMemberRepository
{
    Task<StokvelMember?> GetByStokvelAndUserAsync(
        Guid stokvelId,
        Guid userId);

    Task<PagedResult<StokvelMemberResponse>>
        GetByStokvelPagedAsync(
            Guid stokvelId,
            int pageSize,
            string? pageToken,
            string sortBy,
            string sortDirection,
            string? role);

    Task<bool> ExistsAsync(
        Guid stokvelId,
        Guid userId);

    Task AddAsync(StokvelMember stokvelMember);

    Task RemoveAsync(StokvelMember stokvelMember);
}