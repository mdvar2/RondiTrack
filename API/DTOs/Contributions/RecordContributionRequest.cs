namespace RondiTrack.DTOs.Contributions;

public record RecordContributionRequest(
    long AmountInCents,
    Guid ContributionCycleId
);