using FluentValidation;
using RondiTrack.DTOs.Users;

namespace RondiTrack.Validators;

public class CreateUserRequestValidator
    : AbstractValidator<CreateUserRequest>
{
    public CreateUserRequestValidator()
    {
        RuleFor(request => request.Name)
            .NotEmpty()
            .WithMessage("Name is required.");

        RuleFor(request => request.Email)
            .NotEmpty()
            .WithMessage("Email is required.")
            .EmailAddress()
            .WithMessage("A valid email is required.");
    }
}