using RondiTrack.Models;

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