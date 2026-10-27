using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RondiTrack.Auth;
using RondiTrack.Common;
using RondiTrack.Domain.Exceptions;
using RondiTrack.Domain.Repositories;
using RondiTrack.DTOs.Home;
using RondiTrack.DTOs.Users;
using RondiTrack.Services;

namespace RondiTrack.Controllers;

[ApiController]
[Authorize(Policy = Policies.SignedIn)]
[Route(ApiRoutes.V1 + "/me")]
public class MeController(MemberHomeService service, IUserRepository users) : ControllerBase
{
    // Who the caller is. No role: roles belong to each stokvel, not to the person.
    [HttpGet]
    [ProducesResponseType<UserResponse>(StatusCodes.Status200OK)]
    public async Task<ActionResult<UserResponse>> Get()
    {
        var user = await users.GetByIdReadOnlyAsync(StokvelAuthorization.UserId(User))
            ?? throw new AuthenticationFailedException("Authentication is required.");
        return Ok(UserResponse.FromEntity(user));
    }

    [HttpGet("home")]
    [ProducesResponseType<HomeResponse>(StatusCodes.Status200OK)]
    public async Task<ActionResult<HomeResponse>> Home(CancellationToken cancellationToken) =>
        Ok(await service.GetAsync(StokvelAuthorization.UserId(User), cancellationToken));
}
