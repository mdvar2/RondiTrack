
namespace RondiTrack.Domain.Entities;

public class ContributionCycle
{
    private readonly List<Contribution> _contributions = new();

    public Guid Id { get; private set; }

    public uint Version { get; private set; }

    public Guid StokvelId { get; private set; }

    public string Period { get; private set; }

    public decimal TargetAmount { get; private set; }

    public DateOnly? EndDate { get; private set; }

    public string Status { get; private set; }

    public IReadOnlyCollection<Contribution> Contributions =>
        _contributions.AsReadOnly();

    public ContributionCycle(
        Guid stokvelId,
        string period,
        decimal targetAmount,
        DateOnly? endDate = null)
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
        EndDate = endDate;
        Status = "Open";
    }

    public void UpdateDetails(
        string period,
        decimal targetAmount,
        DateOnly? endDate = null)
    {
        if (string.IsNullOrWhiteSpace(period))
            throw new ArgumentException(
                "Contribution cycle period is required.");

        if (targetAmount <= 0)
            throw new ArgumentException(
                "Target amount must be greater than zero.");

        Period = period.Trim();
        TargetAmount = targetAmount;
        EndDate = endDate;
    }

    public void Close()
    {
        if (Status != "Open")
            throw new InvalidOperationException(
                "Only an open contribution cycle can be closed.");

        Status = "Closed";
    }

    public void MarkPaidOut()
    {
        Status = "PaidOut";
    }
}
