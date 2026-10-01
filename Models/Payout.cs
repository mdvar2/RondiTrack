namespace RondiTrack.Models;

public class Payout
{
    public Guid Id { get; private set; }

    public Guid StokvelId { get; private set; }

    public Guid StokvelMemberId { get; private set; }

    public Guid ContributionCycleId { get; private set; }

    public decimal Amount { get; private set; }

    public DateTime PaidAtUtc { get; private set; }

    public Payout(
        Guid stokvelId,
        Guid stokvelMemberId,
        Guid contributionCycleId,
        decimal amount)
    {
        if (stokvelId == Guid.Empty)
            throw new ArgumentException(
                "Stokvel ID is required.");

        if (stokvelMemberId == Guid.Empty)
            throw new ArgumentException(
                "Stokvel member ID is required.");

        if (contributionCycleId == Guid.Empty)
            throw new ArgumentException(
                "Contribution cycle ID is required.");

        if (amount <= 0)
            throw new ArgumentException(
                "Payout amount must be greater than zero.");

        Id = Guid.NewGuid();
        StokvelId = stokvelId;
        StokvelMemberId = stokvelMemberId;
        ContributionCycleId = contributionCycleId;
        Amount = amount;
        PaidAtUtc = DateTime.UtcNow;
    }
}