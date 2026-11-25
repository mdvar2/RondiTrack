using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RondiTrack.Auth;
using RondiTrack.Common;
using RondiTrack.Domain.Entities;
using RondiTrack.Domain.Exceptions;
using RondiTrack.Infrastructure.Data;

namespace RondiTrack.Controllers;

/// <summary>The body of POST /me/devices.</summary>
public sealed record RegisterDeviceRequest(string? InstallationId);

/// <summary>
/// The signed-in member's phones, as push addresses (README "Notifications").
/// Only ever the caller's own: there is no way to name another member.
/// </summary>
[ApiController]
[Authorize(Policy = Policies.SignedIn)]
[Route(ApiRoutes.V1 + "/me/devices")]
public sealed partial class DevicesController(RondiTrackDbContext db, TimeProvider clock) : ControllerBase
{
    /// <summary>
    /// Registers this phone for the caller. A phone registered to someone
    /// else moves to the caller: a shared phone is safe even if the last
    /// sign-out never reached the API.
    /// </summary>
    [HttpPost]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Register(
        RegisterDeviceRequest request,
        CancellationToken cancellationToken)
    {
        var installationId = Validate(request.InstallationId);
        var callerId = StokvelAuthorization.UserId(User);
        var now = clock.GetUtcNow().UtcDateTime;

        var device = await db.DeviceInstallations
            .SingleOrDefaultAsync(d => d.InstallationId == installationId, cancellationToken);
        if (device is null)
            db.DeviceInstallations.Add(new DeviceInstallation(installationId, callerId, now));
        else
            device.MoveTo(callerId, now);

        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException) when (device is null)
        {
            // Registered at the same moment by another request: move it.
            db.ChangeTracker.Clear();
            var existing = await db.DeviceInstallations
                .SingleAsync(d => d.InstallationId == installationId, cancellationToken);
            existing.MoveTo(callerId, now);
            await db.SaveChangesAsync(cancellationToken);
        }

        return NoContent();
    }

    /// <summary>
    /// Forgets this phone, at sign-out. Only the caller's own registration
    /// is removed; any other id answers 204 and changes nothing.
    /// </summary>
    [HttpDelete("{installationId}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Forget(string installationId, CancellationToken cancellationToken)
    {
        var id = Validate(installationId);
        var callerId = StokvelAuthorization.UserId(User);
        await db.DeviceInstallations
            .Where(d => d.InstallationId == id && d.UserId == callerId)
            .ExecuteDeleteAsync(cancellationToken);
        return NoContent();
    }

    private static string Validate(string? installationId)
    {
        var id = installationId?.Trim();
        if (string.IsNullOrEmpty(id) ||
            id.Length > DeviceInstallation.MaxInstallationIdLength ||
            !InstallationIdPattern().IsMatch(id))
        {
            throw new RequestValidationException(
                "installationId must be 1 to 255 letters, digits, '-', '_', ':' or '.'.");
        }

        return id;
    }

    [GeneratedRegex("^[A-Za-z0-9_:.-]+$")]
    private static partial Regex InstallationIdPattern();
}
