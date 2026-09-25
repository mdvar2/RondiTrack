using FluentValidation;
using RondiTrack.DTOs.Contributions;

namespace RondiTrack.Validators;

public class RecordContributionRequestValidator
    : AbstractValidator<RecordContributionRequest>
{
    public RecordContributionRequestValidator()
    {
        RuleFor(request => request.Amount)
            .GreaterThan(0)
            .WithMessage(
                "Contribution amount must be greater than zero.");

        RuleFor(request => request.ContributionCycleId)
            .NotEmpty()
            .WithMessage(
                "Contribution cycle ID is required.");
    }
}