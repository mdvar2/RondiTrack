using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using RondiTrack.Domain.Exceptions;
using RondiTrack.Observability;

namespace RondiTrack.Auth;

public class StokvelAuthorization(IAuthorizationService authorization, RondiTrackTelemetry telemetry)
{
    public async Task EnsureAsync(ClaimsPrincipal caller, Guid stokvelId, string policy, Guid? contributionUserId = null)
    {
        var result = await authorization.AuthorizeAsync(caller,
            new StokvelAccessResource(stokvelId, contributionUserId), policy);
        if (result.Succeeded) return;
        if (result.Failure?.FailureReasons.Any(reason => reason.Message == Policies.NotMember) == true)
        {
            telemetry.AuthorizationRefused(notFound: true);
            throw new NotFoundException("Stokvel not found.");
        }
        telemetry.AuthorizationRefused(notFound: false);
        throw new ForbiddenException("You do not have permission for this operation.");
    }

    public static Guid UserId(ClaimsPrincipal caller) =>
        Guid.TryParse(caller.FindFirstValue("sub"), out var id) ? id
            : throw new AuthenticationFailedException("Authentication is required.");
}
