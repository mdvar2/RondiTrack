using Microsoft.AspNetCore.Identity;
using RondiTrack.Domain.Entities;
using RondiTrack.Domain.Exceptions;
using RondiTrack.Domain.Repositories;
using RondiTrack.DTOs.Auth;
using RondiTrack.DTOs.Users;

namespace RondiTrack.Auth;

public class AuthService(IUserRepository users, IPasswordHasher<User> passwordHasher)
{
    public async Task<UserResponse> RegisterAsync(RegisterRequest request)
    {
        if (await users.GetByNormalizedEmailAsync(User.NormalizeEmail(request.Email)) is not null)
            throw new BusinessRuleException("An account with this email already exists.");
        var user = new User(request.Name, request.Email);
        user.SetPasswordHash(passwordHasher.HashPassword(user, request.Password));
        await users.AddAsync(user);
        return UserResponse.FromEntity(user);
    }
}
