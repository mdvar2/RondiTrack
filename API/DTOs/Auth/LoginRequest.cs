namespace RondiTrack.DTOs.Auth;

public record LoginRequest(string Email, string Password);
public record AccessTokenResponse(string AccessToken, DateTime ExpiresAtUtc);
