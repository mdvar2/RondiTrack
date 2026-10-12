using Microsoft.AspNetCore.Identity;
using RondiTrack.Domain.Entities;
using RondiTrack.Domain.Exceptions;
using RondiTrack.Domain.Repositories;
using RondiTrack.DTOs.Auth;
using RondiTrack.DTOs.Users;
using RondiTrack.Observability;

namespace RondiTrack.Auth;

public class AuthService(IUserRepository users, IPasswordHasher<User> passwordHasher,
    TokenService tokens, ILogger<AuthService> logger, IRefreshTokenRepository refreshTokens, JwtOptions options)
{
    private static readonly User DummyUser = new("Unknown user", "dummy@invalid.example");
    private static readonly string DummyHash = new PasswordHasher<User>().HashPassword(
        DummyUser, Convert.ToBase64String(System.Security.Cryptography.RandomNumberGenerator.GetBytes(32)));

    public async Task<AuthSessionResult> LoginAsync(LoginRequest request)
    {
        var user = await users.GetByNormalizedEmailAsync(User.NormalizeEmail(request.Email));
        var verification = passwordHasher.VerifyHashedPassword(user ?? DummyUser,
            user?.PasswordHash ?? DummyHash, request.Password);
        if (user?.PasswordHash is null || verification == PasswordVerificationResult.Failed)
        {
            logger.LogInformation("Security event {EventType} with {Outcome}. CorrelationId: {CorrelationId}",
                "sign_in", "failure", SecurityEvents.CorrelationId);
            throw new AuthenticationFailedException("Email or password is incorrect.");
        }
        if (verification == PasswordVerificationResult.SuccessRehashNeeded)
        {
            user.SetPasswordHash(passwordHasher.HashPassword(user, request.Password));
            await users.UpdateAsync(user);
        }
        var now = DateTime.UtcNow;
        var refreshToken = CreateRefreshToken();
        var expires = now.AddDays(options.RefreshTokenDays);
        await refreshTokens.StartSessionAsync(user.Id, HashRefreshToken(refreshToken), now, expires);
        logger.LogInformation("Security event {EventType} with {Outcome}. CorrelationId: {CorrelationId}",
            "sign_in", "success", SecurityEvents.CorrelationId);
        return new(tokens.CreateAccessToken(user), refreshToken, expires);
    }

    public async Task<AuthSessionResult> RefreshAsync(string? refreshToken)
    {
        if (string.IsNullOrEmpty(refreshToken)) throw InvalidRefresh();
        var successor = CreateRefreshToken();
        var now = DateTime.UtcNow;
        var expires = now.AddDays(options.RefreshTokenDays);
        var result = await refreshTokens.RotateAsync(HashRefreshToken(refreshToken), HashRefreshToken(successor), now, expires);
        if (result.UserId is null)
        {
            if (result.ReuseDetected)
                logger.LogWarning("Security event {EventType}; session revoked. CorrelationId: {CorrelationId}",
                    "refresh_reuse", SecurityEvents.CorrelationId);
            throw InvalidRefresh();
        }
        var user = await users.GetByIdReadOnlyAsync(result.UserId.Value);
        if (user is null) throw InvalidRefresh();
        logger.LogInformation("Security event {EventType} with {Outcome}. CorrelationId: {CorrelationId}",
            "refresh", "success", SecurityEvents.CorrelationId);
        return new(tokens.CreateAccessToken(user), successor, expires);
    }

    public async Task LogoutAsync(string? refreshToken)
    {
        if (!string.IsNullOrEmpty(refreshToken))
            await refreshTokens.RevokeSessionAsync(HashRefreshToken(refreshToken), DateTime.UtcNow);
        logger.LogInformation("Security event {EventType} completed. CorrelationId: {CorrelationId}",
            "logout", SecurityEvents.CorrelationId);
    }

    private static string CreateRefreshToken() => Convert.ToBase64String(System.Security.Cryptography.RandomNumberGenerator.GetBytes(64));
    private static string HashRefreshToken(string token) => Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(token)));
    private static AuthenticationFailedException InvalidRefresh() => new("Refresh session is invalid. Please sign in again.");

    public async Task<UserResponse> RegisterAsync(RegisterRequest request)
    {
        if (await users.GetByNormalizedEmailAsync(User.NormalizeEmail(request.Email)) is not null)
            throw new BusinessRuleException("An account with this email already exists.");
        var user = new User(request.Name, request.Email);
        user.SetPasswordHash(passwordHasher.HashPassword(user, request.Password));
        await users.AddAsync(user);
        return UserResponse.FromEntity(user);
    }
}

public record AuthSessionResult(AccessTokenResponse Access, string RefreshToken, DateTime RefreshExpiresAtUtc);
