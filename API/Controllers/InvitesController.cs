using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using RondiTrack.Auth;
using RondiTrack.Common;
using RondiTrack.Domain.Entities;
using RondiTrack.Domain.Exceptions;
using RondiTrack.Domain.Repositories;
using RondiTrack.DTOs.Invites;
using RondiTrack.Infrastructure.Data;
using RondiTrack.Services;

namespace RondiTrack.Controllers;

[Authorize(Policy = Policies.SignedIn)]
[ApiController]
[Route(ApiRoutes.V1 + "/invites")]
public sealed class InvitesController(
    RondiTrackDbContext db,
    IStokvelRepository stokvels,
    IStokvelMemberRepository members,
    MembershipService membership,
    StokvelAuthorization authorization) : ControllerBase
{
    [HttpPost("/api/v1/stokvels/{stokvelId:guid}/invites")]
    public async Task<ActionResult<CreateInviteResponse>> Create(Guid stokvelId)
    {
        await authorization.EnsureAsync(User, stokvelId, Policies.StokvelManage);
        if (await stokvels.GetByIdReadOnlyAsync(stokvelId) is null)
            throw new NotFoundException("Stokvel not found.");
        var invite = new StokvelInvite(stokvelId, DateTime.UtcNow.AddDays(7));
        db.Set<StokvelInvite>().Add(invite);
        await db.SaveChangesAsync();
        return Ok(new CreateInviteResponse(invite.Code, invite.ExpiresAtUtc));
    }

    [HttpGet("{code}")]
    public async Task<ActionResult<InvitePreviewResponse>> Preview(string code)
    {
        var invite = await db.Set<StokvelInvite>().AsNoTracking()
            .Include(x => x.Stokvel)
            .SingleOrDefaultAsync(x => x.Code == code);
        if (invite is null) throw new NotFoundException("Invite not found.");
        if (invite.IsExpired(DateTime.UtcNow))
            throw new ExpiredException("This invite has expired.");
        var count = await db.StokvelMembers.CountAsync(x => x.StokvelId == invite.StokvelId);
        return Ok(new InvitePreviewResponse(
            invite.StokvelId,
            invite.Stokvel.Name,
            checked((long)(invite.Stokvel.ContributionAmount * 100m)),
            "Monthly",
            "Members contribute the agreed amount each month.",
            count));
    }

    // For a signed-out link preview of /join?code=…: whether the invite is
    // still valid, and nothing else. No stokvel name, no member, no amount,
    // because whoever holds the link (or a WhatsApp group's preview bot)
    // need not be a member. Unknown codes are 404, as for Preview.
    [AllowAnonymous]
    [EnableRateLimiting(RateLimitPolicies.InviteStatus)]
    [ProducesResponseType<InviteStatusResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status429TooManyRequests)]
    [HttpGet("{code}/status")]
    public async Task<ActionResult<InviteStatusResponse>> Status(string code)
    {
        var expiresAtUtc = await db.Set<StokvelInvite>().AsNoTracking()
            .Where(x => x.Code == code)
            .Select(x => (DateTime?)x.ExpiresAtUtc)
            .SingleOrDefaultAsync();
        if (expiresAtUtc is null) throw new NotFoundException("Invite not found.");
        return Ok(new InviteStatusResponse(expiresAtUtc.Value <= DateTime.UtcNow ? "expired" : "valid"));
    }

    [HttpPost("{code}/join")]
    public async Task<IActionResult> Join(string code)
    {
        var invite = await db.Set<StokvelInvite>().AsNoTracking()
            .SingleOrDefaultAsync(x => x.Code == code);
        if (invite is null) throw new NotFoundException("Invite not found.");
        if (invite.IsExpired(DateTime.UtcNow))
            throw new ExpiredException("This invite has expired.");
        var userId = StokvelAuthorization.UserId(User);
        if (await members.ExistsAsync(invite.StokvelId, userId))
            throw new BusinessRuleException("You are already a member of this stokvel.");
        await membership.AddMemberAsync(invite.StokvelId, userId);
        return NoContent();
    }
}
