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

    [HttpGet]
    public async Task<ActionResult<IEnumerable<UserResponse>>> GetAll()
    {
        var users = await _userRepository.GetAllAsync();

        var response = users
            .Select(UserResponse.FromEntity)
            .ToList();

        return Ok(response);
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<UserResponse>> GetById(Guid id)
    {
        var user = await _userRepository.GetByIdAsync(id);

        if (user is null)
            throw new NotFoundException("User not found.");

        return Ok(UserResponse.FromEntity(user));
    }

    [HttpPost]
    public async Task<ActionResult<UserResponse>> Create(
        CreateUserRequest request)
    {
        var user = new User(
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

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(
        Guid id,
        UpdateUserRequest request)
    {
        var user =
            await _userRepository.GetByIdAsync(id);

        if (user is null)
            throw new NotFoundException("User not found.");

        user.UpdateDetails(
            request.Name,
            request.Email);

        return NoContent();
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        var deleted =
            await _userRepository.DeleteAsync(id);

        if (!deleted)
            throw new NotFoundException("User not found.");

        return NoContent();
    }
}