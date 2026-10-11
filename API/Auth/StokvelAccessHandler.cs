using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using RondiTrack.Domain.Entities;
using RondiTrack.Domain.Repositories;

namespace RondiTrack.Auth;

public enum StokvelOperation { View, Manage, Contribute }
public record StokvelAccessResource(Guid StokvelId, Guid? ContributionUserId = null);
public class StokvelAccessRequirement(StokvelOperation operation) : IAuthorizationRequirement
{
    public StokvelOperation Operation { get; } = operation;
}

public class StokvelAccessHandler(IStokvelMemberRepository members)
    : AuthorizationHandler<StokvelAccessRequirement, StokvelAccessResource>
{
    protected override async Task HandleRequirementAsync(AuthorizationHandlerContext context,
        StokvelAccessRequirement requirement, StokvelAccessResource resource)
    {
        if (!Guid.TryParse(context.User.FindFirstValue("sub"), out var userId)) return;
        // Always read this stokvel's current membership; platform roles grant no bypass.
        var member = await members.GetByStokvelAndUserAsync(resource.StokvelId, userId);
        if (member is null)
        {
            context.Fail(new AuthorizationFailureReason(this, Policies.NotMember));
            return;
        }
        var elevated = member.Role is Roles.Treasurer or Roles.Chairperson;
        if (requirement.Operation == StokvelOperation.View || elevated ||
            (requirement.Operation == StokvelOperation.Contribute && resource.ContributionUserId == userId))
            context.Succeed(requirement);
    }
}
