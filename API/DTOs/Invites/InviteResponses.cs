namespace RondiTrack.DTOs.Invites;

public sealed record CreateInviteResponse(string Code, DateTime ExpiresAtUtc);

public sealed record InvitePreviewResponse(
    Guid StokvelId,
    string Name,
    long ContributionAmountCents,
    string Schedule,
    string Rules,
    int MemberCount);
