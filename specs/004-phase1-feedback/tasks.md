---
description: "Task list for 004-phase1-feedback"
---

# Tasks: Phase 1 Client Feedback

**Input**: Design documents from `/specs/004-phase1-feedback/`

**Prerequisites**: [plan.md](./plan.md), [spec.md](./spec.md), [research.md](./research.md),
[data-model.md](./data-model.md), [contracts/](./contracts/)

**Tests**: Included. The constitution requires business logic to be covered by unit tests that run
without the web host, and the contracts name the specific test cases. Test tasks cover services and
rules only — views and scripts are validated through [quickstart.md](./quickstart.md).

**Organization**: Grouped by user story so each can be implemented, tested, and demoed on its own.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel (different files, no dependencies on incomplete tasks)
- **[Story]**: Which user story the task belongs to (US1–US7)
- Exact file paths are given in every task

## Path Conventions

Single monolithic ASP.NET Core MVC project: `src/BetaPlatform/`, tests in
`tests/BetaPlatform.Tests/`. Paths below are repository-relative.

## Phase ordering note

Phases follow the **delivery order in plan.md**, not strict priority order. US7 (P3) ships first
because it is a resource-string rename that costs minutes and blocks nothing. Everything else follows
priority. US3, US4, and US6 depend on US1's status rule and are sequenced after it.

**No EF Core migration is created by this feature** — see [data-model.md](./data-model.md). If a task
seems to require one, stop: the field already exists.

---

## Phase 1: Setup

**Purpose**: Configuration and a known-good baseline before anything changes.

- [X] T001 Add the `Telemetry` section with `StaleAfterMinutes: 5` to `src/BetaPlatform/appsettings.json` (per research D4)
- [X] T002 Record the baseline: run `dotnet build BetaPlatform.sln` and `dotnet test BetaPlatform.sln`, confirm the build is clean and all 32 existing tests pass before making changes

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: The single status rule that US1, US3, and US6 all consume. Nothing that displays machine
status can be built correctly until this exists.

**⚠️ CRITICAL**: US1, US3, and US6 cannot begin until this phase is complete. US2, US5, and US7 are
independent of it.

- [X] T003 [P] Create the `MachineRunningState` enum (`Running`, `Stopped`, `Unknown`) in `src/BetaPlatform/Data/Entities/MachineRunningState.cs`
- [X] T004 [P] Create `TelemetryOptions` with `StaleAfterMinutes` (default 5) in `src/BetaPlatform/Services/TelemetryOptions.cs`
- [X] T005 Implement the pure rule `MachineStatusRules.Resolve(OeeData? latest, DateTime now, TimeSpan staleAfter)` in `src/BetaPlatform/Services/MachineStatusRules.cs` per [contracts/machine-status.md](./contracts/machine-status.md) — in-progress order → Running (wins outright), else null → Stopped, stale → Stopped, status 1 → Running, status 0 → Stopped, unrecognised byte → Unknown (amended 2026-08-27) (depends on T003)
- [X] T006 [P] Write `tests/BetaPlatform.Tests/MachineStatusRulesTests.cs` covering all six cases in the contract, including the exact-threshold boundary (not stale) and a future-dated reading (not stale) (depends on T005)
- [X] T007 Implement `IMachineStatusService` / `MachineStatusService` with `GetStatesAsync` and `GetLatestOeeAsync` in `src/BetaPlatform/Services/MachineStatusService.cs`, using the single group-by-max-timestamp-then-join LINQ query from research T1 — one round trip regardless of machine count, machines without telemetry absent from the dictionary, empty input returns empty without querying (depends on T003, T005)
- [X] T008 Register `IMachineStatusService` as scoped and bind `TelemetryOptions` to the `Telemetry` configuration section in `src/BetaPlatform/Program.cs` (depends on T004, T007)

**Checkpoint**: One status rule exists, is tested, and is injectable. Status-consuming stories can begin.

---

## Phase 3: User Story 7 - EdgeView spelling (Priority: P3) 🚀 Ships first

**Goal**: The product name reads as one word everywhere (client comment 8).

