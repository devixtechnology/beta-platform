using BetaPlatform.Data.Entities;

namespace BetaPlatform.Services.Api;

/// <summary>Claim type names used on the API surface.</summary>
/// <remarks>
/// Short names rather than the long WS-Federation URIs, so the token payload an integrator decodes
/// reads cleanly. <c>TokenValidationParameters.NameClaimType</c> and <c>RoleClaimType</c> are
/// pointed at these in <c>Program.cs</c>, and inbound claim mapping is switched off, so what is
/// issued here is exactly what arrives back.
/// </remarks>
public static class ApiClaimTypes
{
    public const string Email = "email";
    public const string Name = "name";
    public const string Role = "role";

    /// <summary>
    /// Says what a token is <em>for</em>: <c>access</c> or <c>refresh</c> (research R12). The two
    /// already carry different audiences, so this claim is not what keeps them apart — it is what
    /// makes a decoded token say so in words, and what lets the refusal name the real reason
    /// instead of "wrong audience".
    /// </summary>
    public const string TokenUse = "token_use";

    /// <summary>Value of <see cref="TokenUse"/> on a token that opens the API.</summary>
    public const string AccessTokenUse = "access";

    /// <summary>Value of <see cref="TokenUse"/> on a token that can only buy a new pair.</summary>
    public const string RefreshTokenUse = "refresh";
}

/// <summary>
/// The per-request revocation check (FR-008, research R3).
/// </summary>
/// <remarks>
/// A stateless 8-hour token would otherwise keep working for the rest of the shift after an account
/// is deactivated. Feature 004 deactivates by rotating the Identity security stamp, and the cookie
/// pipeline already re-validates against that stamp every minute — checking the same stamp here
/// means deactivation has <em>one</em> meaning on both doors into the application rather than a
/// cookie rule and a token rule that drift apart.
///
/// Kept a pure function so every branch is unit-testable without a web host (research R11).
/// </remarks>
public static class ApiTokenValidation
{
    /// <summary>The Identity security stamp, carried as a claim and compared against the store on
    /// every request.</summary>
    public const string SecurityStampClaimType = "AspNet.Identity.SecurityStamp";

    /// <summary>
    /// True only when the account behind a token is still entitled to use it.
    /// </summary>
    /// <param name="user">The account named by the token, or <c>null</c> if it no longer exists.</param>
    /// <param name="securityStampClaim">The stamp carried in the token at issue.</param>
    public static bool IsStillValid(ApplicationUser? user, string? securityStampClaim)
    {
        // The account was deleted after the token was issued.
        if (user is null)
        {
            return false;
        }

        // Deactivated: access was withdrawn and must bite now, not at expiry.
        if (!user.IsActive)
        {
            return false;
        }

        // A token with no stamp claim cannot be checked, so it is not trusted. This also refuses a
        // token minted before the stamp claim existed.
        if (string.IsNullOrEmpty(securityStampClaim))
        {
            return false;
        }

        // Rotated stamp — deactivation, a password change, or an explicit sign-out everywhere.
        return string.Equals(user.SecurityStamp, securityStampClaim, StringComparison.Ordinal);
    }
}
