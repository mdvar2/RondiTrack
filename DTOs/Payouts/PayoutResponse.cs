using RondiTrack.Models;

namespace RondiTrack.DTOs.Payouts;

public record PayoutResponse(
    Guid Id,
    Guid StokvelId,
    Guid RecipientUserId,
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
            payout.RecipientUserId,
            payout.ContributionCycleId,
            payout.Amount,
            payout.PaidAtUtc);
    }
}