using RondiTrack.Domain.Common;
using RondiTrack.Domain.Queries;
using RondiTrack.Domain.Entities;

namespace RondiTrack.Domain.Repositories;

public interface IStokvelMemberRepository
{
    Task<StokvelMember?> GetByStokvelAndUserAsync(
        Guid stokvelId,
        Guid userId);

    Task<PagedResult<StokvelMemberDetails>>
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