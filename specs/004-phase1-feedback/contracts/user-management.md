# Contract: User Accounts & Access Management

**Feature**: `004-phase1-feedback` | Satisfies FR-006 … FR-017

Covers comment 1: change the admin password, create five client accounts, and turn on access
management. Built on the Identity tables already in the database — no migration.

## Roles

| Role | Scope |
|------|-------|
| `Admin` | everything, including user administration and master data |
| `Client` | all operational screens; may create/edit work orders, record input weights, change work order status |

Seeded by `DbSeeder` through `RoleManager`. The existing `DbSeeder.AdminRole` constant gains a
`ClientRole` sibling.

## Authorization map

| Area | Admin | Client | Enforced by |
|------|-------|--------|-------------|
| Dashboard, `/Dashboard/Data`, `/Dashboard/Display` | ✅ | ✅ | global fallback policy (authenticated) |
| Machines / Products — Index, Details, `Data` | ✅ | ✅ (read) | global fallback policy |
| Machines / Products — Create, Edit, Deactivate | ✅ | ❌ | `[Authorize(Roles = "Admin")]` on the action |
| Work Orders — all actions incl. inputs and transitions | ✅ | ✅ | global fallback policy |
| `/Users/*` | ✅ | ❌ | `[Authorize(Roles = "Admin")]` on the controller |
| `/Account/ChangePassword` | ✅ | ✅ | global fallback policy |

Direct address entry by a client user returns the access-denied path, not the page (FR-011, SC-005).
Views hide controls the current user cannot use, via `User.IsInRole("Admin")` — a client never sees a
button that would fail.

## Routes and screens

| Route | Method | Screen |
|-------|--------|--------|
| `/Users` | GET | list — email, full name, role, active state, created; row actions Edit / Reset password / Deactivate-Reactivate |
| `/Users/Create` | GET, POST | create account — email, full name, role, initial password |
| `/Users/Edit/{id}` | GET, POST | edit full name, role, active state (email is read-only) |
| `/Users/ResetPassword/{id}` | GET, POST | set a new password for another account |
| `/Users/ToggleActive/{id}` | POST | deactivate / reactivate |
| `/Account/ChangePassword` | GET, POST | change your own password (current + new + confirm) |

All POSTs carry `[ValidateAntiForgeryToken]`. All screens are bilingual and follow the existing
design system, empty states, and RTL rules (FR-017).

Navigation: a **Users** item appears in the navbar for administrators only, alongside the existing
four tabs. **Change password** sits in the existing account dropdown for everyone.

## Service

```csharp
// Services/UserAdminService.cs
public interface IUserAdminService
{
    Task<List<UserListViewModel>> GetAllAsync();
    Task<UserFormViewModel?> GetForEditAsync(string id);
    Task<ServiceResult<ApplicationUser>> CreateAsync(UserFormViewModel model);
    Task<ServiceResult> UpdateAsync(UserFormViewModel model);
    Task<ServiceResult> SetActiveAsync(string id, bool isActive);
    Task<ServiceResult> ResetPasswordAsync(string id, string newPassword);
}
```

Returns the existing `ServiceResult` type, so controllers keep their current success/error and
`TempData` pattern. The service wraps `UserManager`/`RoleManager` directly — no repository layer
(Principle III).

## Rules

| Rule | Behaviour | Requirement |
|------|-----------|-------------|
| Duplicate email | rejected with a field-level message; no account created | FR-012 |
| Password policy | Identity's configured policy; the rule is shown as help text beside every password field | FR-015 |
| Last active administrator | cannot be deactivated, deleted, or changed to `Client`; refused with an explanatory message | FR-013 |
| Deactivation | sets `IsActive = false` **and** rotates the Identity security stamp | FR-014 |
| Existing session of a deactivated user | rejected within 1 minute by `SecurityStampValidatorOptions.ValidationInterval` | FR-014 |
| Sign-in by an inactive account | refused with a message distinct from "wrong password" | FR-014 |
| Self-deactivation | refused — an administrator may not deactivate their own account | edge case |
| Password reset by an admin | previous password stops working immediately | FR-008 |

## Admin credential change

`AdminSeed:Password` loses its hardcoded `"Admin@123"` fallback (FR-006):

- **Production**: if `AdminSeed:Password` is not configured and no administrator exists, startup fails
  with a message naming the missing configuration key. It never seeds a weak default.
- **Development**: falls back to a development-only value so `dotnet run` still works on a fresh clone.
- An existing administrator's password is never overwritten by seeding; it is changed through
  `/Account/ChangePassword` or `/Users/ResetPassword`.

The five client accounts are created by the administrator through `/Users/Create` after deployment
(FR-016) — they are not baked into the seeder, so the client can add and remove accounts without a
release.

## Tests

`tests/BetaPlatform.Tests/UserAdminServiceTests.cs`:

- create succeeds and assigns the requested role
- duplicate email is refused and creates nothing
- deactivating the last active administrator is refused
- demoting the last active administrator to `Client` is refused
- deactivating a client account succeeds and sets `IsActive = false`
- reactivation restores sign-in eligibility
- password reset succeeds and invalidates the old password
