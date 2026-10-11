using Microsoft.AspNetCore.Authorization;
using RondiTrack.Domain.Entities;

namespace RondiTrack.Auth;

public static class Policies
{
    public const string SignedIn = "SignedIn";
    public const string PlatformAdministration = "PlatformAdministration";
    public const string StokvelView = "StokvelView";
    public const string StokvelManage = "StokvelManage";
    public const string RecordContribution = "RecordContribution";
    public const string NotMember = "StokvelMembershipMissing";

    public static void Configure(AuthorizationOptions options)
    {
        options.FallbackPolicy = new AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build();
        options.AddPolicy(SignedIn, policy => policy.RequireAuthenticatedUser());
        options.AddPolicy(PlatformAdministration, policy => policy.RequireAuthenticatedUser().RequireRole(Roles.PlatformAdmin));
        options.AddPolicy(StokvelView, policy => policy.RequireAuthenticatedUser().AddRequirements(new StokvelAccessRequirement(StokvelOperation.View)));
        options.AddPolicy(StokvelManage, policy => policy.RequireAuthenticatedUser().AddRequirements(new StokvelAccessRequirement(StokvelOperation.Manage)));
        options.AddPolicy(RecordContribution, policy => policy.RequireAuthenticatedUser().AddRequirements(new StokvelAccessRequirement(StokvelOperation.Contribute)));
    }
}
