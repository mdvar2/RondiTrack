namespace RondiTrack.Domain.Repositories;

public record RefreshRotationResult(Guid? UserId, Guid FamilyId, bool ReuseDetected);

public interface IRefreshTokenRepository
{
    Task StartSessionAsync(Guid userId, string tokenHash, DateTime now, DateTime expires);
    Task<RefreshRotationResult> RotateAsync(string tokenHash, string successorHash, DateTime now, DateTime expires);
    Task RevokeSessionAsync(string tokenHash, DateTime now);
}
