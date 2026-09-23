using RondiTrack.Models;

namespace RondiTrack.DTOs.Contributions;

public record ContributionResponse(
    Guid Id,
    Guid StokvelId,
    Guid UserId,
    decimal Amount,
    string Cycle,
    DateTime RecordedAtUtc
)
{
    public static ContributionResponse FromEntity(Contribution contribution)
    {
        return new ContributionResponse(
            contribution.Id,
            contribution.StokvelId,
            contribution.UserId,
            contribution.Amount,
            contribution.Cycle,
            contribution.RecordedAtUtc
        );
    }
}