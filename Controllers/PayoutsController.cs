using Microsoft.AspNetCore.Mvc;
using RondiTrack.DTOs.Payouts;
using RondiTrack.Services;

namespace RondiTrack.Controllers;

[ApiController]
[Route(
    "api/stokvels/{stokvelId:guid}/cycles/{cycleId:guid}/payouts")]
public class PayoutsController : ControllerBase
{
    private readonly PayoutService _payoutService;

    public PayoutsController(
        PayoutService payoutService)
    {
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
    [HttpPost]
    public async Task<ActionResult<PayoutResponse>> Create(
        Guid stokvelId,
        Guid cycleId,
        CreatePayoutRequest request)
    {
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