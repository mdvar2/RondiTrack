using Microsoft.EntityFrameworkCore;
using RondiTrack.Data;
using RondiTrack.Models;

namespace RondiTrack.Repositories;

public class ContributionRepository
    : IContributionRepository
{
    private readonly RondiTrackDbContext _dbContext;

    public ContributionRepository(
        RondiTrackDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<IEnumerable<Contribution>> GetAllAsync()
    {
        return await _dbContext.Contributions
            .AsNoTracking()
            .ToListAsync();
    }

    public async Task<Contribution?> GetByMemberAndCycleAsync(
        Guid stokvelId,
        Guid userId,
        Guid contributionCycleId)
    {
        return await _dbContext.Contributions
            .AsNoTracking()
            .FirstOrDefaultAsync(
                contribution =>
                    contribution.StokvelId == stokvelId &&
                    contribution.UserId == userId &&
                    contribution.ContributionCycleId ==
                        contributionCycleId);
    }

    public async Task AddAsync(
        Contribution contribution)
    {
        await _dbContext.Contributions.AddAsync(
            contribution);

        await _dbContext.SaveChangesAsync();
    }
}