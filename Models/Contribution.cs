namespace RondiTrack.Models;

public class Contribution
{
    public Guid Id { get; private set; }

    public Guid StokvelId { get; private set; }

    public Guid UserId { get; private set; }

    public Guid ContributionCycleId { get; private set; }

    public decimal Amount { get; private set; }

    public DateTime RecordedAtUtc { get; private set; }

    public Contribution(
        Guid stokvelId,
        Guid userId,
        Guid contributionCycleId,
        decimal amount)
    {
        if (stokvelId == Guid.Empty)
            throw new ArgumentException(
                "Stokvel ID is required.");

        if (userId == Guid.Empty)
            throw new ArgumentException(
                "User ID is required.");

        if (contributionCycleId == Guid.Empty)
            throw new ArgumentException(
                "Contribution cycle ID is required.");

        if (amount <= 0)
            throw new ArgumentException(
                "Contribution amount must be greater than zero.");

        Id = Guid.NewGuid();
        StokvelId = stokvelId;
        UserId = userId;
        ContributionCycleId = contributionCycleId;
        Amount = amount;
        RecordedAtUtc = DateTime.UtcNow;
    }
}