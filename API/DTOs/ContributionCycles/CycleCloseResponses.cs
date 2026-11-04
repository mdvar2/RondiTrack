namespace RondiTrack.DTOs.ContributionCycles;

public sealed record CycleArrearPreview(Guid UserId, string UserName, long AmountCents);

public sealed record CycleClosePreviewResponse(
    Guid CycleId,
    string Period,
    DateOnly AsOfDate,
    string Version,
    IReadOnlyList<CycleArrearPreview> Arrears,
    long TotalCents);

public sealed record CloseCycleRequest(string Version);

public sealed record CloseCycleResponse(
    Guid CycleId,
    string Period,
    DateOnly AsOfDate,
    IReadOnlyList<CycleArrearPreview> Arrears,
    long TotalCents);
