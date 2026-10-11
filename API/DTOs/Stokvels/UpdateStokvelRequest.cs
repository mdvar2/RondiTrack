namespace RondiTrack.DTOs.Stokvels;

public record UpdateStokvelRequest(
    string Name,
    decimal ContributionAmount
);
