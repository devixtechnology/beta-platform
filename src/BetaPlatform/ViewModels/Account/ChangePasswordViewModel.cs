using System.ComponentModel.DataAnnotations;

namespace BetaPlatform.ViewModels.Account;

/// <summary>A signed-in user changing their own password (004 — FR-007).</summary>
public class ChangePasswordViewModel
{
    [Required(ErrorMessage = "Your current password is required.")]
    [DataType(DataType.Password)]
    [Display(Name = "CurrentPassword")]
    public string CurrentPassword { get; set; } = string.Empty;

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
