namespace BetaPlatform.Services.Api;

/// <summary>
/// The one place product codes are normalised and compared (005 research R9).
/// </summary>
/// <remarks>
/// Centralised deliberately. MySQL's default collation is already case-insensitive, so a data-backed
/// implementation written without thinking would match case-insensitively <em>by accident</em> on the
/// server while any in-memory check matched case-sensitively — a discrepancy that surfaces as
/// failures nobody can reproduce. One helper makes both halves agree on purpose.
/// </remarks>
public static class ProductCode
{
    /// <summary>Trims surrounding whitespace. A code is stored and echoed in this form — the plant
    /// treats it as a printed label, so accidental padding is not part of its identity.</summary>
    public static string Normalise(string? code) => code?.Trim() ?? string.Empty;

    /// <summary>
    /// True when two codes address the same product: trimmed, compared case-insensitively.
    /// An empty or whitespace-only code never matches anything, including another empty one —
    /// "no code" is not an identity.
    /// </summary>
    public static bool Matches(string? a, string? b)
    {
        var left = Normalise(a);
        var right = Normalise(b);

        if (left.Length == 0 || right.Length == 0)
        {
            return false;
        }

        return string.Equals(left, right, StringComparison.OrdinalIgnoreCase);
    }
}
