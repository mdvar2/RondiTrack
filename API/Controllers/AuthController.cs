using RondiTrack.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RondiTrack.Auth;
using RondiTrack.DTOs.Auth;
using RondiTrack.DTOs.Users;

namespace RondiTrack.Controllers;

[ApiController]
[Route(ApiRoutes.V1 + "/auth")]
public class AuthController(AuthService authService) : ControllerBase
{
    [AllowAnonymous]
    [HttpPost("login")]
    public async Task<ActionResult<AccessTokenResponse>> Login(LoginRequest request)
    {
        var session = await authService.LoginAsync(request);
        SetRefreshCookie(session);
        return Ok(session.Access);
    }

    public const string RefreshCookieName = "__Secure-ronditrack_refresh";

    public static CookieOptions RefreshCookieOptions => new()
    {
        HttpOnly = true, Secure = true, SameSite = SameSiteMode.Strict,
        Path = ApiRoutes.AuthCookiePath, IsEssential = true
    };

    private void SetRefreshCookie(AuthSessionResult session)
    {
        var options = RefreshCookieOptions;
        options.Expires = session.RefreshExpiresAtUtc;
        Response.Cookies.Append(RefreshCookieName, session.RefreshToken, options);
    }

    [AllowAnonymous]
    [HttpPost("refresh")]
    public async Task<ActionResult<AccessTokenResponse>> Refresh()
    {
        var session = await authService.RefreshAsync(Request.Cookies[RefreshCookieName]);
        SetRefreshCookie(session);
        return Ok(session.Access);
    }

    [AllowAnonymous]
    [HttpPost("logout")]
    public async Task<IActionResult> Logout()
    {
        await authService.LogoutAsync(Request.Cookies[RefreshCookieName]);
        Response.Cookies.Delete(RefreshCookieName, RefreshCookieOptions);
        return NoContent();
    }

    [AllowAnonymous]
    [HttpPost("register")]
    public async Task<ActionResult<UserResponse>> Register(RegisterRequest request)
    {
        var response = await authService.RegisterAsync(request);
        return Created($"/{ApiRoutes.V1}/users/{response.Id}", response);
    }
}
