using RondiTrack.Models;

namespace RondiTrack.DTOs.ContributionCycles;

public record ContributionCycleResponse(
    Guid Id,
    Guid StokvelId,
    string Period,
    decimal TargetAmount)
{
    public static ContributionCycleResponse FromEntity(
        ContributionCycle cycle)
    {
        return new ContributionCycleResponse(
            cycle.Id,
            cycle.StokvelId,
            cycle.Period,
            cycle.TargetAmount);
    }
}