**Independent Test**: Visit every screen in both languages; the footer, sign-in screen, and browser tab
titles all read "EdgeView", and a repository search for "Edge View" returns nothing outside `specs/`.

- [X] T009 [P] [US7] Change `AppName` and `AppTagline` to the one-word form in `src/BetaPlatform/Resources/SharedResource.en.resx`
- [X] T010 [P] [US7] Apply the matching Arabic wording for `AppName` and `AppTagline` in `src/BetaPlatform/Resources/SharedResource.ar.resx`
- [X] T011 [US7] Search `src/BetaPlatform/` for any remaining hardcoded "Edge View" or "Beta Platform" user-facing text in `Views/**`, `wwwroot/**`, and `Resources/**`, and replace it with the localized `AppName` — leave the `BetaPlatform` namespace, assembly name, and `beta_theme` cookie alone, they are not user-facing (depends on T009, T010)

**Checkpoint**: Client comment 8 is closed and demoable on its own.

---

## Phase 4: User Story 1 - Consistent machine status (Priority: P1) 🎯 MVP

**Goal**: Every screen derives running state from the same rule, ending the dashboard-vs-machines-page
contradiction (client comment 2).

**Independent Test**: Insert a `status = 0` reading for a machine; `/Dashboard`, `/Machines` list view,
`/Machines` card view, and `/Machines/Details/{id}` all read *Stopped*. Flip to `status = 1`; all four
read *Running*. Age the reading past the threshold; all four read *Stopped*.

### Tests for User Story 1

- [X] T012 [P] [US1] Add tests to `tests/BetaPlatform.Tests/DashboardServiceTests.cs`: a machine's dashboard DTO status matches `MachineStatusRules` for running, stopped, stale, and no-telemetry cases
- [X] T013 [P] [US1] Add tests to `tests/BetaPlatform.Tests/MachineServiceTests.cs`: the same machine resolves to the same state through the list projection and the details view model (FR-002)

### Implementation for User Story 1

- [X] T014 [P] [US1] Create `MachineListItemViewModel` (the `Machine` plus its `RunningState`) in `src/BetaPlatform/ViewModels/Machines/MachineListItemViewModel.cs`
- [X] T015 [US1] Add `GetAllWithStatusAsync()` to `IMachineService` / `MachineService` in `src/BetaPlatform/Services/MachineService.cs`, projecting each machine with its resolved state via `IMachineStatusService` (depends on T007, T014)
- [X] T016 [US1] Add `RunningState` to `MachineDetailsViewModel` in `src/BetaPlatform/ViewModels/Machines/MachineDetailsViewModel.cs` and populate it in `MachineService.GetDetailsAsync` (depends on T007)
- [X] T017 [US1] Change `MachinesController.Index` to call `GetAllWithStatusAsync()` in `src/BetaPlatform/Controllers/MachinesController.cs` (depends on T015)
- [X] T018 [US1] Rewrite the status badges in `src/BetaPlatform/Views/Machines/Index.cshtml` — both the list view (lines ~54-64) and the card view (lines ~91-110) — to render `RunningState` instead of `Machine.IsRunning`, keeping the inactive marker visually distinct from running state (FR-004) (depends on T017)
- [X] T019 [US1] Replace the inline `LatestOee?.Status == 1` check at the top of `src/BetaPlatform/Views/Machines/Details.cshtml` with `Model.RunningState` (depends on T016)
- [X] T020 [US1] Change `MachineDashboardDto.Status` to `MachineRunningState` in `src/BetaPlatform/ViewModels/Dashboard/DashboardModels.cs`, serialized as the existing `"Running"` / `"Stopped"` / `"Unknown"` strings so `dashboard.js` needs no change (depends on T003)
- [X] T021 [US1] Rework `DashboardService.GetAsync` in `src/BetaPlatform/Services/DashboardService.cs` to resolve status through `IMachineStatusService` and to fetch latest OEE and latest power with one query each for all machines, removing the two-queries-per-machine loop (research T1) (depends on T007, T020)
- [X] T022 [P] [US1] Remove `IsRunning` from the machine create and edit forms in `src/BetaPlatform/Views/Machines/Create.cshtml` and `src/BetaPlatform/Views/Machines/Edit.cshtml`, and from the `[Bind]` lists in `src/BetaPlatform/Controllers/MachinesController.cs`, so nothing writes the now-inert flag (per data-model.md)
- [X] T023 [US1] Verify the `Running`, `Stopped`, and `Unknown` resource keys exist in both `src/BetaPlatform/Resources/SharedResource.{en,ar}.resx` and add any that are missing

