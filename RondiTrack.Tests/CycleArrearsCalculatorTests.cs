using RondiTrack.Services;

namespace RondiTrack.Tests;

public sealed class CycleArrearsCalculatorTests
{
    [Fact]
    public void ManualAndScheduledInputsProduceTheSameIntegerCents()
    {
        var members = new[]
        {
            new CycleMemberForCalculation(Guid.NewGuid(), "Unpaid"),
            new CycleMemberForCalculation(Guid.NewGuid(), "Partial"),
            new CycleMemberForCalculation(Guid.NewGuid(), "Paid"),
        };
        var payments = new[]
        {
            new CyclePaymentForCalculation(members[1].UserId, 250m),
            new CyclePaymentForCalculation(members[2].UserId, 500m),
        };

        var result = CycleArrearsCalculator.Calculate(500m, members, payments);

        Assert.Equal(
            new[] { 50000L, 25000L },
            result.Select(arrear => arrear.AmountCents));
        Assert.Equal(75000L, result.Sum(arrear => arrear.AmountCents));
    }

    [Fact]
    public void FullyPaidMembersDoNotEnterArrears()
    {
        var member = new CycleMemberForCalculation(Guid.NewGuid(), "Paid");
        var result = CycleArrearsCalculator.Calculate(
            500m,
            [member],
            [new CyclePaymentForCalculation(member.UserId, 500m)]);

        Assert.Empty(result);
    }
}
