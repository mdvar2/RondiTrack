namespace RondiTrack.DTOs.ContributionCycles;

public record UpdateContributionCycleRequest(
    string Period,
    decimal TargetAmount,
    DateOnly? EndDate = null
);