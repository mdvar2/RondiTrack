using RondiTrack.Models;

namespace RondiTrack.DTOs.Stokvels;

public record StokvelMemberResponse(
    Guid StokvelId,
    Guid UserId,
    string UserName,
    string UserEmail,
    string Role,
    DateTime JoinedAtUtc,
    uint Version)
{
    public static StokvelMemberResponse FromEntity(
        StokvelMember member)
    {
        return new StokvelMemberResponse(
            member.StokvelId,
            member.UserId,
            member.User.Name,
            member.User.Email,
            member.Role,
            member.JoinedAtUtc,
            member.Version);
    }
}