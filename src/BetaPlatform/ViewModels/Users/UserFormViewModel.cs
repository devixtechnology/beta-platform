using System.ComponentModel.DataAnnotations;

namespace BetaPlatform.ViewModels.Users;

/// <summary>Create and edit form for a user account. On edit the email is read-only, and the
/// password field is unused — a password is changed through Reset password instead.</summary>
public class UserFormViewModel
{
    public string? Id { get; set; }

    [Required(ErrorMessage = "Email is required.")]
    [EmailAddress(ErrorMessage = "Enter a valid email address.")]
    [MaxLength(256)]
    [Display(Name = "Email")]
    public string Email { get; set; } = string.Empty;

    [Required(ErrorMessage = "Full name is required.")]
    [MaxLength(100)]
    [Display(Name = "FullName")]
    public string FullName { get; set; } = string.Empty;

    [Required(ErrorMessage = "Role is required.")]
    [Display(Name = "Role")]
    public string Role { get; set; } = string.Empty;

    [Display(Name = "Active")]
    public bool IsActive { get; set; } = true;

    /// <summary>Initial password — required on create, ignored on edit.</summary>
    [DataType(DataType.Password)]
    [Display(Name = "Password")]
    public string? Password { get; set; }

    /// <summary>True when the form is editing an existing account (drives the read-only email and
    /// the absence of the password field).</summary>
    public bool IsEdit => !string.IsNullOrEmpty(Id);
}
