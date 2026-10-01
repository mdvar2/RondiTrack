namespace RondiTrack.Models;

public class StokvelMember
{
    public Guid Id { get; private set; }

    public Guid StokvelId { get; private set; }

    public Guid UserId { get; private set; }

    public DateTime JoinedAtUtc { get; private set; }

    public StokvelMember(
        Guid stokvelId,
        Guid userId)
    {
        if (stokvelId == Guid.Empty)
            throw new ArgumentException(
                "Stokvel ID is required.");

        if (userId == Guid.Empty)
            throw new ArgumentException(
                "User ID is required.");

        Id = Guid.NewGuid();
        StokvelId = stokvelId;
        UserId = userId;
        JoinedAtUtc = DateTime.UtcNow;
    }
}