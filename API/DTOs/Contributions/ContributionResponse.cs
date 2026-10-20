
using RondiTrack.Domain.Entities;

namespace RondiTrack.DTOs.Contributions;

public record ContributionResponse(
    Guid Id,
    Guid StokvelId,
    Guid UserId,
    Guid ContributionCycleId,
    long Amount,
    string Status,
    DateTime RecordedAtUtc,
    uint Version)
{
    public static ContributionResponse FromEntity(
        Contribution contribution)
    {
        return new ContributionResponse(
            contribution.Id,
            contribution.StokvelId,
            contribution.UserId,
            contribution.ContributionCycleId,
            ToCents(contribution.Amount),
            contribution.Status,
            contribution.RecordedAtUtc,
            contribution.Version);
    }

    private static long ToCents(decimal amount)
    {
        var cents = amount * 100m;

        if (cents != decimal.Truncate(cents))
        {
            throw new InvalidOperationException(
                "Money must have no more than two decimal places.");
        }

        return checked((long)cents);
    }
}
