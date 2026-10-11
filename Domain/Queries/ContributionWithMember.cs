namespace RondiTrack.Domain.Queries;

public record ContributionWithMember(
    Guid Id,
    Guid StokvelId,
    Guid UserId,
    string UserName,
    string UserEmail,
    string MemberRole,
    Guid ContributionCycleId,
    decimal Amount,
    DateTime RecordedAtUtc,
    uint Version);