**Checkpoint**: Client comment 2 is closed. Status is consistent across all four screens — the MVP.

---

## Phase 5: User Story 2 - Accounts and access management (Priority: P1)

**Goal**: Named accounts, a real administrator password, and role-based access (client comment 1).

**Independent Test**: Change the administrator password and sign back in with it; create five client
accounts; confirm a client reaches the dashboard, is refused `/Users` and `/Machines/Create` by direct
address, and can still create work orders; deactivate one and watch it lose access.

### Tests for User Story 2

- [X] T024 [P] [US2] Write `tests/BetaPlatform.Tests/UserAdminServiceTests.cs` covering the seven cases in [contracts/user-management.md](./contracts/user-management.md): create assigns the role, duplicate email refused, last active admin cannot be deactivated, last active admin cannot be demoted, client deactivation sets `IsActive = false`, reactivation restores eligibility, password reset invalidates the old password

### Implementation for User Story 2

- [X] T025 [P] [US2] Add a `ClientRole` constant alongside `AdminRole` and seed the `Client` role via `RoleManager` in `src/BetaPlatform/Data/DbSeeder.cs`
- [X] T026 [US2] Remove the `"Admin@123"` fallback from `src/BetaPlatform/Data/DbSeeder.cs`: in Production, fail startup with a message naming `AdminSeed:Password` when it is unset and no administrator exists; in Development keep a development-only fallback. Also remove the password from `src/BetaPlatform/appsettings.json` (FR-006) (depends on T025)
- [X] T027 [P] [US2] Create `UserListViewModel`, `UserFormViewModel`, and `ResetPasswordViewModel` under `src/BetaPlatform/ViewModels/Users/` with the validation attributes in data-model.md
- [X] T028 [P] [US2] Create `ChangePasswordViewModel` in `src/BetaPlatform/ViewModels/Account/ChangePasswordViewModel.cs`
- [X] T029 [US2] Implement `IUserAdminService` / `UserAdminService` in `src/BetaPlatform/Services/UserAdminService.cs` wrapping `UserManager`/`RoleManager` and returning the existing `ServiceResult` type — including the last-active-administrator guard and security-stamp rotation on deactivation (research T4) (depends on T025, T027)
- [X] T030 [US2] Register `IUserAdminService` as scoped, set `SecurityStampValidatorOptions.ValidationInterval` to 1 minute, and state `SlidingExpiration = true` explicitly in `ConfigureApplicationCookie` in `src/BetaPlatform/Program.cs` (depends on T029)
- [X] T031 [US2] Create `UsersController` with `[Authorize(Roles = "Admin")]` at the controller level and Index / Create / Edit / ResetPassword / ToggleActive actions in `src/BetaPlatform/Controllers/UsersController.cs`, thin and delegating to `IUserAdminService` (depends on T029)
- [X] T032 [US2] Create `AccountController` with GET/POST `ChangePassword` in `src/BetaPlatform/Controllers/AccountController.cs` (depends on T028)
- [X] T033 [US2] Build `Index`, `Create`, `Edit`, and `ResetPassword` views under `src/BetaPlatform/Views/Users/`, following the existing design system, empty states, and RTL rules, with the password policy shown as help text beside every password field (FR-015) (depends on T031)
- [X] T034 [US2] Build `src/BetaPlatform/Views/Account/ChangePassword.cshtml` in the same style (depends on T032)
- [X] T035 [US2] Refuse sign-in for accounts with `IsActive = false` in `src/BetaPlatform/Controllers/AuthController.cs`, with a message distinct from "wrong password" (FR-014)
- [X] T036 [US2] Add an `AccessDenied` action and view to `src/BetaPlatform/Controllers/AuthController.cs` and `src/BetaPlatform/Views/Auth/`, and point `AccessDeniedPath` at it in `src/BetaPlatform/Program.cs` instead of the login page, so a refused client sees a refusal rather than a login form
- [X] T037 [US2] Add `[Authorize(Roles = "Admin")]` to the Create, Edit, and Deactivate actions of `src/BetaPlatform/Controllers/MachinesController.cs` and `src/BetaPlatform/Controllers/ProductsController.cs`, leaving `WorkOrdersController` open to both roles per research D1 (depends on T025)
- [X] T038 [US2] Hide the add/edit/deactivate controls from client users in `src/BetaPlatform/Views/Machines/Index.cshtml`, `src/BetaPlatform/Views/Machines/Details.cshtml`, and `src/BetaPlatform/Views/Products/Index.cshtml` using `User.IsInRole("Admin")`, so no button is shown that would fail (depends on T037)
- [X] T039 [US2] Add an admin-only **Users** navbar item and a **Change password** entry in the account dropdown in `src/BetaPlatform/Views/Shared/_Layout.cshtml` (depends on T031, T032)
- [X] T040 [US2] Add every new user-administration label to `src/BetaPlatform/Resources/SharedResource.en.resx` and `SharedResource.ar.resx` (depends on T033, T034)

