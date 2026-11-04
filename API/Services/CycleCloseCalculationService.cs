using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using RondiTrack.DTOs.ContributionCycles;
using RondiTrack.Infrastructure.Data;

namespace RondiTrack.Services;

public sealed class CycleCloseCalculationService(RondiTrackDbContext db, TimeProvider clock)
{
    private static readonly TimeZoneInfo Johannesburg = TimeZoneInfo.FindSystemTimeZoneById("Africa/Johannesburg");

    public async Task<CycleClosePreviewResponse?> CalculateAsync(Guid stokvelId, Guid cycleId, CancellationToken cancellationToken)
    {
        var cycle = await db.ContributionCycles.AsNoTracking()
            .SingleOrDefaultAsync(x => x.Id == cycleId && x.StokvelId == stokvelId, cancellationToken);
        var stokvel = await db.Stokvels.AsNoTracking().SingleOrDefaultAsync(x => x.Id == stokvelId, cancellationToken);
        if (cycle is null || stokvel is null) return null;

        var members = await db.StokvelMembers.AsNoTracking()
            .Where(x => x.StokvelId == stokvelId)
            .Join(db.Users, member => member.UserId, user => user.Id, (member, user) => new { member.UserId, user.Name })
            .ToListAsync(cancellationToken);
        var contributions = await db.Contributions.AsNoTracking()
            .Where(x => x.ContributionCycleId == cycleId)
            .Select(x => new { x.UserId, x.Amount, x.Id })
            .ToListAsync(cancellationToken);
        var arrears = CycleArrearsCalculator.Calculate(
                stokvel.ContributionAmount,
                members.Select(member => new CycleMemberForCalculation(member.UserId, member.Name)),
                contributions.Select(payment => new CyclePaymentForCalculation(payment.UserId, payment.Amount)))
            .Select(arrear => new CycleArrearPreview(arrear.UserId, arrear.UserName, arrear.AmountCents))
            .ToList();
        var versionInput = $"{cycle.Version}|{string.Join("|", contributions.OrderBy(x => x.Id).Select(x => $"{x.Id}:{x.UserId}:{x.Amount}"))}";
        var version = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(versionInput)));
        var asOf = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(clock.GetUtcNow(), Johannesburg).DateTime);
        return new CycleClosePreviewResponse(cycle.Id, cycle.Period, asOf, version, arrears, arrears.Sum(x => x.AmountCents));
    }
}
