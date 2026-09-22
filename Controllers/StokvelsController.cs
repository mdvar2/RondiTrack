using Microsoft.AspNetCore.Mvc;
using RondiTrack.Models;
using RondiTrack.Repositories;

namespace RondiTrack.Controllers;

[ApiController]
[Route("api/stokvels")]
public class StokvelsController : ControllerBase
{
    private readonly IStokvelRepository _stokvelRepository;
    private readonly IUserRepository _userRepository;

    public StokvelsController(
        IStokvelRepository stokvelRepository,
        IUserRepository userRepository)
    {
        _stokvelRepository = stokvelRepository;
        _userRepository = userRepository;
    }

    [HttpGet]
    public async Task<ActionResult<IEnumerable<Stokvel>>> GetAll()
    {
        var stokvels = await _stokvelRepository.GetAllAsync();
        return Ok(stokvels);
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<Stokvel>> GetById(Guid id)
    {
        var stokvel = await _stokvelRepository.GetByIdAsync(id);

        if (stokvel is null)
            return NotFound();

        return Ok(stokvel);
    }

    [HttpPost]
    public async Task<ActionResult<Stokvel>> Create(Stokvel stokvel)
    {
        await _stokvelRepository.AddAsync(stokvel);

        return CreatedAtAction(
            nameof(GetById),
            new { id = stokvel.Id },
            stokvel);
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, Stokvel updatedStokvel)
    {
        var stokvel = await _stokvelRepository.GetByIdAsync(id);

        if (stokvel is null)
            return NotFound();

        try
        {
            stokvel.UpdateDetails(
                updatedStokvel.Name,
                updatedStokvel.ContributionAmount);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { message = ex.Message });
        }

        return NoContent();
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        var deleted = await _stokvelRepository.DeleteAsync(id);

        if (!deleted)
            return NotFound();

        return NoContent();
    }

    [HttpPost("{stokvelId:guid}/members/{userId:guid}")]
    public async Task<IActionResult> AddMember(Guid stokvelId, Guid userId)
    {
        var stokvel = await _stokvelRepository.GetByIdAsync(stokvelId);

        if (stokvel is null)
            return NotFound(new { message = "Stokvel not found." });

        var user = await _userRepository.GetByIdAsync(userId);

        if (user is null)
            return NotFound(new { message = "User not found." });

        try
        {
            stokvel.AddMember(user);
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(new { message = ex.Message });
        }

        return NoContent();
    }

    [HttpDelete("{stokvelId:guid}/members/{userId:guid}")]
    public async Task<IActionResult> RemoveMember(Guid stokvelId, Guid userId)
    {
        var stokvel = await _stokvelRepository.GetByIdAsync(stokvelId);

        if (stokvel is null)
            return NotFound(new { message = "Stokvel not found." });

        try
        {
            stokvel.RemoveMember(userId);
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(new { message = ex.Message });
        }

        return NoContent();
    }
}