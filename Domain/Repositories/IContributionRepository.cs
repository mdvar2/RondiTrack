using RondiTrack.Domain.Common;
using RondiTrack.Domain.Queries;
using RondiTrack.Domain.Entities;

namespace RondiTrack.Domain.Repositories;

public interface IContributionRepository
{
    Task<IEnumerable<Contribution>> GetAllAsync();

    Task<Contribution?> GetByMemberAndCycleAsync(
        Guid stokvelId,
        Guid userId,
        Guid contributionCycleId);

    Task<PagedResult<ContributionWithMember>>
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

    Task<IEnumerable<ContributionWithMember>>
        GetByCycleProjectedAsync(
            Guid stokvelId,
            Guid contributionCycleId);

    Task AddAsync(
        Contribution contribution);
}