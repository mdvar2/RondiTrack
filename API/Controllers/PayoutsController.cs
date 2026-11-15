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
    /// Gets the payout order as seen from one cycle.
    /// </summary>
    /// <remarks>
    /// Lists every member of the stokvel in rotation order (the order they
    /// joined), each with a 1-based position, the amount in cents, when they
    /// were paid, and a status: Paid, Next or Waiting. The server decides
    /// every status: Next is the member the next payout goes to, so no client
    /// works it out. Items is empty for a cycle that isn't open and had no
    /// payout. Any member may read it. Returns 404 when the caller isn't a
    /// member, and when the cycle doesn't exist or belongs to another stokvel.
    /// </remarks>
    [ProducesResponseType(
        typeof(PayoutOrderResponse),
        StatusCodes.Status200OK)]
    [ProducesResponseType(
        typeof(ProblemDetails),
        StatusCodes.Status404NotFound)]
    [HttpGet]
    public async Task<ActionResult<PayoutOrderResponse>> GetOrder(
        Guid stokvelId,
        Guid cycleId,
        CancellationToken cancellationToken)
    {
        await _stokvelAuthorization.EnsureAsync(User, stokvelId, Policies.StokvelView);

        return Ok(
            await _payoutService.GetOrderAsync(
                stokvelId,
                cycleId,
                cancellationToken));
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
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status429TooManyRequests)]
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
