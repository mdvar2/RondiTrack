using RondiTrack.Models;

namespace RondiTrack.Repositories;

public interface IContributionCycleRepository
{
    Task<IEnumerable<ContributionCycle>> GetAllByStokvelAsync(
        Guid stokvelId);

    Task<ContributionCycle?> GetByIdAsync(
        Guid id);

    Task AddAsync(
        ContributionCycle contributionCycle);

    Task<bool> DeleteAsync(
        Guid id);
}