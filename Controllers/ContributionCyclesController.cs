using Microsoft.AspNetCore.Mvc;
using RondiTrack.DTOs.ContributionCycles;
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

    public ContributionCyclesController(
        IContributionCycleRepository cycleRepository,
        IStokvelRepository stokvelRepository)
    {
        _cycleRepository = cycleRepository;
        _stokvelRepository = stokvelRepository;
    }

    [HttpGet]
    public async Task<ActionResult<IEnumerable<ContributionCycleResponse>>> GetAll(
        Guid stokvelId)
    {
        var stokvel =
            await _stokvelRepository.GetByIdAsync(stokvelId);

        if (stokvel is null)
            throw new NotFoundException("Stokvel not found.");

        var cycles =
            await _cycleRepository.GetAllByStokvelAsync(stokvelId);

        var response = cycles
            .Select(ContributionCycleResponse.FromEntity)
            .ToList();

        return Ok(response);
    }

    [HttpGet("{cycleId:guid}")]
    public async Task<ActionResult<ContributionCycleResponse>> GetById(
        Guid stokvelId,
        Guid cycleId)
    {
        var stokvel =
            await _stokvelRepository.GetByIdAsync(stokvelId);

        if (stokvel is null)
            throw new NotFoundException("Stokvel not found.");

        var cycle =
            await _cycleRepository.GetByIdAsync(cycleId);

        if (cycle is null || cycle.StokvelId != stokvelId)
            throw new NotFoundException(
                "Contribution cycle not found.");

        return Ok(
            ContributionCycleResponse.FromEntity(cycle));
    }

    [HttpPost]
    public async Task<ActionResult<ContributionCycleResponse>> Create(
        Guid stokvelId,
        CreateContributionCycleRequest request)
    {
        var stokvel =
            await _stokvelRepository.GetByIdAsync(stokvelId);

        if (stokvel is null)
            throw new NotFoundException("Stokvel not found.");

        var cycle = new ContributionCycle(
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

    [HttpPut("{cycleId:guid}")]
    public async Task<IActionResult> Update(
        Guid stokvelId,
        Guid cycleId,
        UpdateContributionCycleRequest request)
    {
        var stokvel =
            await _stokvelRepository.GetByIdAsync(stokvelId);

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

        return NoContent();
    }

    [HttpDelete("{cycleId:guid}")]
    public async Task<IActionResult> Delete(
        Guid stokvelId,
        Guid cycleId)
    {
        var stokvel =
            await _stokvelRepository.GetByIdAsync(stokvelId);

        if (stokvel is null)
            throw new NotFoundException("Stokvel not found.");

        var cycle =
            await _cycleRepository.GetByIdAsync(cycleId);

        if (cycle is null || cycle.StokvelId != stokvelId)
            throw new NotFoundException(
                "Contribution cycle not found.");

        await _cycleRepository.DeleteAsync(cycleId);

        return NoContent();
    }
}