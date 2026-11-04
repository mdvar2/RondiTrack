namespace RondiTrack.Domain.Entities;

public class StokvelInvite
{
    public string Code { get; private set; } = null!;
    public Guid StokvelId { get; private set; }
    public DateTime ExpiresAtUtc { get; private set; }
    public Stokvel Stokvel { get; private set; } = null!;

    private StokvelInvite() { }

    public StokvelInvite(Guid stokvelId, DateTime expiresAtUtc)
    {
        if (stokvelId == Guid.Empty) throw new ArgumentException("Stokvel ID is required.");
        Code = Convert.ToHexString(Guid.NewGuid().ToByteArray()).ToLowerInvariant();
        StokvelId = stokvelId;
        ExpiresAtUtc = expiresAtUtc;
    }

    public bool IsExpired(DateTime nowUtc) => ExpiresAtUtc <= nowUtc;
}
