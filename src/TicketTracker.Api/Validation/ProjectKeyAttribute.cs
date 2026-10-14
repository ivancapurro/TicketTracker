using System.ComponentModel.DataAnnotations;
using TicketTracker.Api.Domain;

namespace TicketTracker.Api.Validation;

[AttributeUsage(AttributeTargets.Property | AttributeTargets.Parameter)]
public sealed class ProjectKeyAttribute : ValidationAttribute
{
    protected override ValidationResult? IsValid(object? value, ValidationContext validationContext)
    {
        if (value is not string key || string.IsNullOrWhiteSpace(key))
        {
            return new ValidationResult("Key is required.");
        }

        if (!ProjectKeys.IsValid(ProjectKeys.Normalize(key)))
        {
            return new ValidationResult("Key must be 2 to 10 uppercase letters (A-Z), for example TT.");
        }

        return ValidationResult.Success;
    }
}
