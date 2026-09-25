using RondiTrack.Models;

namespace RondiTrack.Repositories;

public class ContributionCycleRepository
    : IContributionCycleRepository
{
    private readonly List<ContributionCycle> _cycles = new();

    public Task<IEnumerable<ContributionCycle>> GetAllByStokvelAsync(
        Guid stokvelId)
    {
        var cycles = _cycles
            .Where(cycle => cycle.StokvelId == stokvelId)
            .ToList();

        return Task.FromResult<IEnumerable<ContributionCycle>>(cycles);
    }

    public Task<ContributionCycle?> GetByIdAsync(
        Guid id)
    {
        var cycle = _cycles
            .FirstOrDefault(cycle => cycle.Id == id);

        return Task.FromResult(cycle);
    }

    public Task AddAsync(
        ContributionCycle contributionCycle)
    {
        _cycles.Add(contributionCycle);

        return Task.CompletedTask;
    }

    public Task<bool> DeleteAsync(
        Guid id)
    {
        var cycle = _cycles
            .FirstOrDefault(cycle => cycle.Id == id);

        if (cycle is null)
            return Task.FromResult(false);

        _cycles.Remove(cycle);

        return Task.FromResult(true);
    }
}