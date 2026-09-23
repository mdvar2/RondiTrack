using RondiTrack.Models;

namespace RondiTrack.DTOs.Users;

public record UserResponse(
    Guid Id,
    string Name,
    string Email
)
{
    public static UserResponse FromEntity(User user)
    {
        return new UserResponse(
            user.Id,
            user.Name,
            user.Email
        );
    }
}
