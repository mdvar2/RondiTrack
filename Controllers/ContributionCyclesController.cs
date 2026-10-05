using Microsoft.AspNetCore.Mvc;
using RondiTrack.Common;
using RondiTrack.DTOs.ContributionCycles;
using RondiTrack.DTOs.Contributions;
using RondiTrack.Exceptions;
using RondiTrack.Models;
using RondiTrack.Repositories;

namespace RondiTrack.Controllers;

[ApiController]
[Route("api/stokvels/{stokvelId:guid}/cycles")]
public class ContributionCyclesController : ControllerBase
{
    private readonly IContributionCycleRepository _cycleRepository;
    private readonly IStokvelRepository _stokvelRepository;
    private readonly IContributionRepository _contributionRepository;

    public ContributionCyclesController(
        IContributionCycleRepository cycleRepository,
        IStokvelRepository stokvelRepository,
        IContributionRepository contributionRepository)
    {
        _cycleRepository = cycleRepository;
        _stokvelRepository = stokvelRepository;
        _contributionRepository = contributionRepository;
    }

    /// <summary>
    /// Gets all contribution cycles for a stokvel.
    /// </summary>
    /// <remarks>
    /// Returns all contribution cycles belonging to the specified stokvel.
    /// An empty collection is returned when the stokvel exists but has no cycles.
    /// Returns 404 when the stokvel does not exist.
    /// </remarks>
    [ProducesResponseType(
        typeof(IEnumerable<ContributionCycleResponse>),
        StatusCodes.Status200OK)]
    [ProducesResponseType(
        typeof(ProblemDetails),
        StatusCodes.Status404NotFound)]
    [HttpGet]
    public async Task<ActionResult<IEnumerable<ContributionCycleResponse>>> GetAll(
        Guid stokvelId)
    {
        var stokvel =
            await _stokvelRepository.GetByIdReadOnlyAsync(stokvelId);

        if (stokvel is null)
            throw new NotFoundException("Stokvel not found.");

        var cycles =
            await _cycleRepository.GetAllByStokvelAsync(stokvelId);

        var response = cycles
            .Select(ContributionCycleResponse.FromEntity)
            .ToList();

        return Ok(response);
    }

    /// <summary>
    /// Gets a contribution cycle by ID.
    /// </summary>
    /// <remarks>
    /// Returns the requested contribution cycle when it belongs to the specified stokvel.
    /// Returns 404 when the stokvel does not exist, the cycle does not exist,
    /// or the cycle does not belong to the specified stokvel.
    /// </remarks>
    [ProducesResponseType(
        typeof(ContributionCycleResponse),
        StatusCodes.Status200OK)]
    [ProducesResponseType(
        typeof(ProblemDetails),
        StatusCodes.Status404NotFound)]
    [HttpGet("{cycleId:guid}")]
    public async Task<ActionResult<ContributionCycleResponse>> GetById(
        Guid stokvelId,
        Guid cycleId)
    {
        var stokvel =
            await _stokvelRepository.GetByIdReadOnlyAsync(stokvelId);

        if (stokvel is null)
            throw new NotFoundException("Stokvel not found.");

        var cycle =
            await _cycleRepository.GetByIdReadOnlyAsync(cycleId);

        if (cycle is null || cycle.StokvelId != stokvelId)
            throw new NotFoundException(
                "Contribution cycle not found.");

        Response.Headers.ETag = $"\"{cycle.Version}\"";

        return Ok(
            ContributionCycleResponse.FromEntity(cycle));
    }

    /// <summary>
    /// Gets contributions for a contribution cycle.
    /// </summary>
    /// <remarks>
    /// Returns contributions together with membership and user information.
    /// The query projects directly to the response DTO so that only the
    /// columns required by the API response are fetched.
    /// </remarks>
    [ProducesResponseType(
        typeof(IEnumerable<ContributionWithMemberResponse>),
        StatusCodes.Status200OK)]
    [ProducesResponseType(
        typeof(ProblemDetails),
        StatusCodes.Status404NotFound)]
    [HttpGet("{cycleId:guid}/contributions")]
    public async Task<ActionResult<PagedResult<ContributionWithMemberResponse>>>
        GetContributions(
            Guid stokvelId,
            Guid cycleId,
            [FromQuery] int pageSize = 25,
            [FromQuery] string? pageToken = null,
            [FromQuery] string sortBy = "recordedAtUtc",
            [FromQuery] string sortDirection = "desc",
            [FromQuery] Guid? userId = null,
            [FromQuery] string? role = null,
            [FromQuery] decimal? minAmount = null,
            [FromQuery] decimal? maxAmount = null)
    {
        var stokvel =
            await _stokvelRepository.GetByIdReadOnlyAsync(stokvelId);

        if (stokvel is null)
            throw new NotFoundException("Stokvel not found.");

        var cycle =
            await _cycleRepository.GetByIdReadOnlyAsync(cycleId);

        if (cycle is null || cycle.StokvelId != stokvelId)
            throw new NotFoundException(
                "Contribution cycle not found.");

        if (pageSize < 0)
        {
            throw new RequestValidationException(
                "Page size cannot be negative.");
        }

        var safePageSize = Math.Min(
            pageSize <= 0 ? 25 : pageSize,
            100);

        var response =
            await _contributionRepository.GetByCyclePagedAsync(
                stokvelId,
                cycleId,
                safePageSize,
                pageToken,
                sortBy,
                sortDirection,
                userId,
                role,
                minAmount,
                maxAmount);

        return Ok(response);
    }

