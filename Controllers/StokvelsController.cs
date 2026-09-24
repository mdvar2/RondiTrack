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

    [HttpGet]
    public async Task<ActionResult<IEnumerable<StokvelResponse>>> GetAll()
    {
        var stokvels = await _stokvelRepository.GetAllAsync();

        var response = stokvels
            .Select(StokvelResponse.FromEntity)
            .ToList();

        return Ok(response);
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<StokvelResponse>> GetById(Guid id)
    {
        var stokvel =
            await _stokvelRepository.GetByIdAsync(id);

        if (stokvel is null)
            throw new NotFoundException("Stokvel not found.");

        return Ok(StokvelResponse.FromEntity(stokvel));
    }

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

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        var deleted =
            await _stokvelRepository.DeleteAsync(id);

        if (!deleted)
            throw new NotFoundException("Stokvel not found.");

        return NoContent();
    }

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