namespace RondiTrack.Models;

public class StokvelMember
{
    public Guid StokvelId { get; private set; }

    public Guid UserId { get; private set; }

    public uint Version { get; private set; }

    public string Role { get; private set; }

    public DateTime JoinedAtUtc { get; private set; }

    // Navigation properties for the real relationships.
    public Stokvel Stokvel { get; private set; } = null!;

    public User User { get; private set; } = null!;

    private StokvelMember()
    {
        Role = null!;
    }

    public StokvelMember(
        Guid stokvelId,
        Guid userId,
        string role = "Member")
    {
        if (stokvelId == Guid.Empty)
            throw new ArgumentException(
                "Stokvel ID is required.");

        if (userId == Guid.Empty)
            throw new ArgumentException(
                "User ID is required.");

        if (string.IsNullOrWhiteSpace(role))
            throw new ArgumentException(
                "Role is required.");

        StokvelId = stokvelId;
        UserId = userId;
        Role = role.Trim();
        JoinedAtUtc = DateTime.UtcNow;
    }
}