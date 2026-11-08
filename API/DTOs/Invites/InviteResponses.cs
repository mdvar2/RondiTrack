namespace RondiTrack.DTOs.Invites;

public sealed record CreateInviteResponse(string Code, DateTime ExpiresAtUtc);

public sealed record InvitePreviewResponse(
    Guid StokvelId,
    string Name,
    long ContributionAmountCents,
    string Schedule,
    string Rules,
    int MemberCount);

// Only what a signed-out link preview may know: "valid" or "expired".
public sealed record InviteStatusResponse(string Status);
