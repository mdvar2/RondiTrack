using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RondiTrack.Auth;
using RondiTrack.Common;
using RondiTrack.DTOs.Home;
using RondiTrack.Services;

namespace RondiTrack.Controllers;

[ApiController]
[Authorize(Policy = Policies.SignedIn)]
[Route(ApiRoutes.V1 + "/me")]
public class MeController(MemberHomeService service) : ControllerBase
{
    [HttpGet("home")]
    [ProducesResponseType<HomeResponse>(StatusCodes.Status200OK)]
    public async Task<ActionResult<HomeResponse>> Home(CancellationToken cancellationToken) =>
        Ok(await service.GetAsync(StokvelAuthorization.UserId(User), cancellationToken));
}
