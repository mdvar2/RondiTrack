using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using RondiTrack.Common;
using RondiTrack.Data;
using RondiTrack.DTOs.Contributions;
using RondiTrack.Exceptions;
using RondiTrack.Models;

namespace RondiTrack.Repositories;

public class ContributionRepository : IContributionRepository
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
                    contribution.ContributionCycleId == contributionCycleId);
    }

    public async Task<PagedResult<ContributionWithMemberResponse>> GetByCyclePagedAsync(
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
        if (!string.Equals(sortDirection, "asc", StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(sortDirection, "desc", StringComparison.OrdinalIgnoreCase))
        {
            throw new RequestValidationException(
                "Sort direction must be 'asc' or 'desc'.");
        }

        var validSorts = new[]
        {
            "recordedAtUtc",
            "amount",
            "userId"
        };

        if (!validSorts.Contains(sortBy, StringComparer.OrdinalIgnoreCase))
        {
            throw new RequestValidationException(
                "Unsupported sort field for contributions.");
        }

        var query = _dbContext.Contributions
            .AsNoTracking()
            .Where(contribution =>
                contribution.StokvelId == stokvelId &&
                contribution.ContributionCycleId == contributionCycleId);

        if (userId.HasValue)
        {
            query = query.Where(contribution => contribution.UserId == userId.Value);
        }

        if (!string.IsNullOrWhiteSpace(role))
        {
            query = query.Where(contribution => contribution.Member.Role == role.Trim());
        }

        if (minAmount.HasValue)
        {
            query = query.Where(contribution => contribution.Amount >= minAmount.Value);
        }

        if (maxAmount.HasValue)
        {
            query = query.Where(contribution => contribution.Amount <= maxAmount.Value);
        }

        var token = ParseToken(pageToken, sortBy, sortDirection, userId, role, minAmount, maxAmount);
        if (token is not null)
        {
            query = ApplyToken(query, sortBy, sortDirection, token.LastValue, token.LastId);
        }

        query = ApplySort(query, sortBy, sortDirection);

        var rows = await query
            .Select(contribution => new
            {
                contribution.Id,
                contribution.StokvelId,
                contribution.UserId,
                contribution.Member.User.Name,
                contribution.Member.User.Email,
                contribution.Member.Role,
                contribution.ContributionCycleId,
                contribution.Amount,
                contribution.RecordedAtUtc,
                contribution.Version
            })
            .Take(pageSize + 1)
            .ToListAsync();

        var hasMore = rows.Count > pageSize;
        var pageRows = rows.Take(pageSize).ToList();

        var items = pageRows
            .Select(row => new ContributionWithMemberResponse(
                row.Id,
                row.StokvelId,
                row.UserId,
                row.Name,
                row.Email,
                row.Role,
                row.ContributionCycleId,
                row.Amount,
                row.RecordedAtUtc,
                row.Version))
            .ToList();

        var nextToken = hasMore
            ? EncodeToken(
                items[^1],
                sortBy,
                sortDirection,
                userId,
                role,
                minAmount,
                maxAmount)
            : string.Empty;

        return new PagedResult<ContributionWithMemberResponse>(
            items,
            nextToken,
            pageSize);
    }

    public async Task<IEnumerable<ContributionWithMemberResponse>>
        GetByCycleProjectedAsync(
            Guid stokvelId,
            Guid contributionCycleId)
    {
        return await _dbContext.Contributions
            .AsNoTracking()
            .Where(
                contribution =>
                    contribution.StokvelId == stokvelId &&
                    contribution.ContributionCycleId == contributionCycleId)
            .Select(
                contribution =>
                    new ContributionWithMemberResponse(
                        contribution.Id,
                        contribution.StokvelId,
                        contribution.UserId,
                        contribution.Member.User.Name,
                        contribution.Member.User.Email,
                        contribution.Member.Role,
                        contribution.ContributionCycleId,
                        contribution.Amount,
                        contribution.RecordedAtUtc,
                        contribution.Version))
            .ToListAsync();
    }

    private static IQueryable<Contribution> ApplySort(
        IQueryable<Contribution> query,
        string sortBy,
        string sortDirection)
    {
        var isDescending = string.Equals(sortDirection, "desc", StringComparison.OrdinalIgnoreCase);

        return sortBy.ToLowerInvariant() switch
        {
            "recordedatutc" => isDescending
                ? query.OrderByDescending(contribution => contribution.RecordedAtUtc)
                    .ThenByDescending(contribution => contribution.Id)
                : query.OrderBy(contribution => contribution.RecordedAtUtc)
                    .ThenBy(contribution => contribution.Id),
            "amount" => isDescending
                ? query.OrderByDescending(contribution => contribution.Amount)
                    .ThenByDescending(contribution => contribution.Id)
                : query.OrderBy(contribution => contribution.Amount)
                    .ThenBy(contribution => contribution.Id),
            "userid" => isDescending
                ? query.OrderByDescending(contribution => contribution.UserId)
                    .ThenByDescending(contribution => contribution.Id)
                : query.OrderBy(contribution => contribution.UserId)
                    .ThenBy(contribution => contribution.Id),
            _ => throw new RequestValidationException(
                "Unsupported sort field for contributions.")
        };
    }

    private static IQueryable<Contribution> ApplyToken(
        IQueryable<Contribution> query,
        string sortBy,
        string sortDirection,
        string? lastValue,
        Guid? lastId)
    {
        if (string.IsNullOrWhiteSpace(lastValue))
        {
            return query;
        }

        var isDescending = string.Equals(sortDirection, "desc", StringComparison.OrdinalIgnoreCase);

        return sortBy.ToLowerInvariant() switch
        {
            "recordedatutc" =>
                DateTime.TryParse(lastValue, out var lastRecordedAt)
                    ? (isDescending
                        ? query.Where(contribution =>
                            contribution.RecordedAtUtc < lastRecordedAt ||
                            (contribution.RecordedAtUtc == lastRecordedAt && contribution.Id < lastId.GetValueOrDefault()))
                        : query.Where(contribution =>
                            contribution.RecordedAtUtc > lastRecordedAt ||
                            (contribution.RecordedAtUtc == lastRecordedAt && contribution.Id > lastId.GetValueOrDefault())))
                    : query,
            "amount" =>
                decimal.TryParse(lastValue, out var lastAmount)
                    ? (isDescending
                        ? query.Where(contribution =>
                            contribution.Amount < lastAmount ||
                            (contribution.Amount == lastAmount && contribution.Id < lastId.GetValueOrDefault()))
                        : query.Where(contribution =>
                            contribution.Amount > lastAmount ||
                            (contribution.Amount == lastAmount && contribution.Id > lastId.GetValueOrDefault())))
                    : query,
            "userid" =>
                Guid.TryParse(lastValue, out var lastUserId)
                    ? (isDescending
                        ? query.Where(contribution =>
                            contribution.UserId < lastUserId ||
                            (contribution.UserId == lastUserId && contribution.Id < lastId.GetValueOrDefault()))
                        : query.Where(contribution =>
                            contribution.UserId > lastUserId ||
                            (contribution.UserId == lastUserId && contribution.Id > lastId.GetValueOrDefault())))
                    : query,
            _ => query
        };
    }

    private static string EncodeToken(
        ContributionWithMemberResponse lastItem,
        string sortBy,
        string sortDirection,
        Guid? userId,
        string? role,
        decimal? minAmount,
        decimal? maxAmount)
    {
        var token = new PageToken(
            sortBy,
            sortDirection,
            userId,
            role,
            minAmount,
            maxAmount,
            sortBy switch
            {
                "recordedAtUtc" => lastItem.RecordedAtUtc.ToString("O"),
                "amount" => lastItem.Amount.ToString("G29"),
                "userId" => lastItem.UserId.ToString(),
                _ => string.Empty
            },
            sortBy is "recordedAtUtc" or "amount" or "userId"
                ? lastItem.Id
                : null);

        var json = JsonSerializer.Serialize(token);
        return Convert.ToBase64String(Encoding.UTF8.GetBytes(json));
    }

    private static PageToken? ParseToken(
        string? pageToken,
        string sortBy,
        string sortDirection,
        Guid? userId,
        string? role,
        decimal? minAmount,
        decimal? maxAmount)
    {
        if (string.IsNullOrWhiteSpace(pageToken))
        {
            return null;
        }

        try
        {
            var json = Encoding.UTF8.GetString(
                Convert.FromBase64String(pageToken));
            var token = JsonSerializer.Deserialize<PageToken>(json);
            if (token is null)
            {
                throw new RequestValidationException("Page token is invalid.");
            }

            if (!string.Equals(token.SortBy, sortBy, StringComparison.OrdinalIgnoreCase) ||
                !string.Equals(token.SortDirection, sortDirection, StringComparison.OrdinalIgnoreCase) ||
                token.UserId != userId ||
                !string.Equals(token.Role, role, StringComparison.OrdinalIgnoreCase) ||
                token.MinAmount != minAmount ||
                token.MaxAmount != maxAmount)
            {
                throw new RequestValidationException(
                    "Page token does not match the current sort or filter.");
            }

            return token;
        }
        catch (FormatException)
        {
            throw new RequestValidationException("Page token is invalid.");
        }
    }

    public async Task AddAsync(
        Contribution contribution)
    {
        await _dbContext.Contributions.AddAsync(contribution);
        await _dbContext.SaveChangesAsync();
    }

    private sealed record PageToken(
        string SortBy,
        string SortDirection,
        Guid? UserId,
        string? Role,
        decimal? MinAmount,
        decimal? MaxAmount,
        string? LastValue,
        Guid? LastId);
}