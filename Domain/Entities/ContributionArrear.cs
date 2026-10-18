
namespace RondiTrack.Domain.Entities;

public class ContributionArrear
{
    public Guid Id { get; private set; }

    public Guid StokvelId { get; private set; }

    public Guid UserId { get; private set; }

    public Guid ContributionCycleId { get; private set; }

    public long ShortfallCents { get; private set; }

    public ContributionCycle Cycle { get; private set; } = null!;

    private ContributionArrear()
    {
    }

    public ContributionArrear(
        Guid stokvelId,
        Guid userId,
        Guid contributionCycleId,
        long shortfallCents)
    {
        if (stokvelId == Guid.Empty)
            throw new ArgumentException("Stokvel ID is required.");

        if (userId == Guid.Empty)
            throw new ArgumentException("User ID is required.");

        if (contributionCycleId == Guid.Empty)
            throw new ArgumentException("Contribution cycle ID is required.");

        if (shortfallCents <= 0)
            throw new ArgumentException(
                "Shortfall must be greater than zero cents.");

        Id = Guid.NewGuid();
        StokvelId = stokvelId;
        UserId = userId;
        ContributionCycleId = contributionCycleId;
        ShortfallCents = shortfallCents;
    }
}
