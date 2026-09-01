using System.ComponentModel.DataAnnotations;
using BetaPlatform.Services.Api;

namespace BetaPlatform.ViewModels.Api;

/// <summary>
/// Requires a list of product codes carrying at least one entry, no blank entry, and no code twice.
/// </summary>
/// <remarks>
/// <para>
/// Exists because a work order consumes <em>several</em> materials but produces one: the input side
/// is a list, and a list needs rules a single string never did. All three are properties of the
/// <strong>request</strong>, so all three are enforced in this slice — whether each code resolves to
/// a real product remains the data question the behaviour slice answers (FR-033).
/// </para>
/// <para>
/// An empty list is refused rather than treated as "no inputs": a work order that consumes nothing
/// is the same class of mistake as one that manufactures nothing, and
/// <see cref="GreaterThanZeroAttribute"/> already catches that at the edge.
/// </para>
/// <para>
/// A repeated code is refused because it carries no information — the contract attaches no quantity
/// to an input, so naming the same material twice can only be a mistake. Comparison goes through
/// <see cref="ProductCode"/> (research R9), so "rm-steel-01" and " RM-STEEL-01 " count as the same
/// code here exactly as they will when the codes are resolved against the catalogue.
/// </para>
/// </remarks>
[AttributeUsage(AttributeTargets.Property)]
public sealed class ProductCodeListAttribute : ValidationAttribute
{
    protected override ValidationResult? IsValid(object? value, ValidationContext validationContext)
    {
        var member = validationContext.MemberName;
        string[]? members = member is null ? null : [member];

        // Absence is Required's business, not ours.
        if (value is null)
        {
            return ValidationResult.Success;
        }

        var codes = ((IEnumerable<string?>)value).ToList();

        if (codes.Count == 0)
        {
            return new ValidationResult(
                ErrorMessage ?? $"The {member} field must contain at least one product code.",
                members);
        }

        for (var i = 0; i < codes.Count; i++)
        {
            if (string.IsNullOrWhiteSpace(codes[i]))
            {
                // The position, not just the field: a caller sending five codes has to know which
                // one is empty without diffing its own payload.
                return new ValidationResult(
                    $"The {member} field must not contain a blank product code (entry {i + 1}).",
                    members);
            }
        }

        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var code in codes)
        {
            var normalised = ProductCode.Normalise(code);
            if (!seen.Add(normalised))
            {
                return new ValidationResult(
                    $"The {member} field lists the product code '{normalised}' more than once.",
                    members);
            }
        }

        return ValidationResult.Success;
    }
}
