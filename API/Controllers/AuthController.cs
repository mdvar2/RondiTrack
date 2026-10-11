using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RondiTrack.Auth;
using RondiTrack.DTOs.Auth;
using RondiTrack.DTOs.Users;

namespace RondiTrack.Controllers;

[ApiController]
[Route("api/auth")]
public class AuthController(AuthService authService) : ControllerBase
{
    [AllowAnonymous]
    [HttpPost("register")]
    public async Task<ActionResult<UserResponse>> Register(RegisterRequest request)
    {
        var response = await authService.RegisterAsync(request);
        return Created($"/api/users/{response.Id}", response);
    }
}
