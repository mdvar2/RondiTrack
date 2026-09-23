namespace RondiTrack.Models;

public class Contribution
{
    public Guid Id { get; private set; }
    public Guid StokvelId { get; private set; }
    public Guid UserId { get; private set; }
    public decimal Amount { get; private set; }
    public string Cycle { get; private set; }
    public DateTime RecordedAtUtc { get; private set; }

    public Contribution(
        Guid stokvelId,
        Guid userId,
        decimal amount,
        string cycle)
    {
        if (stokvelId == Guid.Empty)
            throw new ArgumentException("Stokvel ID is required.");

        if (userId == Guid.Empty)
            throw new ArgumentException("User ID is required.");

        if (amount <= 0)
            throw new ArgumentException(
                "Contribution amount must be greater than zero.");

        if (string.IsNullOrWhiteSpace(cycle))
            throw new ArgumentException("Contribution cycle is required.");

        Id = Guid.NewGuid();
        StokvelId = stokvelId;
        UserId = userId;
        Amount = amount;
        Cycle = cycle.Trim();
        RecordedAtUtc = DateTime.UtcNow;
    }
}
