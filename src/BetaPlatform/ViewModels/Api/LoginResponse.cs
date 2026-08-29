namespace BetaPlatform.ViewModels.Api;

/// <summary>
/// A successful sign-in or renewal: the token, when it dies, the credential that buys the next one,
/// and who they all speak for.
/// </summary>
/// <remarks>
/// One shape for both <c>POST /auth/login</c> and <c>POST /auth/refresh</c> on purpose (FR-040): a
/// caller writes one parser and one "store these tokens" routine, and a renewal is indistinguishable
/// from a fresh sign-in in everything but how it was obtained.
/// </remarks>
public class LoginResponse
{
    /// <summary>The signed bearer token. Present it as <c>Authorization: Bearer &lt;token&gt;</c>.</summary>
    public string AccessToken { get; set; } = string.Empty;

    public string TokenType { get; set; } = "Bearer";

    /// <summary>
    /// Absolute UTC expiry — 8 hours from issue. Absolute rather than a duration so a caller
    /// comparing against its own clock need not remember when the response arrived.
    /// </summary>
    public DateTime ExpiresAt { get; set; }

    /// <summary>
    /// The longer-lived credential presented to <c>POST /auth/refresh</c> to obtain a new pair
    /// without re-sending a password. Rotated on every renewal, and as secret as the password it
    /// stands in for — store it where a password would be stored, never in a URL or a log.
    /// </summary>
    public string RefreshToken { get; set; } = string.Empty;

    /// <summary>
    /// Absolute UTC expiry of the refresh token — 30 days from issue by default. After this the
    /// caller must sign in with credentials again.
    /// </summary>
    public DateTime RefreshTokenExpiresAt { get; set; }

    public string Email { get; set; } = string.Empty;

    public string FullName { get; set; } = string.Empty;

    /// <summary>Roles held by the account, so a caller knows which operations to offer.</summary>
    public IReadOnlyList<string> Roles { get; set; } = [];
}
