using Microsoft.AspNetCore.Mvc;
using RondiTrack.Common;
using RondiTrack.DTOs.Contributions;
using RondiTrack.DTOs.Stokvels;
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
        var stokvel = await _stokvelRepository.GetByIdAsync(id);

        if (stokvel is null)
        {
            return ProblemResponses.NotFound(
                "Stokvel not found.",
                HttpContext.Request.Path);
        }

        return Ok(StokvelResponse.FromEntity(stokvel));
    }

    [HttpPost]
    public async Task<ActionResult<StokvelResponse>> Create(
        CreateStokvelRequest request)
    {
        Stokvel stokvel;

        try
        {
            stokvel = new Stokvel(
                request.Name,
                request.ContributionAmount);
        }
        catch (ArgumentException ex)
        {
            return ProblemResponses.BadRequest(
                ex.Message,
                HttpContext.Request.Path);
        }

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
        {
            return ProblemResponses.NotFound(
                "Stokvel not found.",
                HttpContext.Request.Path);
        }

        try
        {
            stokvel.UpdateDetails(
                request.Name,
                request.ContributionAmount);
        }
        catch (ArgumentException ex)
        {
            return ProblemResponses.BadRequest(
                ex.Message,
                HttpContext.Request.Path);
        }

        return NoContent();
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        var deleted =
            await _stokvelRepository.DeleteAsync(id);

        if (!deleted)
        {
            return ProblemResponses.NotFound(
                "Stokvel not found.",
                HttpContext.Request.Path);
        }

        return NoContent();
    }

    [HttpPost("{stokvelId:guid}/members/{userId:guid}")]
    public async Task<IActionResult> AddMember(
        Guid stokvelId,
        Guid userId)
    {
        var result =
            await _membershipService.AddMemberAsync(
                stokvelId,
                userId);

        return result switch
        {
            MembershipResult.Success =>
                NoContent(),

            MembershipResult.StokvelNotFound =>
                ProblemResponses.NotFound(
                    "Stokvel not found.",
                    HttpContext.Request.Path),

            MembershipResult.UserNotFound =>
                ProblemResponses.NotFound(
                    "User not found.",
                    HttpContext.Request.Path),

            MembershipResult.AlreadyMember =>
                ProblemResponses.Conflict(
                    "User is already a member of this stokvel.",
                    HttpContext.Request.Path),

            _ =>
                ProblemResponses.BadRequest(
                    "The membership request could not be processed.",
                    HttpContext.Request.Path)
        };
    }

    [HttpDelete("{stokvelId:guid}/members/{userId:guid}")]
    public async Task<IActionResult> RemoveMember(
        Guid stokvelId,
        Guid userId)
    {
        var result =
            await _membershipService.RemoveMemberAsync(
                stokvelId,
                userId);

        return result switch
        {
            MembershipResult.Success =>
                NoContent(),

            MembershipResult.StokvelNotFound =>
                ProblemResponses.NotFound(
                    "Stokvel not found.",
                    HttpContext.Request.Path),

            MembershipResult.NotMember =>
                ProblemResponses.Conflict(
                    "User is not a member of this stokvel.",
                    HttpContext.Request.Path),

            _ =>
                ProblemResponses.BadRequest(
                    "The membership request could not be processed.",
                    HttpContext.Request.Path)
        };
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
        if (string.IsNullOrWhiteSpace(idempotencyKey))
        {
            return ProblemResponses.BadRequest(
                "Idempotency-Key header is required.",
                HttpContext.Request.Path);
        }

        var result =
            await _contributionService.RecordContributionAsync(
                stokvelId,
                userId,
                idempotencyKey,
                request);

        return result.Outcome switch
        {
            ContributionOutcome.Created =>
                StatusCode(
                    StatusCodes.Status201Created,
                    result.Response),

            ContributionOutcome.Replayed =>
                StatusCode(
                    result.OriginalStatusCode
                        ?? StatusCodes.Status200OK,
                    result.Response),

            ContributionOutcome.StokvelNotFound =>
                ProblemResponses.NotFound(
                    "Stokvel not found.",
                    HttpContext.Request.Path),

            ContributionOutcome.UserNotFound =>
                ProblemResponses.NotFound(
                    "User not found.",
                    HttpContext.Request.Path),

            ContributionOutcome.NotMember =>
                ProblemResponses.UnprocessableEntity(
                    "User is not a member of this stokvel.",
                    HttpContext.Request.Path),

            ContributionOutcome.InvalidAmount =>
                ProblemResponses.UnprocessableEntity(
                    "Contribution amount must be greater than zero.",
                    HttpContext.Request.Path),

            ContributionOutcome.InvalidCycle =>
                ProblemResponses.BadRequest(
                    "Cycle must use the YYYY-MM format.",
                    HttpContext.Request.Path),

            ContributionOutcome.DuplicateContribution =>
                ProblemResponses.Conflict(
                    "A contribution already exists for this member and cycle.",
                    HttpContext.Request.Path),

            ContributionOutcome.IdempotencyKeyConflict =>
                ProblemResponses.Conflict(
                    "Idempotency-Key was already used with a different request.",
                    HttpContext.Request.Path),

            _ =>
                ProblemResponses.BadRequest(
                    "The contribution request could not be processed.",
                    HttpContext.Request.Path)
        };
    }
}