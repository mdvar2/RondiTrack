using System.Globalization;
using Microsoft.EntityFrameworkCore;
using RondiTrack.DTOs.Home;
using RondiTrack.Infrastructure.Data;

namespace RondiTrack.Services;

public class MemberHomeService(RondiTrackDbContext context, TimeProvider clock)
{
    public async Task<HomeResponse> GetAsync(Guid userId, CancellationToken cancellationToken)
    {
        var period = clock.GetUtcNow().ToString("yyyy-MM", CultureInfo.InvariantCulture);
        var memberships = await context.StokvelMembers.AsNoTracking()
            .Where(member => member.UserId == userId)
            .OrderBy(member => member.Stokvel.Name).ThenBy(member => member.StokvelId)
            .Select(member => new { member.StokvelId, member.Role, member.Stokvel.Name, member.Stokvel.ContributionAmount })
            .ToListAsync(cancellationToken);
        if (memberships.Count == 0) return new HomeResponse([]);

        var ids = memberships.Select(member => member.StokvelId).ToArray();
        var cycles = await context.ContributionCycles.AsNoTracking()
            .Where(cycle => ids.Contains(cycle.StokvelId) && cycle.Period == period)
            .Select(cycle => new
            {
                cycle.Id, cycle.StokvelId, cycle.Period, cycle.Status,
                Paid = cycle.Contributions.Where(contribution => contribution.UserId == userId)
                    .Sum(contribution => (decimal?)contribution.Amount) ?? 0m
            }).ToListAsync(cancellationToken);

        var stokvels = memberships.Select(member =>
        {
            // Historical data permits duplicate periods. Prefer an open cycle, then a stable ID.
            var cycle = cycles.Where(cycle => cycle.StokvelId == member.StokvelId)
                .OrderByDescending(cycle => cycle.Status == "Open").ThenBy(cycle => cycle.Id).FirstOrDefault();
            var paid = cycle?.Paid ?? 0m;
            var owed = cycle is null ? 0m : Math.Max(0m, member.ContributionAmount - paid);
            return new HomeStokvelResponse(member.Name, member.Role,
                cycle is null ? null : new HomeCycleResponse(cycle.Period, cycle.Status),
                Money(paid), Money(owed), NextPayoutDate: null);
        }).ToList();
        return new HomeResponse(stokvels);
    }

    // Decimal strings preserve the domain's full precision without JavaScript floating-point rounding.
    private static string Money(decimal amount) => amount.ToString("0.############################", CultureInfo.InvariantCulture);
}
