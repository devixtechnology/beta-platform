using System.ComponentModel.DataAnnotations;

namespace BetaPlatform.ViewModels.Api;

/// <summary>Requires a value greater than zero.</summary>
/// <remarks>
/// <para>
/// Used instead of <c>[Range(0.0001, double.MaxValue)]</c> for two reasons. The plain one: the
/// OpenAPI document generator formats a range bound into the schema and cannot parse
/// <c>double.MaxValue</c> back out ("1.7976931348623157E+308"), which takes down the whole published
/// contract at <c>/openapi/v1.json</c> with a 500 — so the contract this feature promises would not
/// have been readable at all.
/// </para>
/// <para>
/// The better one: there is no upper bound here. The rule is "more than nothing", and inventing a
/// ceiling to satisfy an attribute's signature would eventually reject a legitimate order.
/// </para>
/// </remarks>
[AttributeUsage(AttributeTargets.Property)]
public sealed class GreaterThanZeroAttribute : ValidationAttribute
{
    protected override ValidationResult? IsValid(object? value, ValidationContext validationContext)
    {
        // Absence is Required's business, not ours.
        if (value is null)
        {
            return ValidationResult.Success;
        }

        var number = Convert.ToDecimal(value);
        if (number > 0m)
        {
            return ValidationResult.Success;
        }

        return new ValidationResult(
            ErrorMessage ?? $"The {validationContext.MemberName} field must be greater than zero.",
            validationContext.MemberName is null ? null : [validationContext.MemberName]);
    }
}

/// <summary>Requires a value of zero or more. See <see cref="GreaterThanZeroAttribute"/> for why
/// this exists rather than an open-ended <c>[Range]</c>.</summary>
[AttributeUsage(AttributeTargets.Property)]
public sealed class NotNegativeAttribute : ValidationAttribute
{
    protected override ValidationResult? IsValid(object? value, ValidationContext validationContext)
    {
        if (value is null)
        {
            return ValidationResult.Success;
        }

        var number = Convert.ToDecimal(value);
        if (number >= 0m)
        {
            return ValidationResult.Success;
        }

        return new ValidationResult(
            ErrorMessage ?? $"The {validationContext.MemberName} field must not be negative.",
            validationContext.MemberName is null ? null : [validationContext.MemberName]);
    }
}
