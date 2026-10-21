
using System.Linq;
using RondiTrack.Domain.Entities;

namespace RondiTrack.DTOs.ContributionCycles;

public record ContributionCycleResponse(
    Guid Id,
    Guid StokvelId,
    string Period,
    decimal TargetAmount,
    uint Version,
    DateOnly? EndDate,
    string Status,
    DateOnly? DueDate,
    long TotalCollectedCents,
    long AmountDueCents)
{
    public static ContributionCycleResponse FromEntity(
        ContributionCycle cycle)
    {
        var collected = cycle.Contributions
            .Where(contribution =>
                contribution.Status is "Paid" or "Confirmed")
            .Sum(contribution => contribution.Amount);

        var amountDue = Math.Max(
            0m,
            cycle.TargetAmount - collected);

        return new ContributionCycleResponse(
            cycle.Id,
            cycle.StokvelId,
            cycle.Period,
            cycle.TargetAmount,
            cycle.Version,
            cycle.EndDate,
            cycle.Status,
            cycle.EndDate,
            checked((long)(collected * 100m)),
            checked((long)(amountDue * 100m)));
    }
}
