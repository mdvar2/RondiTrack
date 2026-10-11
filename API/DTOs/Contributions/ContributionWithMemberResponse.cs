using RondiTrack.Domain.Entities;

namespace RondiTrack.DTOs.Contributions;

public record ContributionWithMemberResponse(
    Guid Id,
    Guid StokvelId,
    Guid UserId,
    string UserName,
    string UserEmail,
    string MemberRole,
    Guid ContributionCycleId,
    decimal Amount,
    DateTime RecordedAtUtc,
    uint Version)
{
    public static ContributionWithMemberResponse FromQuery(RondiTrack.Domain.Queries.ContributionWithMember row) =>
        new(row.Id, row.StokvelId, row.UserId, row.UserName, row.UserEmail, row.MemberRole, row.ContributionCycleId, row.Amount, row.RecordedAtUtc, row.Version);

    public static ContributionWithMemberResponse FromEntity(
        Contribution contribution)
    {
        return new ContributionWithMemberResponse(
            contribution.Id,
            contribution.StokvelId,
            contribution.UserId,
            contribution.Member.User.Name,
            contribution.Member.User.Email,
            contribution.Member.Role,
            contribution.ContributionCycleId,
            contribution.Amount,
            contribution.RecordedAtUtc,
            contribution.Version);
    }
}