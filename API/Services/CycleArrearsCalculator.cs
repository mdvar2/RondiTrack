namespace RondiTrack.Services;

public sealed record CycleMemberForCalculation(Guid UserId, string UserName);

public sealed record CyclePaymentForCalculation(Guid UserId, decimal Amount);

public sealed record CycleArrearCalculation(Guid UserId, string UserName, long AmountCents);

public static class CycleArrearsCalculator
{
    public static IReadOnlyList<CycleArrearCalculation> Calculate(
        decimal contributionAmount,
        IEnumerable<CycleMemberForCalculation> members,
        IEnumerable<CyclePaymentForCalculation> payments)
    {
        var paidByMember = payments
            .GroupBy(payment => payment.UserId)
            .ToDictionary(group => group.Key, group => group.Sum(payment => payment.Amount));

        return members
            .Select(member =>
            {
                var shortfall = Math.Max(0m, contributionAmount - paidByMember.GetValueOrDefault(member.UserId));
                var cents = checked((long)decimal.Round(
                    shortfall * 100m,
                    0,
                    MidpointRounding.AwayFromZero));
                return new CycleArrearCalculation(member.UserId, member.UserName, cents);
            })
            .Where(arrear => arrear.AmountCents > 0)
            .ToList();
    }
}
