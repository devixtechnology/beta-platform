# Implementation Plan: Phase 1 Client Feedback

**Branch**: `004-phase1-feedback` | **Date**: 2026-08-23 | **Spec**: [spec.md](./spec.md)

**Input**: Feature specification from `/specs/004-phase1-feedback/spec.md`

## Summary

Eight client comments on the deployed Phase 1 platform, delivered as one feature. The engineering
core is a single idea: **machine running state must have one source of truth** — the latest
`oee_data` row for that machine, aged out to *Stopped* after 5 minutes — resolved once and consumed
by the dashboard, the machines list and card view, the machine details page, and a new
production-floor display. Around that sit four smaller pieces: user administration on top of the
Identity tables that already exist (no schema change), self-refresh on the machine details page
following the dashboard's existing polling pattern, an *Input Weight* figure on the machine card
sourced from the `work_order_inputs` rows added in 003, and a resource-string rename to *EdgeView*.

**No EF migration is required.** `AspNetUsers.IsActive`, `work_order_inputs`, and `oee_data.status`
all already exist; the `Client` role is seeded through `RoleManager`, which is data, not schema.

## Technical Context

**Language/Version**: C# 13 / .NET 9

**Primary Dependencies**: ASP.NET Core MVC 9, EF Core 9 + Pomelo MySQL provider, ASP.NET Core
Identity, Bootstrap 5, Bootstrap Icons, Chart.js, jQuery validation. No new packages.

**Storage**: MySQL 8.0 — existing schema, unchanged. Reads from the compatibility-locked, read-only
`oee_data` / `power_data` telemetry tables; writes only through existing application tables.

**Testing**: xUnit (`tests/BetaPlatform.Tests`) with EF Core InMemory via `TestDb`. Services are
tested without the web host, per the constitution.

**Target Platform**: Linux/Windows-hosted ASP.NET Core server; browsers on desktop, tablet, and a
wall-mounted landscape display (≈1920×1080 or larger).

**Project Type**: Single monolithic ASP.NET Core MVC web application.

**Performance Goals**: Dashboard and display refresh every 5 s; machine details refreshes every 5 s.
Each refresh must be served with a bounded number of queries — the current per-machine N+1 pattern in
`DashboardService` is replaced by one latest-per-machine query, because the display view multiplies
that cost across every machine at once.

**Constraints**: No SignalR (polling only, per 001). Telemetry stays read-only — nothing here writes
`oee_data` or `power_data`. Refreshes must not flicker, shift layout, or lose scroll position.
Bilingual AR/EN with RTL, light/dark themes, WCAG 2.1 AA — on every screen added or changed.

**Scale/Scope**: Tens of machines, single site, ~10 concurrent users plus one unattended display.
Roughly 6 changed screens, 4 new screens, 1 new controller, 2 new services, 3 new JS files.

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

| Principle | Assessment | Verdict |
|-----------|------------|---------|
| I. Monolithic Architecture | Everything lands in the existing single MVC project. No new process, no network hop. The display view is another Razor view served by the same app. | PASS |
| II. SOLID | New behaviour is added as two focused services (`IUserAdminService`, `IMachineStatusService`) plus one pure rule class, all registered in the built-in DI container and depended on through their interfaces. Existing services are extended, not rewritten. | PASS |
| III. Simplicity First (YAGNI) | Status resolution is one pure function, not a strategy hierarchy. User administration wraps `UserManager`/`RoleManager` directly rather than adding a repository. Live refresh reuses the dashboard's fetch-and-render pattern rather than introducing a client framework. No permission engine — two roles and `[Authorize(Roles = "Admin")]`. | PASS |
| IV. EF Core Migrations for Schema (NON-NEGOTIABLE) | No schema change at all, so no migration. Role and user seeding go through `RoleManager`/`UserManager`, which are EF-backed APIs — no raw SQL DDL or DML anywhere. All queries are LINQ. | PASS |
| V. MVC Separation of Concerns | Status resolution, uptime accounting, current-work-order lookup, and user administration rules all live in services. Controllers map, delegate, and return. Views render what the view model already decided; the `IsRunning`-vs-telemetry branching currently sitting in `Machines/Index.cshtml` moves out of the view. | PASS |

