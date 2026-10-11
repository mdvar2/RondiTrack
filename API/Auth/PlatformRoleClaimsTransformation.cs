using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using RondiTrack.Domain.Repositories;

namespace RondiTrack.Auth;

// Platform role is resolved per request, not stored in the JWT snapshot.
public class PlatformRoleClaimsTransformation(IUserRepository users) : IClaimsTransformation
{
    public async Task<ClaimsPrincipal> TransformAsync(ClaimsPrincipal principal)
    {
        if (!Guid.TryParse(principal.FindFirstValue("sub"), out var userId))
            return new ClaimsPrincipal(new ClaimsIdentity());
        var user = await users.GetByIdReadOnlyAsync(userId);
        if (user is null) return new ClaimsPrincipal(new ClaimsIdentity());
        var identity = new ClaimsIdentity((ClaimsIdentity)principal.Identity!);
        foreach (var claim in identity.FindAll("platform_role").ToArray()) identity.RemoveClaim(claim);
        identity.AddClaim(new Claim("platform_role", user.PlatformRole));
        return new ClaimsPrincipal(identity);
    }
}
