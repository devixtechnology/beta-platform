using BetaPlatform.Services.Api;
using Xunit;

namespace BetaPlatform.Tests;

/// <summary>
/// Product-code normalisation (005 research R9).
/// </summary>
/// <remarks>
/// Worth testing precisely because the rule is easy to get right by accident and wrong in a way
/// nobody can reproduce: MySQL's default collation is already case-insensitive, so a data-backed
/// implementation would match that way on the server while an in-memory check matched
/// case-sensitively. These tests pin the intended rule rather than the incidental one.
/// </remarks>
public class ProductCodeTests
{
    [Theory]
    [InlineData("RM-STEEL-01", "RM-STEEL-01")]
    [InlineData("rm-steel-01", "RM-STEEL-01")]
    [InlineData("Rm-StEeL-01", "RM-STEEL-01")]
    [InlineData("  RM-STEEL-01  ", "RM-STEEL-01")]
    [InlineData("\tRM-STEEL-01\n", "rm-steel-01")]
    public void Matches_Ignores_Case_And_Surrounding_Whitespace(string left, string right)
    {
        Assert.True(ProductCode.Matches(left, right));
    }

    [Theory]
    [InlineData("RM-STEEL-01", "RM-STEEL-02")]
    [InlineData("RM-STEEL-01", "RM STEEL 01")]
    [InlineData("RM-STEEL-01", "RMSTEEL01")]
    public void Matches_Rejects_Different_Codes(string left, string right)
    {
        // Only surrounding whitespace is insignificant — interior spacing is part of the code.
        Assert.False(ProductCode.Matches(left, right));
    }

    [Theory]
    [InlineData(null, "RM-STEEL-01")]
    [InlineData("", "RM-STEEL-01")]
    [InlineData("   ", "RM-STEEL-01")]
    [InlineData("RM-STEEL-01", null)]
    [InlineData("RM-STEEL-01", "")]
    [InlineData(null, null)]
    [InlineData("", "")]
    [InlineData("  ", "\t")]
    public void Matches_Never_Treats_An_Empty_Code_As_An_Identity(string? left, string? right)
    {
        // Two blanks are not "the same product" — "no code" is not an identity. Without this, a
        // request omitting a code would silently address whichever record also had none.
        Assert.False(ProductCode.Matches(left, right));
    }

    [Theory]
    [InlineData("  RM-STEEL-01  ", "RM-STEEL-01")]
    [InlineData("RM-STEEL-01", "RM-STEEL-01")]
    [InlineData(null, "")]
    public void Normalise_Trims_But_Preserves_Case(string? input, string expected)
    {
        // Case is preserved on the way in: a code is stored and echoed as the plant typed it,
        // minus accidental padding. Only comparison ignores case.
        Assert.Equal(expected, ProductCode.Normalise(input));
    }
}
