using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using BetaPlatform.Data;
using BetaPlatform.Data.Entities;
using BetaPlatform.ViewModels.Users;

namespace BetaPlatform.Services;

/// <summary>
/// User administration on top of the Identity tables that already exist — no repository layer, no
/// schema change (004 — contracts/user-management.md). It wraps <see cref="UserManager{TUser}"/>
/// and <see cref="RoleManager{TRole}"/> directly and returns the platform's existing
/// <see cref="ServiceResult"/> so controllers keep their current success/error + TempData pattern.
/// </summary>
public interface IUserAdminService
{
    Task<List<UserListViewModel>> GetAllAsync();
    Task<UserFormViewModel?> GetForEditAsync(string id);
    Task<ServiceResult<ApplicationUser>> CreateAsync(UserFormViewModel model);
    Task<ServiceResult> UpdateAsync(UserFormViewModel model);
    Task<ServiceResult> SetActiveAsync(string id, bool isActive);
    Task<ServiceResult> ResetPasswordAsync(string id, string newPassword);
}

public class UserAdminService : IUserAdminService
{
    private readonly UserManager<ApplicationUser> _users;
    private readonly RoleManager<IdentityRole> _roles;

    /// <summary>The roles this platform recognises. Two is the whole permission model (Principle III).</summary>
    public static readonly string[] AssignableRoles = { DbSeeder.AdminRole, DbSeeder.ClientRole };

    public UserAdminService(UserManager<ApplicationUser> users, RoleManager<IdentityRole> roles)
    {
        _users = users;
        _roles = roles;
    }

    public async Task<List<UserListViewModel>> GetAllAsync()
    {
        var users = await _users.Users.OrderBy(u => u.Email).ToListAsync();

        var list = new List<UserListViewModel>(users.Count);
        foreach (var user in users)
        {
            list.Add(new UserListViewModel
            {
                Id = user.Id,
                Email = user.Email ?? string.Empty,
                FullName = user.FullName,
                Role = await GetRoleAsync(user),
                IsActive = user.IsActive,
                CreatedAt = user.CreatedAt
            });
        }
        return list;
    }

    public async Task<UserFormViewModel?> GetForEditAsync(string id)
    {
        var user = await _users.FindByIdAsync(id);
        if (user is null) return null;

        return new UserFormViewModel
        {
            Id = user.Id,
            Email = user.Email ?? string.Empty,
            FullName = user.FullName,
            Role = await GetRoleAsync(user),
            IsActive = user.IsActive
        };
    }

    public async Task<ServiceResult<ApplicationUser>> CreateAsync(UserFormViewModel model)
    {
        var email = (model.Email ?? string.Empty).Trim();

        if (!IsAssignableRole(model.Role))
            return ServiceResult<ApplicationUser>.Fail($"'{model.Role}' is not a valid role.");
        if (string.IsNullOrWhiteSpace(model.Password))
            return ServiceResult<ApplicationUser>.Fail("A password is required.");

        // Explicit pre-check so a duplicate reads as a plain sentence rather than an Identity code (FR-012).
        if (await _users.FindByEmailAsync(email) is not null)
            return ServiceResult<ApplicationUser>.Fail($"An account with the email '{email}' already exists.");

        await EnsureRoleExistsAsync(model.Role);

        var user = new ApplicationUser
        {
            UserName = email,
            Email = email,
            EmailConfirmed = true,
            FullName = (model.FullName ?? string.Empty).Trim(),
            IsActive = model.IsActive
        };

        var created = await _users.CreateAsync(user, model.Password!);
        if (!created.Succeeded)
            return ServiceResult<ApplicationUser>.Fail(Describe(created));

        var roleAssigned = await _users.AddToRoleAsync(user, model.Role);
        if (!roleAssigned.Succeeded)
        {
            // Never leave a roleless account behind — it would sign in and see nothing it may use.
            await _users.DeleteAsync(user);
            return ServiceResult<ApplicationUser>.Fail(Describe(roleAssigned));
        }

        return ServiceResult<ApplicationUser>.Ok(user);
    }

