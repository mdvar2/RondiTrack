
namespace RondiTrack.DTOs.Home;

public record HomeResponse(IReadOnlyList<HomeStokvelResponse> Stokvels);

public record HomeStokvelResponse(
    Guid StokvelId,
    string StokvelName,
    string Role,
    HomeCycleResponse? CurrentCycle,
    long AmountPaid,
    long AmountOwed,
    DateTimeOffset? NextPayoutDate);

public record HomeCycleResponse(
    string Period,
    string Status,
    DateOnly? DueDate);
