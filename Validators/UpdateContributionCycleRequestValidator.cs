using System.Globalization;
using FluentValidation;
using RondiTrack.DTOs.ContributionCycles;

namespace RondiTrack.Validators;

public class UpdateContributionCycleRequestValidator
    : AbstractValidator<UpdateContributionCycleRequest>
{
    public UpdateContributionCycleRequestValidator()
    {
        RuleFor(request => request.Period)
            .NotEmpty()
            .WithMessage("Contribution cycle period is required.")
            .Must(BeValidPeriod)
            .WithMessage("Period must use the YYYY-MM format.");

        RuleFor(request => request.TargetAmount)
            .GreaterThan(0)
            .WithMessage("Target amount must be greater than zero.");
    }

    private static bool BeValidPeriod(string period)
    {
        if (string.IsNullOrWhiteSpace(period))
            return false;

        return DateTime.TryParseExact(
            period,
            "yyyy-MM",
            CultureInfo.InvariantCulture,
            DateTimeStyles.None,
            out _);
    }
}