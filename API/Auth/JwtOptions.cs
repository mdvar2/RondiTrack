using Microsoft.IdentityModel.Tokens;

namespace RondiTrack.Auth;

public sealed record JwtOptions(string Issuer, string Audience, byte[] SigningKey,
    int AccessTokenMinutes, int ClockSkewSeconds, int RefreshTokenDays)
{
    public static JwtOptions Load(IConfiguration configuration)
    {
        const string guidance = "Configure Jwt:SigningKey in User Secrets as a base64-encoded random key of at least 32 bytes: dotnet user-secrets --project API set Jwt:SigningKey <BASE64_KEY>.";
        byte[] key;
        try { key = Convert.FromBase64String(configuration["Jwt:SigningKey"] ?? ""); }
        catch (FormatException) { throw new InvalidOperationException(guidance); }
        if (key.Length < 32) throw new InvalidOperationException(guidance);
        var issuer = configuration["Jwt:Issuer"];
        var audience = configuration["Jwt:Audience"];
        if (string.IsNullOrWhiteSpace(issuer) || string.IsNullOrWhiteSpace(audience))
            throw new InvalidOperationException("Configure the non-secret Jwt:Issuer and Jwt:Audience settings.");
        var minutes = configuration.GetValue("Jwt:AccessTokenMinutes", 5);
        var skew = configuration.GetValue("Jwt:ClockSkewSeconds", 30);
        var days = configuration.GetValue("Jwt:RefreshTokenDays", 7);
        if (minutes is < 1 or > 15 || skew is < 0 or > 60 || days is < 1 or > 30)
            throw new InvalidOperationException("JWT settings require access lifetime 1?15 minutes, ClockSkew 0?60 seconds and refresh lifetime 1?30 days.");
        return new(issuer, audience, key, minutes, skew, days);
    }

    public TokenValidationParameters ValidationParameters => new()
    {
        ValidateIssuerSigningKey = true,
        IssuerSigningKey = new SymmetricSecurityKey(SigningKey),
        RequireSignedTokens = true,
        ValidAlgorithms = [SecurityAlgorithms.HmacSha256],
        ValidateIssuer = true, ValidIssuer = Issuer,
        ValidateAudience = true, ValidAudience = Audience,
        ValidateLifetime = true, RequireExpirationTime = true,
        ClockSkew = TimeSpan.FromSeconds(ClockSkewSeconds),
        NameClaimType = "sub", RoleClaimType = "platform_role"
    };
}
