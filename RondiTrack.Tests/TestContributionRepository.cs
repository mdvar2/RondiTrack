using RondiTrack.Domain.Common;
using RondiTrack.Domain.Queries;
using RondiTrack.Domain.Entities;
using RondiTrack.Domain.Repositories;

namespace RondiTrack.Tests;

public class TestContributionRepository
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

    public Task<PagedResult<ContributionWithMember>> GetByCyclePagedAsync(
        Guid stokvelId,
        Guid contributionCycleId,
        int pageSize,
        string? pageToken,
        string sortBy,
        string sortDirection,
        Guid? userId,
        string? role,
        decimal? minAmount,
        decimal? maxAmount)
    {
        return Task.FromResult(
            new PagedResult<ContributionWithMember>(
                new List<ContributionWithMember>(),
                string.Empty,
                pageSize));
    }

    public Task<IEnumerable<ContributionWithMember>> GetByCycleProjectedAsync(
        Guid stokvelId,
        Guid contributionCycleId)
    {
        return Task.FromResult<IEnumerable<ContributionWithMember>>(
            new List<ContributionWithMember>());
    }

    public Task AddAsync(
        Contribution contribution)
    {
        _contributions.Add(contribution);

        return Task.CompletedTask;
    }
}