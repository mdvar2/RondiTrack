namespace RondiTrack.DTOs.Contributions;

public record RecordContributionRequest(
    decimal Amount,
    Guid ContributionCycleId
);