**Checkpoint**: Client comment 1 is closed. The default credential is gone and roles are enforced.

---

## Phase 6: User Story 3 - Machine details updates itself (Priority: P2)

**Goal**: The details page refreshes on its own, and the 24-hour figures say what period they cover
(client comments 4 and 3).

**Independent Test**: Open a details page, scroll halfway, insert new telemetry, and watch the values
change within ~5 s with no scroll jump and no manual refresh. Stop the app and confirm the last values
stay on screen.

**Depends on**: Phase 2 (status rule), Phase 4 (`RunningState` on the details view model).

### Tests for User Story 3

- [X] T041 [P] [US3] Add uptime/downtime tests to `tests/BetaPlatform.Tests/MachineServiceTests.cs`: uptime + downtime + no-data equals the full window; a telemetry gap lands in no-data rather than uptime (FR-027); no telemetry at all yields three zeros

### Implementation for User Story 3

- [X] T042 [US3] Replace the sample-ratio uptime calculation in `src/BetaPlatform/Services/MachineService.cs` with the duration-weighted walk from research T2 — each reading contributes the time until the next reading, capped at the staleness threshold — and add `NoDataTime24h`, `WindowStart`, and `WindowEnd` to `src/BetaPlatform/ViewModels/Machines/MachineDetailsViewModel.cs` (depends on T004)
- [X] T043 [P] [US3] Create `MachineLiveDto` matching the payload in [contracts/machine-live-data.md](./contracts/machine-live-data.md) in `src/BetaPlatform/ViewModels/Machines/MachineLiveDto.cs`
- [X] T044 [US3] Add `GetLiveAsync(int id)` to `IMachineService` / `MachineService` in `src/BetaPlatform/Services/MachineService.cs`, returning `MachineLiveDto` with nulls for absent telemetry rather than failing (depends on T042, T043)
- [X] T045 [US3] Add the `Data(int id)` JSON action to `src/BetaPlatform/Controllers/MachinesController.cs`, returning 404 for an unknown machine (depends on T044)
- [X] T046 [US3] Add stable element ids to the KPI tiles, component bars, uptime/downtime/energy figures, and status badge in `src/BetaPlatform/Views/Machines/Details.cshtml`, and change the labels to state the period in words — "Uptime (last 24 h)" — with a "no data" figure rendered only when non-zero (FR-025) (depends on T042)
- [X] T047 [US3] Write `src/BetaPlatform/wwwroot/js/machine-details.js`: poll `/Machines/Data/{id}` every 5000 ms, update elements in place by `textContent` and class swaps (never `innerHTML` on a container), keep the last-good view on failure, and update the three Chart.js instances via `.update()` at a 60 s cadence (depends on T045, T046)
- [X] T048 [US3] Reference `machine-details.js` from the scripts section of `src/BetaPlatform/Views/Machines/Details.cshtml` and pass the machine id and localized labels to it the way `Dashboard/Index.cshtml` passes `window.betaLabels` (depends on T047)
- [X] T049 [P] [US3] Add the uptime/downtime/no-data period labels to `src/BetaPlatform/Resources/SharedResource.en.resx` and `SharedResource.ar.resx`

