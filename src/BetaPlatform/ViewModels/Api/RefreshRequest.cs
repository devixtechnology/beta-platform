using System.ComponentModel.DataAnnotations;

namespace BetaPlatform.ViewModels.Api;

/// <summary>The refresh token presented to <c>POST /api/v1/auth/refresh</c>.</summary>
/// <remarks>
/// Only presence is validated. A token that is expired, altered, signed with another key, or is an
/// access token wearing the wrong hat is a <em>rejected credential</em> (401), not a malformed
/// request (400) — the same line <see cref="LoginRequest"/> draws between a wrong password and a
/// missing one, and for the same reason: a caller must not be able to learn from a 400/401 split
/// which part of a stolen token it got wrong.
///
/// It travels in the body rather than in a header or the query string, so it cannot end up in an
/// access log or a browser history alongside the URL.
/// </remarks>
public class RefreshRequest
{
    [Required]
    public string RefreshToken { get; set; } = string.Empty;
}
