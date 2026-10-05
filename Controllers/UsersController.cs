using Microsoft.AspNetCore.Mvc;
using RondiTrack.DTOs.Users;
using RondiTrack.Exceptions;
using RondiTrack.Models;
using RondiTrack.Repositories;

namespace RondiTrack.Controllers;

[ApiController]
[Route("api/users")]
public class UsersController : ControllerBase
{
    private readonly IUserRepository _userRepository;

    public UsersController(IUserRepository userRepository)
    {
        _userRepository = userRepository;
    }

    /// <summary>
    /// Gets all users.
    /// </summary>
    /// <remarks>
    /// Returns all users currently available in RondiTrack.
    /// An empty collection is returned when no users are available.
    /// </remarks>
    [ProducesResponseType(
        typeof(IEnumerable<UserResponse>),
        StatusCodes.Status200OK)]
    [HttpGet]
    public async Task<ActionResult<IEnumerable<UserResponse>>> GetAll()
    {
        var users = await _userRepository.GetAllAsync();

        var response = users
            .Select(UserResponse.FromEntity)
            .ToList();

        return Ok(response);
    }

    /// <summary>
    /// Gets a user by ID.
    /// </summary>
    /// <remarks>
    /// Returns the requested user when the ID matches an existing user.
    /// Returns 404 when no user exists with the supplied ID.
    /// </remarks>
    [ProducesResponseType(
        typeof(UserResponse),
        StatusCodes.Status200OK)]
    [ProducesResponseType(
        typeof(ProblemDetails),
        StatusCodes.Status404NotFound)]
    [HttpGet("{id:guid}")]
    public async Task<ActionResult<UserResponse>> GetById(Guid id)
    {
        var user =
            await _userRepository.GetByIdReadOnlyAsync(id);

        if (user is null)
            throw new NotFoundException("User not found.");

        Response.Headers.ETag = $"\"{user.Version}\"";

        return Ok(UserResponse.FromEntity(user));
    }

    /// <summary>
    /// Creates a new user.
    /// </summary>
    /// <remarks>
    /// Creates a user from a valid name and email address.
    /// Returns 400 when the request fails validation.
    /// </remarks>
    [ProducesResponseType(
        typeof(UserResponse),
        StatusCodes.Status201Created)]
    [ProducesResponseType(
        typeof(ProblemDetails),
        StatusCodes.Status400BadRequest)]
    [HttpPost]
    public async Task<ActionResult<UserResponse>> Create(
        CreateUserRequest request)
    {
        var user =
            new User(
                request.Name,
                request.Email);

        await _userRepository.AddAsync(user);

        var response =
            UserResponse.FromEntity(user);

        return CreatedAtAction(
            nameof(GetById),
            new { id = user.Id },
            response);
    }

    /// <summary>
    /// Updates an existing user.
    /// </summary>
    /// <remarks>
    /// Updates the name and email address of an existing user.
    /// Returns 400 when the request fails validation.
    /// Returns 404 when the user does not exist.
    /// </remarks>
    [ProducesResponseType(
        StatusCodes.Status204NoContent)]
    [ProducesResponseType(
        typeof(ProblemDetails),
        StatusCodes.Status400BadRequest)]
    [ProducesResponseType(
        typeof(ProblemDetails),
        StatusCodes.Status404NotFound)]
    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(
        Guid id,
        UpdateUserRequest request,
        [FromHeader(Name = "If-Match")] string? ifMatch)
    {
        var user =
            await _userRepository.GetByIdAsync(id);

        if (user is null)
            throw new NotFoundException("User not found.");

        if (string.IsNullOrWhiteSpace(ifMatch))
        {
            throw new RequestValidationException(
                "If-Match header is required.");
        }

        var providedVersion = ParseIfMatch(ifMatch);
        if (providedVersion != user.Version)
        {
            throw new PreconditionFailedException(
                "The user has changed. Refetch it and retry with the current ETag.");
        }

        user.UpdateDetails(
            request.Name,
            request.Email);

        await _userRepository.UpdateAsync(user);

        return NoContent();
    }

    /// <summary>
    /// Deletes a user.
    /// </summary>
    /// <remarks>
    /// Deletes the user with the supplied ID.
    /// Returns 404 when the user does not exist.
    /// </remarks>
    [ProducesResponseType(
        StatusCodes.Status204NoContent)]
    [ProducesResponseType(
        typeof(ProblemDetails),
        StatusCodes.Status404NotFound)]
    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        var deleted =
            await _userRepository.DeleteAsync(id);

        if (!deleted)
            throw new NotFoundException("User not found.");

        return NoContent();
    }

    private static uint ParseIfMatch(string ifMatch)
    {
        var normalized = ifMatch.Trim();
        if (normalized.StartsWith('"') && normalized.EndsWith('"'))
        {
            normalized = normalized.Trim('"');
        }

        if (normalized.StartsWith("W/", StringComparison.OrdinalIgnoreCase))
        {
            normalized = normalized.Substring(2).Trim();
        }

        if (normalized.StartsWith('"') && normalized.EndsWith('"'))
        {
            normalized = normalized.Trim('"');
        }

        if (uint.TryParse(normalized, out var version))
        {
            return version;
        }

        throw new RequestValidationException(
            "If-Match header must contain a valid ETag value.");
    }
}