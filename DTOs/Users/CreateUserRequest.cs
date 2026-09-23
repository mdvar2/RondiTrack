namespace RondiTrack.DTOs.Users;

public record CreateUserRequest(
    string Name,
    string Email
);
