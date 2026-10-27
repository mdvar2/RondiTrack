using System.Globalization;
using Microsoft.EntityFrameworkCore;
using RondiTrack.DTOs.Stokvels;
using RondiTrack.Infrastructure.Data;

namespace RondiTrack.Services;

public class StokvelDetailService(
    RondiTrackDbContext context,
    TimeProvider clock)
{
    // Callers must have passed the StokvelView policy first. Returns null when
    // the stokvel or the caller's membership no longer exists.
    public async Task<StokvelDetailResponse?> GetAsync(
        Guid stokvelId,
        Guid userId,
        CancellationToken cancellationToken)
    {
        var stokvel = await context.Stokvels
            .AsNoTracking()
            .Where(stokvel => stokvel.Id == stokvelId)
            .Select(stokvel => new
            {
                stokvel.Id,
                stokvel.Name,
                stokvel.ContributionAmount,
                stokvel.Version,
                MemberCount = context.StokvelMembers.Count(member => member.StokvelId == stokvel.Id),
                Role = context.StokvelMembers
                    .Where(member => member.StokvelId == stokvel.Id && member.UserId == userId)
                    .Select(member => member.Role)
                    .FirstOrDefault()
            })
            .FirstOrDefaultAsync(cancellationToken);

        if (stokvel?.Role is null)
            return null;

        // Same Johannesburg month and Open-first choice as MemberHomeService,
        // so the detail page and the dashboards agree on the current cycle.
        var johannesburgNow = TimeZoneInfo.ConvertTimeBySystemTimeZoneId(
            clock.GetUtcNow(), "Africa/Johannesburg");
        var period = johannesburgNow.ToString("yyyy-MM", CultureInfo.InvariantCulture);

        var cycle = await context.ContributionCycles
            .AsNoTracking()
            .Where(cycle => cycle.StokvelId == stokvelId && cycle.Period == period)
            .OrderByDescending(cycle => cycle.Status == "Open")
            .ThenBy(cycle => cycle.Id)
            .Select(cycle => new
            {
                cycle.Period,
                cycle.Status,
                cycle.EndDate,
                cycle.TargetAmount,
                MembersPaid = cycle.Contributions
                    .Select(contribution => contribution.UserId)
                    .Distinct()
                    .Count(),
                Collected = cycle.Contributions
                    .Sum(contribution => (decimal?)contribution.Amount) ?? 0m
            })
            .FirstOrDefaultAsync(cancellationToken);

        return new StokvelDetailResponse(
            stokvel.Id,
            stokvel.Name,
            stokvel.ContributionAmount,
            stokvel.MemberCount,
            stokvel.Version,
            ToCents(stokvel.ContributionAmount),
            stokvel.Role,
            cycle is null ? null : new StokvelCycleSummaryResponse(
                cycle.Period,
                cycle.Status,
                cycle.EndDate,
                cycle.MembersPaid,
                ToCents(cycle.Collected),
                ToCents(cycle.TargetAmount)));
    }

    private static long ToCents(decimal amount)
    {
        var cents = amount * 100m;
        if (cents != decimal.Truncate(cents))
            throw new InvalidOperationException("Money must have no more than two decimal places.");
        return checked((long)cents);
    }
}
