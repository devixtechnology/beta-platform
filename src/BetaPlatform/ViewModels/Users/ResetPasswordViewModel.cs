using System.ComponentModel.DataAnnotations;

namespace BetaPlatform.ViewModels.Users;

/// <summary>An administrator setting a new password on someone else's account. The previous
/// password stops working immediately (FR-008).</summary>
public class ResetPasswordViewModel
{
    [Required]
    public string Id { get; set; } = string.Empty;

    /// <summary>Shown for confirmation so an administrator cannot reset the wrong account.</summary>
    public string Email { get; set; } = string.Empty;

    public string FullName { get; set; } = string.Empty;

    [Required(ErrorMessage = "A new password is required.")]
    [DataType(DataType.Password)]
    [Display(Name = "NewPassword")]
    public string NewPassword { get; set; } = string.Empty;

    [Required(ErrorMessage = "Confirm the new password.")]
    [DataType(DataType.Password)]
    [Compare(nameof(NewPassword), ErrorMessage = "The passwords do not match.")]
    [Display(Name = "ConfirmPassword")]
    public string ConfirmPassword { get; set; } = string.Empty;
}
