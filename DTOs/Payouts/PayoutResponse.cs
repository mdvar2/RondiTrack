using RondiTrack.Models;

namespace RondiTrack.DTOs.Payouts;

public record PayoutResponse(
    Guid Id,
    Guid StokvelId,
    Guid StokvelMemberId,
    Guid ContributionCycleId,
    decimal Amount,
    DateTime PaidAtUtc)
{
    public static PayoutResponse FromEntity(
        Payout payout)
    {
        return new PayoutResponse(
            payout.Id,
            payout.StokvelId,
            payout.StokvelMemberId,
            payout.ContributionCycleId,
            payout.Amount,
            payout.PaidAtUtc);
    }
}