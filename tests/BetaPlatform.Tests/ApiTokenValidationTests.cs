using BetaPlatform.Data.Entities;
using BetaPlatform.Services.Api;
using Xunit;

namespace BetaPlatform.Tests;

/// <summary>
/// The per-request revocation check (005 FR-008). This predicate is the whole reason a deactivated
/// account loses API access on its next call rather than eight hours later, so each way it can fail
/// is asserted separately.
/// </summary>
public class ApiTokenValidationTests
{
    private static ApplicationUser NewUser(bool isActive = true, string stamp = "STAMP-1") => new()
    {
        Id = "user-1",
        Email = "user@beta.local",
        FullName = "A User",
        SecurityStamp = stamp,
        IsActive = isActive
    };

    [Fact]
    public void Active_User_With_Matching_Stamp_Is_Valid()
    {
        Assert.True(ApiTokenValidation.IsStillValid(NewUser(), "STAMP-1"));
    }

    [Fact]
    public void Deleted_Account_Is_Rejected()
    {
        Assert.False(ApiTokenValidation.IsStillValid(null, "STAMP-1"));
    }

    [Fact]
    public void Deactivated_Account_Is_Rejected()
    {
        // The core of FR-008: the token is structurally fine, the account is not.
        Assert.False(ApiTokenValidation.IsStillValid(NewUser(isActive: false), "STAMP-1"));
    }

    [Fact]
    public void Rotated_Stamp_Is_Rejected()
    {
        // Deactivation and a password change both rotate the stamp, so both kill outstanding tokens.
        Assert.False(ApiTokenValidation.IsStillValid(NewUser(stamp: "STAMP-2"), "STAMP-1"));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void Token_Without_A_Stamp_Claim_Is_Rejected(string? stampClaim)
    {
        // A token that cannot be checked is not trusted — this also refuses one minted before the
        // stamp claim existed.
        Assert.False(ApiTokenValidation.IsStillValid(NewUser(), stampClaim));
    }

    [Fact]
    public void Stamp_Comparison_Is_Case_Sensitive()
    {
        // Unlike a product code, a security stamp is an opaque key, not a printed label.
        Assert.False(ApiTokenValidation.IsStillValid(NewUser(stamp: "STAMP-1"), "stamp-1"));
    }
}
