using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using RondiTrack.Common;
using RondiTrack.Data;
using RondiTrack.DTOs.Stokvels;
using RondiTrack.Exceptions;
using RondiTrack.Models;

namespace RondiTrack.Repositories;

public class EfStokvelMemberRepository : IStokvelMemberRepository
{
    private readonly RondiTrackDbContext _dbContext;

    public EfStokvelMemberRepository(
        RondiTrackDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<StokvelMember?> GetByStokvelAndUserAsync(
        Guid stokvelId,
        Guid userId)
    {
        return await _dbContext.StokvelMembers
            .FirstOrDefaultAsync(
                member =>
                    member.StokvelId == stokvelId &&
                    member.UserId == userId);
    }

    public async Task<PagedResult<StokvelMemberResponse>> GetByStokvelPagedAsync(
        Guid stokvelId,
        int pageSize,
        string? pageToken,
        string sortBy,
        string sortDirection,
        string? role)
    {
        if (!string.Equals(sortDirection, "asc", StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(sortDirection, "desc", StringComparison.OrdinalIgnoreCase))
        {
            throw new RequestValidationException(
                "Sort direction must be 'asc' or 'desc'.");
        }

        var validSorts = new[]
        {
            "joinedAtUtc",
            "userId",
            "role"
        };

        if (!validSorts.Contains(sortBy, StringComparer.OrdinalIgnoreCase))
        {
            throw new RequestValidationException(
                "Unsupported sort field for stokvel members.");
        }

        var query = _dbContext.StokvelMembers
            .AsNoTracking()
            .Where(member => member.StokvelId == stokvelId);

        if (!string.IsNullOrWhiteSpace(role))
        {
            query = query.Where(member => member.Role == role.Trim());
        }

        var token = ParseToken(pageToken, sortBy, sortDirection, role);
        if (token is not null)
        {
            query = ApplyToken(query, sortBy, sortDirection, token.LastValue, token.LastId);
        }

        query = ApplySort(query, sortBy, sortDirection);

        var rows = await query
            .Select(member => new
            {
                member.StokvelId,
                member.UserId,
                member.Role,
                member.JoinedAtUtc,
                member.User.Name,
                member.User.Email,
                member.Version
            })
            .Take(pageSize + 1)
            .ToListAsync();

        var hasMore = rows.Count > pageSize;
        var pageRows = rows.Take(pageSize).ToList();

        var items = pageRows
            .Select(row => new StokvelMemberResponse(
                row.StokvelId,
                row.UserId,
                row.Name,
                row.Email,
                row.Role,
                row.JoinedAtUtc,
                row.Version))
            .ToList();

        var nextToken = hasMore
            ? EncodeToken(
                items[^1],
                sortBy,
                sortDirection,
                role)
            : string.Empty;

        return new PagedResult<StokvelMemberResponse>(
            items,
            nextToken,
            pageSize);
    }

    private static IQueryable<StokvelMember> ApplySort(
        IQueryable<StokvelMember> query,
        string sortBy,
        string sortDirection)
    {
        var isDescending = string.Equals(sortDirection, "desc", StringComparison.OrdinalIgnoreCase);

        return sortBy.ToLowerInvariant() switch
        {
            "joinedatutc" => isDescending
                ? query.OrderByDescending(member => member.JoinedAtUtc)
                    .ThenByDescending(member => member.UserId)
                : query.OrderBy(member => member.JoinedAtUtc)
                    .ThenBy(member => member.UserId),
            "userid" => isDescending
                ? query.OrderByDescending(member => member.UserId)
                : query.OrderBy(member => member.UserId),
            "role" => isDescending
                ? query.OrderByDescending(member => member.Role)
                    .ThenByDescending(member => member.UserId)
                : query.OrderBy(member => member.Role)
                    .ThenBy(member => member.UserId),
            _ => throw new RequestValidationException(
                "Unsupported sort field for stokvel members.")
        };
    }

    private static IQueryable<StokvelMember> ApplyToken(
        IQueryable<StokvelMember> query,
        string sortBy,
        string sortDirection,
        string? lastValue,
        Guid? lastId)
    {
        if (string.IsNullOrWhiteSpace(lastValue) || lastId is null)
        {
            return query;
        }

        var isDescending = string.Equals(sortDirection, "desc", StringComparison.OrdinalIgnoreCase);

        return sortBy.ToLowerInvariant() switch
        {
            "joinedatutc" =>
                DateTime.TryParse(lastValue, out var lastDate)
                    ? (isDescending
                        ? query.Where(member =>
                            member.JoinedAtUtc < lastDate ||
                            (member.JoinedAtUtc == lastDate && member.UserId < lastId.Value))
                        : query.Where(member =>
                            member.JoinedAtUtc > lastDate ||
                            (member.JoinedAtUtc == lastDate && member.UserId > lastId.Value)))
                    : query,
            "userid" =>
                Guid.TryParse(lastValue, out var lastUserId)
                    ? (isDescending
                        ? query.Where(member => member.UserId < lastUserId)
                        : query.Where(member => member.UserId > lastUserId))
                    : query,
            "role" => isDescending
                ? query.Where(member =>
                    member.Role.CompareTo(lastValue) < 0 ||
                    (member.Role == lastValue && member.UserId < lastId.Value))
                : query.Where(member =>
                    member.Role.CompareTo(lastValue) > 0 ||
                    (member.Role == lastValue && member.UserId > lastId.Value)),
            _ => query
        };
    }

    private static string EncodeToken(
        StokvelMemberResponse lastItem,
        string sortBy,
        string sortDirection,
        string? role)
    {
        var token = new PageToken(
            sortBy,
            sortDirection,
            role,
            sortBy switch
            {
                "joinedAtUtc" => lastItem.JoinedAtUtc.ToString("O"),
                "userId" => lastItem.UserId.ToString(),
                "role" => lastItem.Role,
                _ => string.Empty
            },
            sortBy is "joinedAtUtc" or "role"
                ? lastItem.UserId
                : null);

        var json = JsonSerializer.Serialize(token);
        return Convert.ToBase64String(Encoding.UTF8.GetBytes(json));
    }

    private static PageToken? ParseToken(
        string? pageToken,
        string sortBy,
        string sortDirection,
        string? role)
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
                !string.Equals(token.Role, role, StringComparison.OrdinalIgnoreCase))
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

    public async Task<bool> ExistsAsync(
        Guid stokvelId,
        Guid userId)
    {
        return await _dbContext.StokvelMembers
            .AsNoTracking()
            .AnyAsync(
                member =>
                    member.StokvelId == stokvelId &&
                    member.UserId == userId);
    }

    public async Task AddAsync(
        StokvelMember stokvelMember)
    {
        await _dbContext.StokvelMembers.AddAsync(stokvelMember);
        await _dbContext.SaveChangesAsync();
    }

    public async Task RemoveAsync(
        StokvelMember stokvelMember)
    {
        _dbContext.StokvelMembers.Remove(stokvelMember);
        await _dbContext.SaveChangesAsync();
    }

    private sealed record PageToken(
        string SortBy,
        string SortDirection,
        string? Role,
        string? LastValue,
        Guid? LastId);
}