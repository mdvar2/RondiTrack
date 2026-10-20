
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
    long Amount,
    string Status,
    DateTime RecordedAtUtc,
    uint Version)
{
    public static ContributionWithMemberResponse FromQuery(
        RondiTrack.Domain.Queries.ContributionWithMember row)
    {
        return new ContributionWithMemberResponse(
            row.Id,
            row.StokvelId,
            row.UserId,
            row.UserName,
            row.UserEmail,
            row.MemberRole,
            row.ContributionCycleId,
            ToCents(row.Amount),
            row.Status,
            row.RecordedAtUtc,
            row.Version);
    }

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
            ToCents(contribution.Amount),
            contribution.Status,
            contribution.RecordedAtUtc,
            contribution.Version);
    }

    private static long ToCents(decimal amount)
    {
        var cents = amount * 100m;

        if (cents != decimal.Truncate(cents))
        {
            throw new InvalidOperationException(
                "Money must have no more than two decimal places.");
        }

        return checked((long)cents);
    }
}
