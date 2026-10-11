namespace RondiTrack.Domain.Queries;

public record StokvelMemberDetails(
    Guid StokvelId,
    Guid UserId,
    string UserName,
    string UserEmail,
    string Role,
    DateTime JoinedAtUtc,
    uint Version);
