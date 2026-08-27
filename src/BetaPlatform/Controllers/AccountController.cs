using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using BetaPlatform.Data.Entities;
using BetaPlatform.ViewModels.Account;

namespace BetaPlatform.Controllers;

/// <summary>
/// A signed-in user's own account. Available to both roles under the global authorization policy —
/// everyone must be able to change their own password (004 — FR-007).
/// </summary>
public class AccountController : Controller
{
    private readonly UserManager<ApplicationUser> _users;
    private readonly SignInManager<ApplicationUser> _signInManager;

    public AccountController(UserManager<ApplicationUser> users, SignInManager<ApplicationUser> signInManager)
    {
        _users = users;
        _signInManager = signInManager;
    }

    [HttpGet]
    public IActionResult ChangePassword() => View(new ChangePasswordViewModel());

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ChangePassword(ChangePasswordViewModel model)
    {
        if (!ModelState.IsValid) return View(model);

        var user = await _users.GetUserAsync(User);
        if (user is null) return Challenge();

        if (model.CurrentPassword == model.NewPassword)
        {
            ModelState.AddModelError(nameof(model.NewPassword), "The new password must be different from the current one.");
            return View(model);
        }

        var result = await _users.ChangePasswordAsync(user, model.CurrentPassword, model.NewPassword);
        if (!result.Succeeded)
        {
            foreach (var error in result.Errors)
                ModelState.AddModelError(string.Empty, error.Description);
            return View(model);
        }

        // Changing the password rotates the security stamp, which would otherwise sign this user out
        // on their next request — refresh the cookie so they stay where they are.
        await _signInManager.RefreshSignInAsync(user);

        TempData["Success"] = "Your password has been changed.";
        return RedirectToAction("Index", "Dashboard");
    }
}