**Checkpoint**: Client comments 3 and 4 are closed.

---

## Phase 7: User Story 4 - Current work order on the machine (Priority: P2)

**Goal**: The machine details page shows what that machine is producing right now (client comment 5).

**Independent Test**: Start a work order on a machine, open its details page, and see the order with
number, product, planned quantity, and elapsed time, linking to the work order. Finish it and watch
the card become an empty state without reloading.

**Depends on**: Phase 6 (the live payload and the polling script it extends).

### Tests for User Story 4

- [X] T050 [P] [US4] Add current-work-order tests to `tests/BetaPlatform.Tests/MachineServiceTests.cs`: selection by latest `StartedAt`, tie broken by highest id, `HasOtherWorkOrdersInProgress` true with two in-progress orders, null with none

### Implementation for User Story 4

- [X] T051 [P] [US4] Create `CurrentWorkOrderDto` with the fields listed in data-model.md in `src/BetaPlatform/ViewModels/Machines/CurrentWorkOrderDto.cs`
- [X] T052 [US4] Populate `CurrentWorkOrder` and `HasOtherWorkOrdersInProgress` in `MachineService.GetDetailsAsync` and `GetLiveAsync` in `src/BetaPlatform/Services/MachineService.cs`, and add both to `MachineDetailsViewModel` and `MachineLiveDto` (depends on T043, T051)
- [X] T053 [US4] Add the current work order card to `src/BetaPlatform/Views/Machines/Details.cshtml` — order number, output product, planned quantity, elapsed time, a link to `/WorkOrders/Details/{id}`, the "others in progress" notice, and the `_EmptyState` partial when there is none (depends on T052)
- [X] T054 [US4] Extend `src/BetaPlatform/wwwroot/js/machine-details.js` to render, update, and clear the current work order card from each poll (depends on T047, T053)
- [X] T055 [P] [US4] Add the current-work-order labels and empty-state text to `src/BetaPlatform/Resources/SharedResource.en.resx` and `SharedResource.ar.resx`

**Checkpoint**: Client comment 5 is closed.

---

## Phase 8: User Story 5 - Input Weight replaces Good Units (Priority: P2)

**Goal**: The machine card shows raw material input weight instead of good units (client comment 7).

**Independent Test**: Record two input weights on an in-progress order; the machine's dashboard card
shows their total as *Input Weight* and no longer shows *Good Units*. Add a third and watch it update
within ~5 s. Finish the order and see it fall back to zero.

**Depends on**: Phase 4 (the reworked `DashboardService` query this extends).

### Tests for User Story 5

- [X] T056 [P] [US5] Add input-weight tests to `tests/BetaPlatform.Tests/DashboardServiceTests.cs`: the sum of the in-progress order's inputs appears on the card; zero when the order has no inputs; zero when the machine has no in-progress order

### Implementation for User Story 5

- [X] T057 [US5] Add `InputWeight` to `MachineDashboardDto` in `src/BetaPlatform/ViewModels/Dashboard/DashboardModels.cs` (depends on T020)
- [X] T058 [US5] Populate `InputWeight` in `DashboardService.GetAsync` in `src/BetaPlatform/Services/DashboardService.cs` with one query over in-progress work orders summing `work_order_inputs.weight`, choosing the most recently started order per machine and defaulting to zero (depends on T021, T057)
- [X] T059 [US5] Replace the `TotalGoods` stat with `InputWeight` in `src/BetaPlatform/Views/Dashboard/_MachineCard.cshtml`, leaving `TotalGoods` in the payload for the details page's quality rate (depends on T057)
- [X] T060 [US5] Update the `cardHtml` renderer in `src/BetaPlatform/wwwroot/js/dashboard.js` to match, so the polled card and the server-rendered card stay identical (depends on T059)
- [X] T061 [P] [US5] Add the `InputWeight` label to `src/BetaPlatform/Resources/SharedResource.en.resx` and `SharedResource.ar.resx`

