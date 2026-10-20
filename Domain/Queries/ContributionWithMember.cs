
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
    string Status,
    DateTime RecordedAtUtc,
    uint Version);
