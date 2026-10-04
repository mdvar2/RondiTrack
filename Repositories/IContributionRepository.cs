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

    Task<IEnumerable<ContributionWithMemberResponse>>
        GetByCycleProjectedAsync(
            Guid stokvelId,
            Guid contributionCycleId);

    Task AddAsync(
        Contribution contribution);
}