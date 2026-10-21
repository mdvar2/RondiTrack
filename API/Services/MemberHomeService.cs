using System.Globalization;
using Microsoft.EntityFrameworkCore;
using RondiTrack.DTOs.Home;
using RondiTrack.Infrastructure.Data;

namespace RondiTrack.Services;

public class MemberHomeService(
    RondiTrackDbContext context,
    TimeProvider clock)
{
    public async Task<HomeResponse> GetAsync(
        Guid userId,
        CancellationToken cancellationToken)
    {
        // The cycle-close job uses Johannesburg calendar days. Keep home aligned.
        var johannesburgNow = TimeZoneInfo.ConvertTimeBySystemTimeZoneId(
            clock.GetUtcNow(), "Africa/Johannesburg");
        var period = johannesburgNow.ToString("yyyy-MM", CultureInfo.InvariantCulture);

        var memberships = await context.StokvelMembers
            .AsNoTracking()
            .Where(member => member.UserId == userId)
            .OrderBy(member => member.Stokvel.Name)
            .ThenBy(member => member.StokvelId)
            .Select(member => new
            {
                member.StokvelId,
                member.Role,
                member.Stokvel.Name,
                member.Stokvel.ContributionAmount
            })
            .ToListAsync(cancellationToken);

        if (memberships.Count == 0)
            return new HomeResponse([]);

        var ids = memberships.Select(member => member.StokvelId).ToArray();

        var cycles = await context.ContributionCycles
            .AsNoTracking()
            .Where(cycle => ids.Contains(cycle.StokvelId) && cycle.Period == period)
            .Select(cycle => new
            {
                cycle.Id,
                cycle.StokvelId,
                cycle.Period,
                cycle.Status,
                cycle.EndDate,
                Paid = cycle.Contributions
                    .Where(contribution => contribution.UserId == userId)
                    .Sum(contribution => (decimal?)contribution.Amount) ?? 0m
            })
            .ToListAsync(cancellationToken);

        // The server is authoritative for arrears. Never reconstruct them from
        // displayed contributions or expose another member's shortfall.
        var arrears = await context.ContributionArrears
            .AsNoTracking()
            .Where(arrear => arrear.UserId == userId && ids.Contains(arrear.StokvelId))
            .GroupBy(arrear => arrear.StokvelId)
            .Select(group => new
            {
                StokvelId = group.Key,
                Cents = group.Sum(arrear => arrear.ShortfallCents)
            })
            .ToDictionaryAsync(item => item.StokvelId, item => item.Cents, cancellationToken);

        var stokvels = memberships.Select(member =>
        {
            var cycle = cycles
                .Where(cycle => cycle.StokvelId == member.StokvelId)
                .OrderByDescending(cycle => cycle.Status == "Open")
                .ThenBy(cycle => cycle.Id)
                .FirstOrDefault();

            var paid = cycle?.Paid ?? 0m;
            var owed = cycle is null ? 0m : Math.Max(0m, member.ContributionAmount - paid);

            return new HomeStokvelResponse(
                member.StokvelId,
                member.Name,
                member.Role,
                cycle is null ? null : new HomeCycleResponse(
                    cycle.Period, cycle.Status, cycle.EndDate),
                ToCents(paid),
                ToCents(owed),
                NextPayoutDate: null,
                ArrearsCents: arrears.GetValueOrDefault(member.StokvelId));
        }).ToList();

        return new HomeResponse(stokvels);
    }

    private static long ToCents(decimal amount)
    {
        var cents = amount * 100m;
        if (cents != decimal.Truncate(cents))
            throw new InvalidOperationException("Money must have no more than two decimal places.");
        return checked((long)cents);
    }
}
