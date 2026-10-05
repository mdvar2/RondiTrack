using RondiTrack.Common;
using RondiTrack.DTOs.Contributions;
using RondiTrack.Models;

namespace RondiTrack.Repositories;

public interface IContributionRepository
{
    Task<IEnumerable<Contribution>> GetAllAsync();

    Task<Contribution?> GetByMemberAndCycleAsync(
        Guid stokvelId,
        Guid userId,
        Guid contributionCycleId);

    Task<PagedResult<ContributionWithMemberResponse>>
        GetByCyclePagedAsync(
            Guid stokvelId,
            Guid contributionCycleId,
            int pageSize,
            string? pageToken,
            string sortBy,
            string sortDirection,
            Guid? userId,
            string? role,
            decimal? minAmount,
            decimal? maxAmount);

    Task<IEnumerable<ContributionWithMemberResponse>>
        GetByCycleProjectedAsync(
            Guid stokvelId,
            Guid contributionCycleId);

    Task AddAsync(
        Contribution contribution);
}