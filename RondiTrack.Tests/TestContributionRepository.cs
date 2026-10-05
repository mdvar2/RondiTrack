using RondiTrack.Common;
using RondiTrack.DTOs.Contributions;
using RondiTrack.Models;
using RondiTrack.Repositories;

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

    public Task<PagedResult<ContributionWithMemberResponse>> GetByCyclePagedAsync(
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
            new PagedResult<ContributionWithMemberResponse>(
                new List<ContributionWithMemberResponse>(),
                string.Empty,
                pageSize));
    }

    public Task<IEnumerable<ContributionWithMemberResponse>> GetByCycleProjectedAsync(
        Guid stokvelId,
        Guid contributionCycleId)
    {
        return Task.FromResult<IEnumerable<ContributionWithMemberResponse>>(
            new List<ContributionWithMemberResponse>());
    }

    public Task AddAsync(
        Contribution contribution)
    {
        _contributions.Add(contribution);

        return Task.CompletedTask;
    }
}