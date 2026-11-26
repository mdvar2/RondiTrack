using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RondiTrack.Auth;
using RondiTrack.Common;
using RondiTrack.Domain.Exceptions;
using RondiTrack.Infrastructure.Data;
using RondiTrack.Push;

namespace RondiTrack.Controllers;

/// <summary>
/// A treasurer (or chairperson) confirms a member's contribution: the money
/// arrived. Saved first; then the member's phones are told there's an update.
/// </summary>
[ApiController]
[Authorize(Policy = Policies.SignedIn)]
[Route(ApiRoutes.V1 + "/stokvels/{stokvelId:guid}/cycles/{cycleId:guid}/contributions/{contributionId:guid}/confirm")]
public sealed class ContributionConfirmationController(
    RondiTrackDbContext db,
    StokvelAuthorization authorization,
    PushNotifier notifier) : ControllerBase
{
    /// <summary>
    /// 204 when confirmed, or already confirmed (nothing is sent twice).
    /// 404 for a stokvel the caller isn't in, or a contribution not in this
    /// cycle; 403 for a member who isn't its treasurer or chairperson; 409
    /// for a contribution that isn't paid.
    /// </summary>
    [HttpPost]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Confirm(
        Guid stokvelId,
        Guid cycleId,
        Guid contributionId,
        CancellationToken cancellationToken)
    {
        await authorization.EnsureAsync(User, stokvelId, Policies.StokvelManage);

        var contribution = await db.Contributions.SingleOrDefaultAsync(
            c => c.Id == contributionId && c.StokvelId == stokvelId && c.ContributionCycleId == cycleId,
            cancellationToken) ?? throw new NotFoundException("Contribution not found.");

        if (contribution.Status == "Confirmed")
            return NoContent();
        if (contribution.Status != "Paid")
            throw new BusinessRuleException("Only a paid contribution can be confirmed.");

        contribution.UpdateStatus("Confirmed");
        await db.SaveChangesAsync(cancellationToken);

        // After the save, never before: a push about something that then
        // failed to save would be a lie. A failed push never fails this.
        await notifier.ContributionUpdatedAsync(
            contribution.UserId, stokvelId, cycleId, CancellationToken.None);

        return NoContent();
    }
}
