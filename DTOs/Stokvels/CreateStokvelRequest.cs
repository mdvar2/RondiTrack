namespace RondiTrack.DTOs.Stokvels;

public record CreateStokvelRequest(
    string Name,
    decimal ContributionAmount
);