**Checkpoint**: Client comment 7 is closed.

---

## Phase 9: User Story 6 - Production-floor big screen (Priority: P3)

**Goal**: A chromeless display for a wall-mounted screen in the production area (client comment 6).

**Independent Test**: Open `/Dashboard/Display` full-screen on a large monitor, leave it untouched for
a shift, and confirm it stays current, legible from across the floor, and never shows navigation, a
scrollbar, or a dialog.

**Depends on**: Phases 2, 4, and 8 — the display must not put a contradictory status or a stale metric
on a wall.

- [X] T062 [P] [US6] Create the chromeless `src/BetaPlatform/Views/Shared/_DisplayLayout.cshtml` — no navbar, footer, language switcher, or theme toggle; dark theme fixed; `overflow: hidden`; same `site.css` design tokens
- [X] T063 [US6] Add the `Display()` action to `src/BetaPlatform/Controllers/DashboardController.cs`, reusing `IDashboardService.GetAsync()` for the first render (depends on T021)
- [X] T064 [US6] Build `src/BetaPlatform/Views/Dashboard/Display.cshtml` on `_DisplayLayout`: a 3 × 2 tile grid showing name, code, status badge, OEE gauge, the three component bars, power, total weight, total count, and input weight, plus a page indicator and a small last-updated stamp (depends on T062, T063)
- [X] T065 [US6] Add the display styles to `src/BetaPlatform/wwwroot/css/site.css` at the sizes in [contracts/production-display.md](./contracts/production-display.md), scaling with the viewport so the same view works on a 43″ and an 86″ panel, with status carried by text and position as well as colour (depends on T064)
- [X] T066 [US6] Write `src/BetaPlatform/wwwroot/js/display.js`: poll `/Dashboard/Data` every 5000 ms, paginate into 6-tile pages, rotate every 15 s (paused mid-refresh), recalculate pages when the machine list changes, treat a 302 to login as a failed poll and keep the last-good render (depends on T064)
- [X] T067 [US6] Add the display empty states to `src/BetaPlatform/Views/Dashboard/Display.cshtml` and `display.js`: full-screen message with no active machines, `Unknown` tiles with dashes when nothing is reporting, single centred tile with one machine and no rotation (depends on T064, T066)
- [X] T068 [P] [US6] Add the display page-indicator and empty-state labels to `src/BetaPlatform/Resources/SharedResource.en.resx` and `SharedResource.ar.resx`

**Checkpoint**: Client comment 6 is closed. All eight comments are addressed.

---

## Phase 10: Polish & Cross-Cutting Concerns

- [X] T069 [P] Walk every new and changed screen in English and Arabic, light and dark, checking RTL layout on `/Users`, `/Account/ChangePassword`, the current work order card, and `/Dashboard/Display`
  - Verified against the running app: every screen returns 200 in both cultures, `dir="rtl"` and `bootstrap.rtl.min.css` are applied under `ar`, and every screen renders Arabic text (no key fell back to its English-looking name). Light/dark is carried by the existing `data-bs-theme` tokens; the display is fixed dark by contract. **A visual pass by eye is still worth doing.**
- [X] T070 [P] Verify WCAG 2.1 AA on the new screens: keyboard navigation and focus outlines on the user administration forms, label associations, and contrast in both themes (SC-011)
  - Markup verified: every form control on `/Users/*` and `/Account/ChangePassword` has an associated `<label>`, required fields carry `aria-required`, password fields point at the policy text via `aria-describedby`, and icon-only row actions carry `visually-hidden` labels. Status on the display is carried by badge text and position as well as colour. **Contrast measurement and a keyboard walkthrough still need a human.**
