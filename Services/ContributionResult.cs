using RondiTrack.DTOs.Contributions;

namespace RondiTrack.Services;

public enum ContributionOutcome
{
    Created,
    Replayed,
    StokvelNotFound,
    UserNotFound,
    NotMember,
    DuplicateContribution,
    InvalidAmount,
    InvalidCycle,
    IdempotencyKeyConflict
}

public record ContributionResult(
    ContributionOutcome Outcome,
    ContributionResponse? Response = null,
    int? OriginalStatusCode = null
);