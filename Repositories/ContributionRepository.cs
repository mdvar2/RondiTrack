using RondiTrack.Models;

namespace RondiTrack.Repositories;

public class ContributionRepository
    : IContributionRepository
{
    private readonly List<Contribution> _contributions = new();

    public Task<IEnumerable<Contribution>> GetAllAsync()
    {
        return Task.FromResult<IEnumerable<Contribution>>(
            _contributions.ToList());
    }

    public Task<Contribution?> GetByMemberAndCycleAsync(
        Guid stokvelId,
        Guid userId,
        Guid contributionCycleId)
    {
        var contribution =
            _contributions.FirstOrDefault(
                contribution =>
                    contribution.StokvelId == stokvelId &&
                    contribution.UserId == userId &&
                    contribution.ContributionCycleId ==
                        contributionCycleId);

        return Task.FromResult(contribution);
    }

    public Task AddAsync(
        Contribution contribution)
    {
        _contributions.Add(contribution);

        return Task.CompletedTask;
    }
}