- [X] T071 Confirm no dashboard regression — `/Dashboard` still refreshes every ~5 s with no flicker, no layout shift, and no slower response than the T002 baseline (SC-012)
  - `/Dashboard/Data` measured at min 10 ms / avg 12 ms / max 20 ms over five calls. The polling cadence, the payload shape and `dashboard.js`'s render path are unchanged apart from the one stat swap, so flicker and layout shift are unchanged by construction.
- [X] T072 With 20+ machines in the database, confirm one dashboard refresh no longer issues two queries per machine (research T1)
  - Measured against the running app with MySQL logging: **one refresh is 5 SQL statements, constant** — machines, latest `oee_data`, latest `power_data`, in-progress work orders + input weights, finished-order count. The old shape was `2N + 2` (42 statements at 20 machines). The scale claim is pinned by `DashboardQueryShapeTests` at 1 and 25 machines, so no test rows were written to the development database.
- [X] T073 Run `dotnet test BetaPlatform.sln` — all existing tests plus the new `MachineStatusRulesTests`, `UserAdminServiceTests`, and the `MachineServiceTests` / `DashboardServiceTests` additions must pass
  - **77 passed, 0 failed** (baseline before this feature: 32).
- [ ] T074 Work through all eight scenarios in [quickstart.md](./quickstart.md) end to end
  - **Partially done, needs a person to finish.** Automated and verified: the status rule and its staleness boundary (Scenario 1), uptime/downtime/no-data accounting including a telemetry gap (Scenario 6), current work order selection and its empty state (Scenario 4), input weight totals and fallbacks (Scenario 5), the user administration rules including the last-admin guard (Scenario 2), `GET /Machines/Data/{id}` returning the contract payload and 404 for an unknown machine (Scenario 3), and zero remaining two-word brand occurrences (Scenario 8). Still requires a browser and a person: in-place refresh with no scroll jump, the display's 15 s rotation and legibility from across the floor, an unattended shift, and the two-browser client-session checks. These need controlled `oee_data` INSERTs into a development database — see the quickstart's test-data helper.
- [ ] T075 Deployment step: set `AdminSeed:Password`, sign in, change the administrator password, and create the five client accounts through `/Users/Create` (FR-016)
  - **Deployment action, not a code change.** The code side is done: `appsettings.json` no longer carries a password, and startup outside Development throws a message naming `AdminSeed:Password` when no administrator exists.
  - **The five client accounts are now delivered by code**, not by hand: migration `20260903192810_SeedClientAccounts` (data only, no schema change) provisions `client1@beta.local` … `client5@beta.local` in the `Client` role, active, on every environment the app starts against. Only the Identity password hashes are committed — the distinct plaintexts were generated once and handed over out of band, and every statement is `INSERT IGNORE`, so re-running never overwrites a password that has since been changed. Applied and verified on the development database on 2026-09-03: all five sign in, a wrong password is refused. Rename them through `/Users/Edit` once the client supplies real addresses.
  - **The administrator password is rotated by the same migration** at the client's request: the `admin@beta.local` row gets a new hash and a fresh security stamp, which drops every live cookie session and every issued bearer token on its next request. Applied and verified on the development database on 2026-09-03 — the new password authenticates, `Admin@123` is refused. Two properties to keep in mind: the change is **not reversible** (`Down` cannot restore a hash it never saw), and on any database where this migration has not yet run it **overwrites** whatever administrator password is in place; on a brand-new database it matches no rows, because migrations run before `DbSeeder` creates the administrator, and `AdminSeed:Password` still governs there.
  - The replacement administrator and client passwords exist only as hashes in the migration; the plaintexts were generated once and handed over out of band. Whoever holds them should still change them at `/Account/ChangePassword`.
