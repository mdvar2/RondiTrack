namespace RondiTrack.Domain.Entities;

public class RefreshTokenFamily
{
    public Guid Id { get; private set; } = Guid.NewGuid();
    public Guid UserId { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }
    public DateTime ExpiresAtUtc { get; private set; }
    public DateTime? RevokedAtUtc { get; private set; }

    private RefreshTokenFamily() { }
    public RefreshTokenFamily(Guid userId, DateTime now)
    {
        UserId = userId;
        CreatedAtUtc = now;
        ExpiresAtUtc = now.AddDays(30);
    }
}
