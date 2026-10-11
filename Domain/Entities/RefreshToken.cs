namespace RondiTrack.Domain.Entities;

public class RefreshToken
{
    public Guid Id { get; private set; } = Guid.NewGuid();
    public Guid FamilyId { get; private set; }
    public string TokenHash { get; private set; } = null!;
    public DateTime CreatedAtUtc { get; private set; }
    public DateTime ExpiresAtUtc { get; private set; }
    public DateTime? UsedAtUtc { get; private set; }
    public Guid? ReplacedByTokenId { get; private set; }

    private RefreshToken() { }
    public RefreshToken(Guid familyId, string tokenHash, DateTime now, DateTime expires)
    {
        FamilyId = familyId;
        TokenHash = tokenHash;
        CreatedAtUtc = now;
        ExpiresAtUtc = expires;
    }
}