**Gate result**: PASS — no violations, Complexity Tracking table omitted.

**Re-check after Phase 1 design**: PASS. The design added no abstraction beyond the two services and
one rule class named above; `MachineStatusRules` is a static pure function rather than an injected
strategy precisely because there is only one rule. Contracts introduce two new read-only JSON
endpoints that reuse the existing polling contract shape rather than defining a second one.

## Project Structure

### Documentation (this feature)

```text
specs/004-phase1-feedback/
├── plan.md              # This file
├── research.md          # Phase 0 output — the four resolved decisions
├── data-model.md        # Phase 1 output — no schema change; read models and roles
├── quickstart.md        # Phase 1 output — validation guide
├── contracts/           # Phase 1 output
│   ├── machine-status.md
│   ├── machine-live-data.md
│   ├── user-management.md
│   └── production-display.md
└── tasks.md             # Phase 2 output (/speckit-tasks — NOT created here)
```

### Source Code (repository root)

```text
src/BetaPlatform/
├── Controllers/
│   ├── AccountController.cs           # NEW — change own password
│   ├── UsersController.cs             # NEW — admin-only user administration
│   ├── DashboardController.cs         # + Display() action for the production screen
│   ├── MachinesController.cs          # + Data(id) JSON action; admin-only write actions
│   ├── ProductsController.cs          # write actions become admin-only
│   └── WorkOrdersController.cs        # unchanged permissions (clients may run production)
├── Services/
│   ├── MachineStatusRules.cs          # NEW — pure Resolve(latest, now, staleAfter) rule
│   ├── MachineStatusService.cs        # NEW — latest-per-machine lookup, one query
│   ├── UserAdminService.cs            # NEW — create/edit/deactivate/reset + last-admin guard
│   ├── MachineService.cs              # details view model gains status, current WO, uptime rework
│   └── DashboardService.cs            # N+1 removed; status + input weight per card
├── ViewModels/
│   ├── Account/ChangePasswordViewModel.cs          # NEW
│   ├── Users/UserListViewModel.cs                  # NEW
│   ├── Users/UserFormViewModel.cs                  # NEW
│   ├── Users/ResetPasswordViewModel.cs             # NEW
│   ├── Machines/MachineDetailsViewModel.cs         # + RunningState, CurrentWorkOrder, NoDataTime
│   ├── Machines/MachineLiveDto.cs                  # NEW — payload for the details poll
│   └── Dashboard/DashboardModels.cs                # + InputWeight, RunningState on the card DTO
├── Data/
│   ├── DbSeeder.cs                    # Client role, no default admin password in Production
│   └── Entities/MachineRunningState.cs             # NEW — Running / Stopped / Unknown
├── Views/
│   ├── Users/{Index,Create,Edit,ResetPassword}.cshtml   # NEW
│   ├── Account/ChangePassword.cshtml                    # NEW
│   ├── Dashboard/Display.cshtml                         # NEW — production floor screen
│   ├── Shared/_DisplayLayout.cshtml                     # NEW — chromeless layout
│   ├── Shared/_Layout.cshtml            # Users nav item (admin only), account menu
│   ├── Dashboard/_MachineCard.cshtml    # Input Weight replaces Good Units
│   ├── Machines/{Index,Details}.cshtml  # shared status, live refresh, current WO card
│   └── {Machines,Products}/*.cshtml     # write controls hidden from client users
├── wwwroot/js/
│   ├── machine-details.js             # NEW — 5 s poll, in-place DOM updates
│   ├── display.js                     # NEW — big-screen poll + page rotation
│   └── dashboard.js                   # card renderer gains Input Weight
├── wwwroot/css/site.css               # display-screen styles, status/empty-state tweaks
├── Resources/SharedResource.{en,ar}.resx   # EdgeView rename + all new labels
└── appsettings.json                   # Telemetry:StaleAfterMinutes; AdminSeed password removed

tests/BetaPlatform.Tests/
├── MachineStatusRulesTests.cs         # NEW — running/stopped/unknown/stale boundaries
├── UserAdminServiceTests.cs           # NEW — duplicate email, last-admin guard, deactivate
├── MachineServiceTests.cs             # + current work order, uptime/downtime accounting
└── DashboardServiceTests.cs           # + status consistency, input weight
```

