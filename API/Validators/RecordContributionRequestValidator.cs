using FluentValidation;
using RondiTrack.DTOs.Contributions;

namespace RondiTrack.Validators;

public class RecordContributionRequestValidator
    : AbstractValidator<RecordContributionRequest>
{
    public const long MaxAmountInCents = 100_000_000;

    public RecordContributionRequestValidator()
    {
        RuleFor(request => request.AmountInCents)
            .GreaterThan(0)
            .WithMessage(
                "Contribution amount must be greater than zero.")
            // The web form refuses the same maximum before it calls the API
            // (web-next/src/lib/money.ts, MAX_CONTRIBUTION_CENTS).
            .LessThanOrEqualTo(MaxAmountInCents)
            .WithMessage(
                "Contribution amount must be R1 000 000 or less.");

        RuleFor(request => request.ContributionCycleId)
            .NotEmpty()
            .WithMessage(
                "Contribution cycle ID is required.");
    }
}