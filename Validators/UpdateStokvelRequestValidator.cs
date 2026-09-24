using FluentValidation;
using RondiTrack.DTOs.Stokvels;

namespace RondiTrack.Validators;

public class UpdateStokvelRequestValidator
    : AbstractValidator<UpdateStokvelRequest>
{
    public UpdateStokvelRequestValidator()
    {
        RuleFor(request => request.Name)
            .NotEmpty()
            .WithMessage("Stokvel name is required.");

        RuleFor(request => request.ContributionAmount)
            .GreaterThan(0)
            .WithMessage(
                "Contribution amount must be greater than zero.");
    }
}