- [ ] T076 **Client decision — open.** Confirm research decisions **D1** (what the five client accounts may change) and **D2** (rolling 24 h vs. midnight reset — the client's own question in comment 3) with the client, and update the spec and code if either answer differs

---

## Dependencies & Execution Order

### Phase Dependencies

- **Phase 1 (Setup)**: no dependencies
- **Phase 2 (Foundational)**: needs Phase 1 — **blocks US1, US3, US6**
- **Phase 3 (US7)**: needs nothing beyond Phase 1; can ship immediately
- **Phase 4 (US1)**: needs Phase 2
- **Phase 5 (US2)**: needs Phase 1 only — independent of the status work
- **Phase 6 (US3)**: needs Phase 4
- **Phase 7 (US4)**: needs Phase 6
- **Phase 8 (US5)**: needs Phase 4 (extends the reworked dashboard query)
- **Phase 9 (US6)**: needs Phases 4 and 8
- **Phase 10 (Polish)**: needs every story you intend to ship

### Story Dependency Graph

```text
Setup ──> Foundational ──> US1 ──┬──> US3 ──> US4
                                 ├──> US5 ──┐
                                 └──────────┴──> US6
Setup ──> US2        (independent)
Setup ──> US7        (independent)
```

### Within Each Story

Tests → view models → services → controllers → views → scripts → resource strings.
Resource-string tasks are marked [P] because they touch only the `.resx` files.

### Parallel Opportunities

- **T003 + T004** — enum and options class, different files
- **T009 + T010** — English and Arabic resx, different files
- **T012 + T013 + T014** — two test files and a new view model
- **US7, US2, and the US1 chain** can run concurrently on three developers after Phase 1, since US2 and
  US7 touch neither the status rule nor the dashboard query
- All `[P]` resource-string tasks (T049, T055, T061, T068) can be batched into one pass if preferred
- **T069 + T070** — the language/theme sweep and the accessibility sweep are independent

---

## Parallel Example: User Story 1

```bash
# Tests and the new view model, together:
Task: "Add dashboard status tests in tests/BetaPlatform.Tests/DashboardServiceTests.cs"
Task: "Add status consistency tests in tests/BetaPlatform.Tests/MachineServiceTests.cs"
Task: "Create MachineListItemViewModel in src/BetaPlatform/ViewModels/Machines/MachineListItemViewModel.cs"

# Then the service layer (sequential — same file):
Task: "Add GetAllWithStatusAsync to src/BetaPlatform/Services/MachineService.cs"
Task: "Add RunningState to MachineDetailsViewModel and populate it in GetDetailsAsync"
```

---

## Implementation Strategy

### MVP (US7 + US1)

1. Phase 1 (Setup) → Phase 3 (US7) — the rename ships in minutes
2. Phase 2 (Foundational) → Phase 4 (US1)
3. **STOP and VALIDATE**: quickstart Scenarios 1 and 8
4. Demo: the status contradiction the client photographed is gone

### Incremental Delivery

1. Setup + US7 → deploy (comment 8 closed)
2. Foundational + US1 → deploy (comment 2 closed — MVP)
3. US2 → deploy (comment 1 closed; **confirm D1 with the client first**)
4. US3 + US4 → deploy (comments 3, 4, 5 closed)
5. US5 → deploy (comment 7 closed)
6. US6 → deploy (comment 6 closed — all eight)

### Parallel Team Strategy

After Phase 1, three tracks run independently:

- **Developer A**: Phase 2 → US1 → US3 → US4 (the status and live-data spine)
- **Developer B**: US2 (accounts — touches Identity, Program.cs, and its own controllers/views)
- **Developer C**: US7, then US5 once US1 lands, then US6

Contention points: `Program.cs` (T008 and T030), `_Layout.cshtml` (T039), and the `.resx` files —
coordinate or serialize those.

---

## Notes

- **No EF migration in this feature.** Every field already exists (data-model.md). If a task appears to
  need one, re-read the data model before creating it.
- `oee_data` and `power_data` stay read-only. The `INSERT` statements in quickstart.md are test
  fixtures for a development database, not application behaviour.
- No Razor view may branch on `Machine.IsRunning` or `OeeData.Status` after T018–T021.
- Four decisions (research D1–D4) are provisional; T076 closes them.
- Commit after each task or logical group; stop at any checkpoint to validate a story on its own.
