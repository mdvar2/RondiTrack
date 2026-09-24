using System.Globalization;
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

        RuleFor(request => request.Cycle)
            .NotEmpty()
            .WithMessage("Contribution cycle is required.")
            .Must(BeValidCycle)
            .WithMessage("Cycle must use the YYYY-MM format.");
    }

    private static bool BeValidCycle(string cycle)
    {
        if (string.IsNullOrWhiteSpace(cycle))
            return false;

        return DateTime.TryParseExact(
            cycle,
            "yyyy-MM",
            CultureInfo.InvariantCulture,
            DateTimeStyles.None,
            out _);
    }
}