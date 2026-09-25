namespace RondiTrack.Models;

public class ContributionCycle
{
    public Guid Id { get; private set; }

    public Guid StokvelId { get; private set; }

    public string Period { get; private set; }

    public decimal TargetAmount { get; private set; }

    public ContributionCycle(
        Guid stokvelId,
        string period,
        decimal targetAmount)
    {
        if (stokvelId == Guid.Empty)
            throw new ArgumentException(
                "Stokvel ID is required.");

        if (string.IsNullOrWhiteSpace(period))
            throw new ArgumentException(
                "Contribution cycle period is required.");

        if (targetAmount <= 0)
            throw new ArgumentException(
                "Target amount must be greater than zero.");

        Id = Guid.NewGuid();
        StokvelId = stokvelId;
        Period = period.Trim();
        TargetAmount = targetAmount;
    }

    public void UpdateDetails(
        string period,
        decimal targetAmount)
    {
        if (string.IsNullOrWhiteSpace(period))
            throw new ArgumentException(
                "Contribution cycle period is required.");

        if (targetAmount <= 0)
            throw new ArgumentException(
                "Target amount must be greater than zero.");

        Period = period.Trim();
        TargetAmount = targetAmount;
    }
}