**Structure Decision**: The existing single-project layout is kept exactly as it is
(`src/BetaPlatform` + `tests/BetaPlatform.Tests`), with new files placed in the folders their layer
already owns — Controllers → Services → Data, view models under `ViewModels/<Area>`, per-screen
scripts under `wwwroot/js`. No new project, folder convention, or area is introduced.

## Phase 0 — Research

See [research.md](./research.md). It resolves the four open decisions carried over from the spec
(client permissions, uptime window, display authentication, staleness threshold) and settles four
implementation questions: how to fetch latest-telemetry-per-machine in one translatable LINQ query,
how uptime/downtime should account for gaps in telemetry, how an unattended display session survives
a shift, and how a deactivated account loses access on its next request.

## Phase 1 — Design & Contracts

- [data-model.md](./data-model.md) — confirms zero schema change and defines the read models,
  the `MachineRunningState` value, the role model, and the derivation rules.
- [contracts/machine-status.md](./contracts/machine-status.md) — the single status rule every screen
  consumes.
- [contracts/machine-live-data.md](./contracts/machine-live-data.md) — `GET /Machines/Data/{id}`.
- [contracts/user-management.md](./contracts/user-management.md) — user administration screens,
  routes, authorization, and validation rules.
- [contracts/production-display.md](./contracts/production-display.md) — `GET /Dashboard/Display`,
  its layout contract, and rotation behaviour.
- [quickstart.md](./quickstart.md) — how to validate all eight client comments end to end.

## Delivery Order

Sequenced so each slice is independently demonstrable, matching the spec's priorities:

1. **US7 — EdgeView rename** (P3, minutes). Resource strings only; ships first because it is free.
2. **US1 — status consistency** (P1). `MachineStatusRules` + `MachineStatusService`, consumed by
   dashboard, list, card view, and details. Everything downstream depends on it.
3. **US2 — accounts and access management** (P1). Roles, user administration, password change,
   deactivation enforcement, five client accounts.
4. **US3 + US4 — live details and current work order** (P2). One JSON endpoint serves both.
5. **US5 — Input Weight** (P2). Rides on the dashboard query reworked in step 2.
6. **US6 — production display** (P3). Built last, on a status rule already proven correct.

## Risks

| Risk | Mitigation |
|------|------------|
| The four provisional decisions are wrong (client permissions in particular). | Each is isolated: permissions are attributes on actions, the uptime window is one service method, the staleness threshold is configuration. Recorded in research.md with reversal cost; confirm with the client before step 3 ships. |
| Removing the default admin password locks someone out of an existing deployment. | Startup fails loudly with a message naming the configuration key, rather than silently seeding a weak credential. Documented in quickstart.md. |
| `Machine.IsRunning` is still writable on the machine create/edit forms and will drift again. | The field stops driving any status display. Either drop it from the forms or label it clearly as a non-live administrative flag — decided in data-model.md. |
| Details-page polling causes chart flicker. | Charts update via Chart.js `update()` on their existing instances at a slower cadence; KPI tiles update in place by element, never by replacing markup. |
| The display view drifts from the dashboard as both evolve. | Both consume the same `/Dashboard/Data` payload and the same status rule; no second data contract. |
