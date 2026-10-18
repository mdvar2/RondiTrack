namespace RondiTrack.DTOs.ContributionCycles;

public record CreateContributionCycleRequest(
    string Period,
    decimal TargetAmount,
    DateOnly? EndDate = null
);