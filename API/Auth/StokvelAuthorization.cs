using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using RondiTrack.Domain.Exceptions;

namespace RondiTrack.Auth;

public class StokvelAuthorization(IAuthorizationService authorization)
{
    public async Task EnsureAsync(ClaimsPrincipal caller, Guid stokvelId, string policy, Guid? contributionUserId = null)
    {
        var result = await authorization.AuthorizeAsync(caller,
            new StokvelAccessResource(stokvelId, contributionUserId), policy);
        if (result.Succeeded) return;
        if (result.Failure?.FailureReasons.Any(reason => reason.Message == Policies.NotMember) == true)
            throw new NotFoundException("Stokvel not found.");
        throw new ForbiddenException("You do not have permission for this operation.");
    }

    public static Guid UserId(ClaimsPrincipal caller) =>
        Guid.TryParse(caller.FindFirstValue("sub"), out var id) ? id
            : throw new AuthenticationFailedException("Authentication is required.");
}
