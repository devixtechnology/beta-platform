using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using BetaPlatform.Data;
using BetaPlatform.Data.Entities;
using BetaPlatform.Services;
using BetaPlatform.ViewModels.Users;

namespace BetaPlatform.Controllers;

/// <summary>
/// User administration (004 — client comment 1). Administrator-only at the controller level, on top
/// of the global policy that already requires an authenticated user. Thin: it maps and delegates —
/// every rule lives in <see cref="IUserAdminService"/>.
/// </summary>
[Authorize(Roles = DbSeeder.AdminRole)]
public class UsersController : Controller
{
    private readonly IUserAdminService _users;
    private readonly UserManager<ApplicationUser> _userManager;

    public UsersController(IUserAdminService users, UserManager<ApplicationUser> userManager)
    {
        _users = users;
        _userManager = userManager;
    }

    public async Task<IActionResult> Index()
    {
        var users = await _users.GetAllAsync();
        return View(users);
    }

    [HttpGet]
    public IActionResult Create()
    {
        PopulateRoles(DbSeeder.ClientRole);
        return View(new UserFormViewModel { Role = DbSeeder.ClientRole, IsActive = true });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(UserFormViewModel model)
    {
        if (string.IsNullOrWhiteSpace(model.Password))
            ModelState.AddModelError(nameof(model.Password), "A password is required.");

        if (!ModelState.IsValid)
        {
            PopulateRoles(model.Role);
            return View(model);
        }

        var result = await _users.CreateAsync(model);
        if (!result.Success)
        {
            ModelState.AddModelError(string.Empty, result.Error!);
            PopulateRoles(model.Role);
            return View(model);
        }

        TempData["Success"] = "User account created.";
        return RedirectToAction(nameof(Index));
    }

    [HttpGet]
    public async Task<IActionResult> Edit(string id)
    {
        var model = await _users.GetForEditAsync(id);
        if (model is null) return NotFound();
        PopulateRoles(model.Role);
        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(string id, UserFormViewModel model)
    {
        if (id != model.Id) return BadRequest();

        // The email is read-only on edit, so it is not required to round-trip through the form.
        ModelState.Remove(nameof(model.Password));

        if (!ModelState.IsValid)
        {
            PopulateRoles(model.Role);
            return View(model);
        }

        if (!model.IsActive && IsCurrentUser(model.Id))
        {
            ModelState.AddModelError(string.Empty, "You cannot deactivate your own account.");
            PopulateRoles(model.Role);
            return View(model);
        }

        var result = await _users.UpdateAsync(model);
        if (!result.Success)
        {
            ModelState.AddModelError(string.Empty, result.Error!);
            PopulateRoles(model.Role);
            return View(model);
        }

        TempData["Success"] = "User account updated.";
        return RedirectToAction(nameof(Index));
    }

    [HttpGet]
    public async Task<IActionResult> ResetPassword(string id)
    {
        var user = await _users.GetForEditAsync(id);
        if (user is null) return NotFound();

        return View(new ResetPasswordViewModel { Id = user.Id!, Email = user.Email, FullName = user.FullName });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ResetPassword(ResetPasswordViewModel model)
    {
        if (!ModelState.IsValid) return View(model);

        var result = await _users.ResetPasswordAsync(model.Id, model.NewPassword);
        if (!result.Success)
        {
            ModelState.AddModelError(string.Empty, result.Error!);
            return View(model);
        }

        TempData["Success"] = "Password reset. The previous password no longer works.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ToggleActive(string id, bool isActive)
    {
        // An administrator locking themselves out mid-session helps nobody.
        if (!isActive && IsCurrentUser(id))
        {
            TempData["Error"] = "You cannot deactivate your own account.";
            return RedirectToAction(nameof(Index));
        }

        var result = await _users.SetActiveAsync(id, isActive);
        TempData[result.Success ? "Success" : "Error"] = result.Success
            ? (isActive ? "User account reactivated." : "User account deactivated.")
            : result.Error;
        return RedirectToAction(nameof(Index));
    }

    private bool IsCurrentUser(string? id) =>
        !string.IsNullOrEmpty(id) && id == _userManager.GetUserId(User);

    private void PopulateRoles(string? selected) =>
        ViewBag.Roles = new SelectList(UserAdminService.AssignableRoles, selected);
}
