namespace RondiTrack.DTOs.Contributions;

public sealed record ContributionProofResponse(
    Guid Id,
    Guid ContributionId,
    string FileName,
    string ContentType,
    long Length,
    DateTime UploadedAtUtc);
