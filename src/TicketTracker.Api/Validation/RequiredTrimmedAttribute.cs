using System.ComponentModel.DataAnnotations;

namespace TicketTracker.Api.Validation;

[AttributeUsage(AttributeTargets.Property | AttributeTargets.Parameter)]
public sealed class RequiredTrimmedAttribute(int maxLength) : ValidationAttribute
{
    protected override ValidationResult? IsValid(object? value, ValidationContext validationContext)
    {
        var name = validationContext.DisplayName;
        if (value is not string text || string.IsNullOrWhiteSpace(text))
        {
            return new ValidationResult($"{name} is required.");
        }

        if (text.Trim().Length > maxLength)
        {
            return new ValidationResult($"{name} must be at most {maxLength} characters.");
        }

        return ValidationResult.Success;
    }
}
