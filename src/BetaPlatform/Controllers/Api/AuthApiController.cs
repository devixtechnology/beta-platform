using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using BetaPlatform.Data.Entities;
using BetaPlatform.Services.Api;
using BetaPlatform.ViewModels.Api;

namespace BetaPlatform.Controllers.Api;

/// <summary>
/// Sign-in and renewal for the integration API. Fully implemented — a real account, a real
/// password check, a real token (FR-001 … FR-004, FR-036 … FR-041).
/// </summary>
[ApiController]
[Route("api/v1/auth")]
[AllowAnonymous]
public class AuthApiController : ControllerBase
{
    /// <summary>One message for every credential failure, so the response cannot be used to
    /// discover which accounts exist.</summary>
    private const string InvalidCredentials = "Invalid credentials.";

    /// <summary>The one message every renewal failure returns, for the same reason.</summary>
    private const string InvalidRefreshToken = "Invalid or expired refresh token.";

    private readonly UserManager<ApplicationUser> _users;
    private readonly IJwtTokenService _tokens;

    public AuthApiController(UserManager<ApplicationUser> users, IJwtTokenService tokens)
    {
        _users = users;
        _tokens = tokens;
    }

    /// <summary>
    /// Signs in and returns an 8-hour bearer token and its refresh token. Fully implemented (not
    /// representative data).
    /// </summary>
    /// <remarks>
    /// An unknown email, a wrong password and a deactivated account all answer the same 401
    /// (FR-004). The browser login page deliberately says more — a person retyping a correct
    /// password deserves to know their account was deactivated — but an anonymous API caller is not
    /// that person, and here the distinction would be an account-existence oracle for anyone
    /// holding a list of email addresses.
    /// </remarks>
    /// <response code="200">A token pair, their absolute UTC expiries, and the account's roles.</response>
    /// <response code="400">The request is missing or malforming a field.</response>
    /// <response code="401">Unknown account, wrong password, or a deactivated account.</response>
    [EndpointSummary("Sign in and receive an 8-hour bearer token and a refresh token")]
    [EndpointDescription("FULLY IMPLEMENTED (not representative). Unknown email, wrong password and a deactivated account all return an identical 401, so the response cannot be used to discover which accounts exist. The response also carries a refresh token: renew with POST /auth/refresh, or by signing in again.")]
    [HttpPost("login")]
    [ProducesResponseType(typeof(LoginResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Login([FromBody] LoginRequest request)
    {
        var user = await _users.FindByEmailAsync(request.Email);
        if (user is null)
        {
            return InvalidCredentialsResult();
        }

        if (!user.IsActive)
        {
            return InvalidCredentialsResult();
        }

        // CheckPasswordAsync rather than SignInManager: this surface issues a token and must not
        // establish a cookie session as a side effect.
        if (!await _users.CheckPasswordAsync(user, request.Password))
        {
            return InvalidCredentialsResult();
        }

        return Ok(await IssueForAsync(user));
    }

    /// <summary>
    /// Exchanges a refresh token for a new access token and a new refresh token. Fully implemented.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Anonymous, like sign-in: the refresh token <em>is</em> the credential, and a caller whose
    /// access token has already expired has nothing else to present (FR-036).
    /// </para>
    /// <para>
    /// The account is re-read on every renewal, so a deactivated account, a rotated security stamp
    /// (deactivation, a password change, a sign-out everywhere) or a deleted account stops renewals
    /// as immediately as it stops requests — a refresh token cannot outlive the access it renews
    /// (FR-038). Roles are re-read too, so a role granted or withdrawn since sign-in takes effect on
    /// the next renewal rather than at the end of the refresh window (FR-039).
    /// </para>
    /// <para>
    /// Renewal <strong>rotates</strong>: the response carries a new refresh token and the caller
    /// stores it in place of the old one (FR-037).
    /// </para>
    /// </remarks>
    /// <response code="200">A new token pair, in the same shape sign-in returns.</response>
    /// <response code="400">The request carries no refreshToken field.</response>
    /// <response code="401">The refresh token is expired, altered, unknown, or its account can no longer use it.</response>
    [EndpointSummary("Exchange a refresh token for a new token pair")]
    [EndpointDescription("FULLY IMPLEMENTED (not representative). Anonymous — the refresh token is the credential. Every failure returns an identical 401. Renewal rotates: store the returned refreshToken in place of the one you presented. Roles and the account's active state are re-read, so a deactivated account or a rotated security stamp stops renewals immediately.")]
    [HttpPost("refresh")]
    [ProducesResponseType(typeof(LoginResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Refresh([FromBody] RefreshRequest request)
    {
        // Genuine token? Right signature, right issuer, right (refresh) audience, not expired.
        var subject = await _tokens.ValidateRefreshTokenAsync(request.RefreshToken);
        if (subject is null)
        {
            return InvalidRefreshTokenResult();
        }

        // Genuine is not the same as still usable. The same predicate the bearer handler applies on
        // every request answers that, so a token and its renewal cannot disagree about who is
        // allowed in (research R3, R12).
        var user = await _users.FindByIdAsync(subject.UserId);
        if (!ApiTokenValidation.IsStillValid(user, subject.SecurityStamp))
        {
            return InvalidRefreshTokenResult();
        }

        return Ok(await IssueForAsync(user!));
    }

    /// <summary>
    /// Mints a pair for an account already proven to be entitled to one, and shapes the response.
    /// Shared by sign-in and renewal so the two cannot drift (FR-040).
    /// </summary>
    private async Task<LoginResponse> IssueForAsync(ApplicationUser user)
    {
        var roles = await _users.GetRolesAsync(user);
        var issued = _tokens.Issue(user, roles);

        return new LoginResponse
        {
            AccessToken = issued.Access.Token,
            TokenType = "Bearer",
            ExpiresAt = issued.Access.ExpiresAt,
            RefreshToken = issued.Refresh.Token,
            RefreshTokenExpiresAt = issued.Refresh.ExpiresAt,
            Email = user.Email ?? string.Empty,
            FullName = user.FullName,
            Roles = roles.ToList()
        };
    }

    /// <summary>
    /// The single 401 every credential failure returns. Routed through the framework's
    /// ProblemDetails factory so this body is identical in shape to every other failure the API
    /// produces (FR-030).
    /// </summary>
    private ObjectResult InvalidCredentialsResult() =>
        Problem(title: InvalidCredentials, statusCode: StatusCodes.Status401Unauthorized);

    /// <summary>
    /// The single 401 every renewal failure returns — expired, altered, an access token presented
    /// in its place, a deleted or deactivated account, a rotated stamp. A caller needs to know only
    /// that it must sign in with credentials again.
    /// </summary>
    private ObjectResult InvalidRefreshTokenResult() =>
        Problem(title: InvalidRefreshToken, statusCode: StatusCodes.Status401Unauthorized);
}
