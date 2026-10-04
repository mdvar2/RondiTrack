using RondiTrack.Models;

namespace RondiTrack.Repositories;

public interface IContributionCycleRepository
{
    Task<IEnumerable<ContributionCycle>> GetAllByStokvelAsync(
        Guid stokvelId);

    Task<ContributionCycle?> GetByIdAsync(
        Guid id);

    Task<ContributionCycle?> GetByIdReadOnlyAsync(
        Guid id);

    Task AddAsync(
        ContributionCycle contributionCycle);

    Task UpdateAsync(
        ContributionCycle contributionCycle);

    Task<bool> DeleteAsync(
        Guid id);
}