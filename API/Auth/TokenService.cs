using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.IdentityModel.Tokens;
using RondiTrack.Domain.Entities;
using RondiTrack.DTOs.Auth;

namespace RondiTrack.Auth;

public class TokenService(JwtOptions options)
{
    public AccessTokenResponse CreateAccessToken(User user)
    {
        var now = DateTime.UtcNow;
        var expires = now.AddMinutes(options.AccessTokenMinutes);
        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString("N")),
            new(JwtRegisteredClaimNames.Iat, new DateTimeOffset(now).ToUnixTimeSeconds().ToString(), ClaimValueTypes.Integer64)
        };
        var token = new JwtSecurityToken(options.Issuer, options.Audience, claims,
            now, expires, new SigningCredentials(new SymmetricSecurityKey(options.SigningKey), SecurityAlgorithms.HmacSha256));
        return new(new JwtSecurityTokenHandler().WriteToken(token), expires);
    }
}
