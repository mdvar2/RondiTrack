using Microsoft.EntityFrameworkCore;
using RondiTrack.Data;
using RondiTrack.Models;

namespace RondiTrack.Repositories;

public class EfContributionCycleRepository
    : IContributionCycleRepository
{
    private readonly RondiTrackDbContext _dbContext;

    public EfContributionCycleRepository(
        RondiTrackDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<IEnumerable<ContributionCycle>>
        GetAllByStokvelAsync(
            Guid stokvelId)
    {
        return await _dbContext.ContributionCycles
            .AsNoTracking()
            .Where(
                cycle =>
                    cycle.StokvelId == stokvelId)
            .ToListAsync();
    }

    public async Task<ContributionCycle?> GetByIdAsync(
        Guid id)
    {
        return await _dbContext.ContributionCycles
            .FirstOrDefaultAsync(
                cycle =>
                    cycle.Id == id);
    }

    public async Task<ContributionCycle?> GetByIdReadOnlyAsync(
        Guid id)
    {
        return await _dbContext.ContributionCycles
            .AsNoTracking()
            .FirstOrDefaultAsync(
                cycle =>
                    cycle.Id == id);
    }

    public async Task AddAsync(
        ContributionCycle contributionCycle)
    {
        await _dbContext.ContributionCycles.AddAsync(
            contributionCycle);

        await _dbContext.SaveChangesAsync();
    }

    public async Task UpdateAsync(
        ContributionCycle contributionCycle)
    {
        _dbContext.ContributionCycles.Update(
            contributionCycle);

        await _dbContext.SaveChangesAsync();
    }

    public async Task<bool> DeleteAsync(
        Guid id)
    {
        var cycle =
            await _dbContext.ContributionCycles
                .FirstOrDefaultAsync(
                    cycle =>
                        cycle.Id == id);

        if (cycle is null)
            return false;

        _dbContext.ContributionCycles.Remove(
            cycle);

        await _dbContext.SaveChangesAsync();

        return true;
    }
}