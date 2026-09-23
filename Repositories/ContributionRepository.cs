using RondiTrack.Models;

namespace RondiTrack.Repositories;

public class ContributionRepository : IContributionRepository
{
    private readonly List<Contribution> _contributions = new();

    public Task<IEnumerable<Contribution>> GetAllAsync()
    {
        return Task.FromResult<IEnumerable<Contribution>>(_contributions);
    }

    public Task<Contribution?> GetByMemberAndCycleAsync(
        Guid stokvelId,
        Guid userId,
        string cycle)
    {
        var contribution = _contributions.FirstOrDefault(c =>
            c.StokvelId == stokvelId &&
            c.UserId == userId &&
            c.Cycle.Equals(
                cycle,
                StringComparison.OrdinalIgnoreCase));

        return Task.FromResult(contribution);
    }

    public Task AddAsync(Contribution contribution)
    {
        _contributions.Add(contribution);
        return Task.CompletedTask;
    }
}