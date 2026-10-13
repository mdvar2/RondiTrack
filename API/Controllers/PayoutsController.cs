using RondiTrack.Common;
using Microsoft.AspNetCore.Authorization;
using RondiTrack.Auth;
using Microsoft.AspNetCore.Mvc;
using RondiTrack.DTOs.Payouts;
using RondiTrack.Services;

namespace RondiTrack.Controllers;

[Authorize(Policy = Policies.SignedIn)]
[ApiController]
[Route(
    ApiRoutes.V1 + "/stokvels/{stokvelId:guid}/cycles/{cycleId:guid}/payouts")]
public class PayoutsController : ControllerBase
{
    private readonly StokvelAuthorization _stokvelAuthorization;

    private readonly PayoutService _payoutService;

    public PayoutsController(
        PayoutService payoutService,
        StokvelAuthorization stokvelAuthorization)
    {
        _stokvelAuthorization = stokvelAuthorization;
        _payoutService = payoutService;
    }

    /// <summary>
    /// Creates a payout for the next eligible stokvel member.
    /// </summary>
    /// <remarks>
    /// Selects the next eligible member according to the stokvel
    /// payout rotation and creates the payout inside an explicit
    /// database transaction.
    /// </remarks>
    [ProducesResponseType(
        typeof(PayoutResponse),
        StatusCodes.Status201Created)]
    [ProducesResponseType(
        typeof(ProblemDetails),
        StatusCodes.Status404NotFound)]
    [ProducesResponseType(
        typeof(ProblemDetails),
        StatusCodes.Status409Conflict)]
    [Microsoft.AspNetCore.RateLimiting.EnableRateLimiting(RateLimitPolicies.Payouts)]
    [HttpPost]
    public async Task<ActionResult<PayoutResponse>> Create(
        Guid stokvelId,
        Guid cycleId,
        CreatePayoutRequest request)
    {
        await _stokvelAuthorization.EnsureAsync(User, stokvelId, Policies.StokvelManage);

        var payout =
            await _payoutService.CreatePayoutAsync(
                stokvelId,
                cycleId,
                request.Amount);

        return StatusCode(
            StatusCodes.Status201Created,
            payout);
    }
}