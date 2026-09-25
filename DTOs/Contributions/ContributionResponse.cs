using RondiTrack.Models;

namespace RondiTrack.DTOs.Contributions;

public record ContributionResponse(
    Guid Id,
    Guid StokvelId,
    Guid UserId,
    Guid ContributionCycleId,
    decimal Amount,
    DateTime RecordedAtUtc)
{
    public static ContributionResponse FromEntity(
        Contribution contribution)
    {
        return new ContributionResponse(
            contribution.Id,
            contribution.StokvelId,
            contribution.UserId,
            contribution.ContributionCycleId,
            contribution.Amount,
            contribution.RecordedAtUtc);
    }
}