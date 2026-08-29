using System.ComponentModel.DataAnnotations;

namespace BetaPlatform.ViewModels.Api;

/// <summary>Credentials presented to <c>POST /api/v1/auth/login</c>.</summary>
/// <remarks>
/// The password-policy length rule is deliberately <em>not</em> applied here. A three-character
/// password is a wrong password (401), not a malformed request (400); validating it would confirm
/// that a policy exists and invite probing. FR-004 is enforced in the shape of this type as much as
/// in the controller.
/// </remarks>
public class LoginRequest
{
    [Required]
    [EmailAddress]
    public string Email { get; set; } = string.Empty;

    [Required]
    public string Password { get; set; } = string.Empty;
}
