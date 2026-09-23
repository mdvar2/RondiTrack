using RondiTrack.Models;

namespace RondiTrack.DTOs.Stokvels;

public record StokvelResponse(
    Guid Id,
    string Name,
    decimal ContributionAmount,
    int MemberCount
)
{
    public static StokvelResponse FromEntity(Stokvel stokvel)
    {
        return new StokvelResponse(
            stokvel.Id,
            stokvel.Name,
            stokvel.ContributionAmount,
            stokvel.Members.Count
        );
    }
}
