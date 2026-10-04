using Microsoft.AspNetCore.Mvc;
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
    public async Task<ActionResult<IEnumerable<ContributionWithMemberResponse>>>
        GetContributions(
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

        var response =
            await _contributionRepository.GetByCycleProjectedAsync(
                stokvelId,
                cycleId);

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
        UpdateContributionCycleRequest request)
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
}