    /// <summary>
    /// Creates a contribution cycle for a stokvel.
    /// </summary>
    /// <remarks>
    /// Creates a new contribution cycle for an existing stokvel.
    /// Returns 400 when the request fails validation.
    /// Returns 404 when the stokvel does not exist.
    /// </remarks>
    [ProducesResponseType(
        typeof(ContributionCycleResponse),
        StatusCodes.Status201Created)]
    [ProducesResponseType(
        typeof(ProblemDetails),
        StatusCodes.Status400BadRequest)]
    [ProducesResponseType(
        typeof(ProblemDetails),
        StatusCodes.Status404NotFound)]
    [HttpPost]
    public async Task<ActionResult<ContributionCycleResponse>> Create(
        Guid stokvelId,
        CreateContributionCycleRequest request)
    {
        var stokvel =
            await _stokvelRepository.GetByIdReadOnlyAsync(stokvelId);

        if (stokvel is null)
            throw new NotFoundException("Stokvel not found.");

        var cycle =
            new ContributionCycle(
                stokvelId,
                request.Period,
                request.TargetAmount);

        await _cycleRepository.AddAsync(cycle);

        var response =
            ContributionCycleResponse.FromEntity(cycle);

        return CreatedAtAction(
            nameof(GetById),
            new
            {
                stokvelId,
                cycleId = cycle.Id
            },
            response);
    }

    /// <summary>
    /// Updates an existing contribution cycle.
    /// </summary>
    /// <remarks>
    /// Updates the period and target amount of an existing contribution cycle.
    /// Returns 400 when the request fails validation.
    /// Returns 404 when the stokvel or contribution cycle does not exist,
    /// or when the cycle does not belong to the specified stokvel.
    /// </remarks>
    [ProducesResponseType(
        StatusCodes.Status204NoContent)]
    [ProducesResponseType(
        typeof(ProblemDetails),
        StatusCodes.Status400BadRequest)]
    [ProducesResponseType(
        typeof(ProblemDetails),
        StatusCodes.Status404NotFound)]
    [HttpPut("{cycleId:guid}")]
    public async Task<IActionResult> Update(
        Guid stokvelId,
        Guid cycleId,
        UpdateContributionCycleRequest request,
        [FromHeader(Name = "If-Match")] string? ifMatch)
    {
        var stokvel =
            await _stokvelRepository.GetByIdReadOnlyAsync(stokvelId);

        if (stokvel is null)
            throw new NotFoundException("Stokvel not found.");

        var cycle =
            await _cycleRepository.GetByIdAsync(cycleId);

        if (cycle is null || cycle.StokvelId != stokvelId)
            throw new NotFoundException(
                "Contribution cycle not found.");

        if (string.IsNullOrWhiteSpace(ifMatch))
        {
            throw new RequestValidationException(
                "If-Match header is required.");
        }

        var providedVersion = ParseIfMatch(ifMatch);
        if (providedVersion != cycle.Version)
        {
            throw new PreconditionFailedException(
                "The contribution cycle has changed. Refetch it and retry with the current ETag.");
        }

        cycle.UpdateDetails(
            request.Period,
            request.TargetAmount);

        await _cycleRepository.UpdateAsync(cycle);

        return NoContent();
    }

    /// <summary>
    /// Deletes a contribution cycle.
    /// </summary>
    /// <remarks>
    /// Deletes an existing contribution cycle from the specified stokvel.
    /// Returns 404 when the stokvel or contribution cycle does not exist,
    /// or when the cycle does not belong to the specified stokvel.
    /// </remarks>
    [ProducesResponseType(
        StatusCodes.Status204NoContent)]
    [ProducesResponseType(
        typeof(ProblemDetails),
        StatusCodes.Status404NotFound)]
    [HttpDelete("{cycleId:guid}")]
    public async Task<IActionResult> Delete(
        Guid stokvelId,
        Guid cycleId)
    {
        var stokvel =
            await _stokvelRepository.GetByIdReadOnlyAsync(stokvelId);

        if (stokvel is null)
            throw new NotFoundException("Stokvel not found.");

        var cycle =
            await _cycleRepository.GetByIdReadOnlyAsync(cycleId);

        if (cycle is null || cycle.StokvelId != stokvelId)
            throw new NotFoundException(
                "Contribution cycle not found.");

        await _cycleRepository.DeleteAsync(cycleId);

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