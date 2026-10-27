namespace RondiTrack.DTOs.Stokvels;

// The first five fields match StokvelResponse so existing callers keep working.
// The rest is worked out for the caller: their role here and the current cycle.
public record StokvelDetailResponse(
    Guid Id,
    string Name,
    decimal ContributionAmount,
    int MemberCount,
    uint Version,
    long ContributionAmountCents,
    string Role,
    StokvelCycleSummaryResponse? CurrentCycle);

public record StokvelCycleSummaryResponse(
    string Period,
    string Status,
    DateOnly? DueDate,
    int MembersPaid,
    long CollectedCents,
    long TargetCents);
