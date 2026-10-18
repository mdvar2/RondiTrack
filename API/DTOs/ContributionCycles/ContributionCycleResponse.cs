
using RondiTrack.Domain.Entities;

namespace RondiTrack.DTOs.ContributionCycles;

public record ContributionCycleResponse(
    Guid Id,
    Guid StokvelId,
    string Period,
    decimal TargetAmount,
    uint Version,
    DateOnly? EndDate = null)
{
    public static ContributionCycleResponse FromEntity(
        ContributionCycle cycle)
    {
        return new ContributionCycleResponse(
            cycle.Id,
            cycle.StokvelId,
            cycle.Period,
            cycle.TargetAmount,
            cycle.Version,
            cycle.EndDate);
    }
}