    public async Task<ServiceResult> UpdateAsync(UserFormViewModel model)
    {
        if (string.IsNullOrEmpty(model.Id))
            return ServiceResult.Fail("User not found.");
        if (!IsAssignableRole(model.Role))
            return ServiceResult.Fail($"'{model.Role}' is not a valid role.");

        var user = await _users.FindByIdAsync(model.Id);
        if (user is null) return ServiceResult.Fail("User not found.");

        var currentRole = await GetRoleAsync(user);
        var losingAdmin = currentRole == DbSeeder.AdminRole &&
                          (model.Role != DbSeeder.AdminRole || !model.IsActive);

        // The platform must never be left without a way in (FR-013).
        if (losingAdmin && !await OtherActiveAdminExistsAsync(user.Id))
        {
            return ServiceResult.Fail(
                "This is the last active administrator — it cannot be deactivated or changed to a client account.");
        }

        user.FullName = (model.FullName ?? string.Empty).Trim();

        var wasActive = user.IsActive;
        user.IsActive = model.IsActive;

        var updated = await _users.UpdateAsync(user);
        if (!updated.Succeeded) return ServiceResult.Fail(Describe(updated));

        if (currentRole != model.Role)
        {
            await EnsureRoleExistsAsync(model.Role);
            if (!string.IsNullOrEmpty(currentRole))
            {
                var removed = await _users.RemoveFromRoleAsync(user, currentRole);
                if (!removed.Succeeded) return ServiceResult.Fail(Describe(removed));
            }
            var added = await _users.AddToRoleAsync(user, model.Role);
            if (!added.Succeeded) return ServiceResult.Fail(Describe(added));
        }

        // Losing access must take effect on the next request, not at the next sign-out (research T4).
        if (wasActive && !model.IsActive)
            await _users.UpdateSecurityStampAsync(user);

        return ServiceResult.Ok();
    }

    public async Task<ServiceResult> SetActiveAsync(string id, bool isActive)
    {
        var user = await _users.FindByIdAsync(id);
        if (user is null) return ServiceResult.Fail("User not found.");

        if (!isActive && await IsInRoleAsync(user, DbSeeder.AdminRole) && !await OtherActiveAdminExistsAsync(user.Id))
            return ServiceResult.Fail("This is the last active administrator — it cannot be deactivated.");

        user.IsActive = isActive;
        var updated = await _users.UpdateAsync(user);
        if (!updated.Succeeded) return ServiceResult.Fail(Describe(updated));

        if (!isActive)
        {
            // Rotating the security stamp invalidates cookies already issued to this account, so an
            // open session is rejected within SecurityStampValidatorOptions.ValidationInterval.
            await _users.UpdateSecurityStampAsync(user);
        }

        return ServiceResult.Ok();
    }

    public async Task<ServiceResult> ResetPasswordAsync(string id, string newPassword)
    {
        var user = await _users.FindByIdAsync(id);
        if (user is null) return ServiceResult.Fail("User not found.");

        var token = await _users.GeneratePasswordResetTokenAsync(user);
        var reset = await _users.ResetPasswordAsync(user, token, newPassword);
        if (!reset.Succeeded) return ServiceResult.Fail(Describe(reset));

        // The old password stops working immediately, including on sessions already signed in (FR-008).
        await _users.UpdateSecurityStampAsync(user);
        return ServiceResult.Ok();
    }

    // ---- helpers ----

    private static bool IsAssignableRole(string? role) =>
        !string.IsNullOrWhiteSpace(role) && AssignableRoles.Contains(role);

    private async Task EnsureRoleExistsAsync(string role)
    {
        if (!await _roles.RoleExistsAsync(role))
            await _roles.CreateAsync(new IdentityRole(role));
    }

    private async Task<string> GetRoleAsync(ApplicationUser user) =>
        (await _users.GetRolesAsync(user)).FirstOrDefault() ?? string.Empty;

    private async Task<bool> IsInRoleAsync(ApplicationUser user, string role) =>
        await _users.IsInRoleAsync(user, role);

    /// <summary>True when at least one <b>other</b> active administrator exists.</summary>
    private async Task<bool> OtherActiveAdminExistsAsync(string excludeUserId)
    {
        var admins = await _users.GetUsersInRoleAsync(DbSeeder.AdminRole);
        return admins.Any(a => a.Id != excludeUserId && a.IsActive);
    }

    private static string Describe(IdentityResult result) =>
        string.Join(" ", result.Errors.Select(e => e.Description));
}
