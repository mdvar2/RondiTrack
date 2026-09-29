using Microsoft.AspNetCore.Mvc;
using RondiTrack.DTOs.Contributions;
using RondiTrack.DTOs.Stokvels;
using RondiTrack.Exceptions;
using RondiTrack.Models;
using RondiTrack.Repositories;
using RondiTrack.Services;

namespace RondiTrack.Controllers;

[ApiController]
[Route("api/stokvels")]
public class StokvelsController : ControllerBase
{
    private readonly IStokvelRepository _stokvelRepository;
    private readonly MembershipService _membershipService;
    private readonly ContributionService _contributionService;

    public StokvelsController(
        IStokvelRepository stokvelRepository,
        MembershipService membershipService,
        ContributionService contributionService)
    {
        _stokvelRepository = stokvelRepository;
        _membershipService = membershipService;
        _contributionService = contributionService;
    }

    /// <summary>
    /// Gets all stokvels.
    /// </summary>
    /// <remarks>
    /// Returns all stokvels currently available in RondiTrack.
    /// An empty collection is returned when no stokvels are available.
    /// </remarks>
    [ProducesResponseType(
        typeof(IEnumerable<StokvelResponse>),
        StatusCodes.Status200OK)]
    [HttpGet]
    public async Task<ActionResult<IEnumerable<StokvelResponse>>> GetAll()
    {
        var stokvels = await _stokvelRepository.GetAllAsync();

        var response = stokvels
            .Select(StokvelResponse.FromEntity)
            .ToList();

        return Ok(response);
    }

    /// <summary>
    /// Gets a stokvel by ID.
    /// </summary>
    /// <remarks>
    /// Returns the requested stokvel when the ID matches an existing stokvel.
    /// Returns 404 when no stokvel exists with the supplied ID.
    /// </remarks>
    [ProducesResponseType(typeof(StokvelResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [HttpGet("{id:guid}")]
    public async Task<ActionResult<StokvelResponse>> GetById(Guid id)
    {
        var stokvel =
            await _stokvelRepository.GetByIdAsync(id);

        if (stokvel is null)
            throw new NotFoundException("Stokvel not found.");

        return Ok(StokvelResponse.FromEntity(stokvel));
    }

    /// <summary>
    /// Creates a new stokvel.
    /// </summary>
    /// <remarks>
    /// Creates a stokvel from a valid name and contribution amount.
    /// Returns 400 when the request fails validation.
    /// </remarks>
    [ProducesResponseType(typeof(StokvelResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [HttpPost]
    public async Task<ActionResult<StokvelResponse>> Create(
        CreateStokvelRequest request)
    {
        var stokvel = new Stokvel(
            request.Name,
            request.ContributionAmount);

        await _stokvelRepository.AddAsync(stokvel);

        var response =
            StokvelResponse.FromEntity(stokvel);

        return CreatedAtAction(
            nameof(GetById),
            new { id = stokvel.Id },
            response);
    }

    /// <summary>
    /// Updates an existing stokvel.
    /// </summary>
    /// <remarks>
    /// Updates the name and contribution amount of an existing stokvel.
    /// Returns 400 when the request fails validation.
    /// Returns 404 when the stokvel does not exist.
    /// </remarks>
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(
        Guid id,
        UpdateStokvelRequest request)
    {
        var stokvel =
            await _stokvelRepository.GetByIdAsync(id);

        if (stokvel is null)
            throw new NotFoundException("Stokvel not found.");

        stokvel.UpdateDetails(
            request.Name,
            request.ContributionAmount);

        return NoContent();
    }

    /// <summary>
    /// Deletes a stokvel.
    /// </summary>
    /// <remarks>
    /// Deletes the stokvel with the supplied ID.
    /// Returns 404 when the stokvel does not exist.
    /// </remarks>
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        var deleted =
            await _stokvelRepository.DeleteAsync(id);

        if (!deleted)
            throw new NotFoundException("Stokvel not found.");

        return NoContent();
    }

    /// <summary>
    /// Adds a user to a stokvel.
    /// </summary>
    /// <remarks>
    /// Adds an existing user as a member of an existing stokvel.
    /// Returns 404 when the stokvel or user does not exist.
    /// Returns 409 when the user is already a member of the stokvel.
    /// </remarks>
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    [HttpPost("{stokvelId:guid}/members/{userId:guid}")]
    public async Task<IActionResult> AddMember(
        Guid stokvelId,
        Guid userId)
    {
        await _membershipService.AddMemberAsync(
            stokvelId,
            userId);

        return NoContent();
    }

    /// <summary>
    /// Removes a user from a stokvel.
    /// </summary>
    /// <remarks>
    /// Removes an existing member from an existing stokvel.
    /// Returns 404 when the stokvel does not exist.
    /// Returns 409 when the user is not a member of the stokvel.
    /// </remarks>
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    [HttpDelete("{stokvelId:guid}/members/{userId:guid}")]
    public async Task<IActionResult> RemoveMember(
        Guid stokvelId,
        Guid userId)
    {
        await _membershipService.RemoveMemberAsync(
            stokvelId,
            userId);

        return NoContent();
    }

    /// <summary>
    /// Records a contribution for a stokvel member.
    /// </summary>
    /// <remarks>
    /// Records a contribution for an existing member and contribution cycle.
    /// An Idempotency-Key header is required to safely retry the request.
    /// Repeating the same request with the same key returns the stored response
    /// without creating another contribution.
    /// Returns 400 when the Idempotency-Key is missing or the request fails validation.
    /// Returns 404 when the stokvel, user, or contribution cycle does not exist.
    /// Returns 409 when a business rule is violated, such as when the user is not
    /// a member, the cycle belongs to another stokvel, a contribution already exists
    /// for the member and cycle, or an idempotency key is reused with different data.
    /// </remarks>
    [ProducesResponseType(
        typeof(ContributionResponse),
        StatusCodes.Status201Created)]
    [ProducesResponseType(
        typeof(ProblemDetails),
        StatusCodes.Status400BadRequest)]
    [ProducesResponseType(
        typeof(ProblemDetails),
        StatusCodes.Status404NotFound)]
    [ProducesResponseType(
        typeof(ProblemDetails),
        StatusCodes.Status409Conflict)]
    [HttpPost(
        "{stokvelId:guid}/members/{userId:guid}/contributions")]
    public async Task<IActionResult> RecordContribution(
        Guid stokvelId,
        Guid userId,
        [FromHeader(Name = "Idempotency-Key")]
        string? idempotencyKey,
        RecordContributionRequest request)
    {
        var response =
            await _contributionService.RecordContributionAsync(
                stokvelId,
                userId,
                idempotencyKey ?? string.Empty,
                request);

        return StatusCode(
            StatusCodes.Status201Created,
            response);
    }
}