namespace RondiTrack.Models;

public class Payout
{
    public Guid Id { get; private set; }

    public uint Version { get; private set; }

    public Guid StokvelId { get; private set; }

    public Guid RecipientUserId { get; private set; }

    public Guid ContributionCycleId { get; private set; }

    public decimal Amount { get; private set; }

    public DateTime PaidAtUtc { get; private set; }

    // Navigation to the specific membership identified by
    // the composite key (UserId, StokvelId).
    public StokvelMember RecipientMember { get; private set; } = null!;

    public Payout(
        Guid stokvelId,
        Guid recipientUserId,
        Guid contributionCycleId,
        decimal amount)
    {
        if (stokvelId == Guid.Empty)
            throw new ArgumentException(
                "Stokvel ID is required.");

        if (recipientUserId == Guid.Empty)
            throw new ArgumentException(
                "Recipient user ID is required.");

        if (contributionCycleId == Guid.Empty)
            throw new ArgumentException(
                "Contribution cycle ID is required.");

        if (amount <= 0)
            throw new ArgumentException(
                "Payout amount must be greater than zero.");

        Id = Guid.NewGuid();
        StokvelId = stokvelId;
        RecipientUserId = recipientUserId;
        ContributionCycleId = contributionCycleId;
        Amount = amount;
        PaidAtUtc = DateTime.UtcNow;
    }

    public void UpdateAmount(decimal amount)
    {
        if (amount <= 0)
        {
            throw new ArgumentException(
                "Payout amount must be greater than zero.");
        }

        Amount = amount;
    }
}