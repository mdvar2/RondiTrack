using Microsoft.AspNetCore.Identity;
using RondiTrack.Domain.Entities;
using RondiTrack.Domain.Exceptions;
using RondiTrack.Domain.Repositories;
using RondiTrack.DTOs.Auth;
using RondiTrack.DTOs.Users;

namespace RondiTrack.Auth;

public class AuthService(IUserRepository users, IPasswordHasher<User> passwordHasher,
    TokenService tokens, ILogger<AuthService> logger)
{
    private static readonly User DummyUser = new("Unknown user", "dummy@invalid.example");
    private static readonly string DummyHash = new PasswordHasher<User>().HashPassword(
        DummyUser, Convert.ToBase64String(System.Security.Cryptography.RandomNumberGenerator.GetBytes(32)));

    public async Task<AccessTokenResponse> LoginAsync(LoginRequest request)
    {
        var user = await users.GetByNormalizedEmailAsync(User.NormalizeEmail(request.Email));
        var verification = passwordHasher.VerifyHashedPassword(user ?? DummyUser,
            user?.PasswordHash ?? DummyHash, request.Password);
        if (user?.PasswordHash is null || verification == PasswordVerificationResult.Failed)
            throw new AuthenticationFailedException("Email or password is incorrect.");
        if (verification == PasswordVerificationResult.SuccessRehashNeeded)
        {
            user.SetPasswordHash(passwordHasher.HashPassword(user, request.Password));
            await users.UpdateAsync(user);
        }
        logger.LogInformation("Login succeeded. UserId: {UserId}", user.Id);
        return tokens.CreateAccessToken(user);
    }

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
