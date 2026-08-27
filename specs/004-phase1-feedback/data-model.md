# Data Model: Phase 1 Client Feedback

**Feature**: `004-phase1-feedback` | **Date**: 2026-08-23

## Schema changes: none

**No EF Core migration is created by this feature.** Every field it needs is already in the database:

| Need | Already exists as | Added by |
|------|-------------------|----------|
| Per-user active flag | `AspNetUsers.IsActive` (`ApplicationUser.IsActive`) | 001 initial migration |
| Per-user display name | `AspNetUsers.FullName` | 001 initial migration |
| Roles and role membership | `AspNetRoles`, `AspNetUserRoles` | ASP.NET Core Identity |
| Machine running telemetry | `oee_data.status` (0 = stopped, 1 = running), `oee_data.timestamp` | reference schema (read-only) |
| Raw material input weight | `work_order_inputs.weight` | 003 change request |
| Work order ↔ machine link | `work_orders.machine_id`, `work_orders.status`, `work_orders.started_at` | 001 / 003 |

The `Client` role and the five client accounts are **data**, created at runtime through
`RoleManager<IdentityRole>` and `UserManager<ApplicationUser>`. These are EF-backed APIs, so
Principle IV (no raw SQL DDL/DML) holds without a migration.

`oee_data` and `power_data` remain read-only. Nothing in this feature writes them.

---

## Derived value: MachineRunningState

A new enum in `Data/Entities/MachineRunningState.cs`. It is **derived, never persisted** — there is no
column behind it.

```
Running | Stopped | Unknown
```

**Derivation** (the single rule, implemented once in `MachineStatusRules.Resolve`):

| Condition | Result |
|---|---|
| The machine has a work order with status `InProgress` | `Running` — checked first |
| No `oee_data` row exists | `Stopped` |
| `timestamp` older than `Telemetry:StaleAfterMinutes` (default 5) before now | `Stopped` |
| `status == 1` | `Running` |
| `status == 0` | `Stopped` |
| any other `status` value | `Unknown` |

Time is compared in the platform's existing KSA-local basis via `TimeZoneHelper.GetKsaNow()`, matching
how every other timestamp in the application is handled.

### Relationship to `Machine.IsRunning`

`machines.is_running` is an administrator-set flag, not telemetry. It is the source of the defect in
comment 2: the machines list reads it while the dashboard reads telemetry.

**Decision**: `IsRunning` stops driving any status display anywhere. It is removed from the machine
create and edit forms and from the `[Bind]` lists in `MachinesController`, so nothing new is written
to it. The column stays in the database — dropping it would require a migration for no functional
gain, and Principle III argues against schema churn to delete a now-inert field. Existing rows keep
whatever value they hold; no screen reads it.

`Machine.IsActive` is unaffected and keeps its current meaning: whether the machine is administratively
in service. It is displayed distinctly from running state (FR-004).

---

## Read models

All are view models under `ViewModels/`, populated by services. None are entities.

### `MachineDashboardDto` (extended)

Existing DTO in `ViewModels/Dashboard/DashboardModels.cs`, serialized to the dashboard and display
polling clients.

| Field | Change | Notes |
|-------|--------|-------|
| `Status` | now `MachineRunningState`, serialized as the same `"Running"` / `"Stopped"` / `"Unknown"` strings | wire format unchanged, so `dashboard.js` keeps working |
| `InputWeight` | **new**, decimal | total `work_order_inputs.weight` for the machine's current in-progress work order; `0` when there is none |
| `Oee.TotalGoods` | retained in the payload, removed from the card | still used by the details page's quality rate |

### `MachineDetailsViewModel` (extended)

| Field | Change |
|-------|--------|
| `RunningState` | **new** — replaces the view's inline `LatestOee?.Status == 1` check |
| `CurrentWorkOrder` | **new**, nullable `CurrentWorkOrderDto` |
| `HasOtherWorkOrdersInProgress` | **new**, bool — drives the "others exist" notice (FR-024) |
| `NoDataTime24h` | **new**, `TimeSpan` — the third bucket from research T2 |
| `Uptime24h` / `Downtime24h` | unchanged shape, recalculated by duration rather than sample count |
| `WindowStart` / `WindowEnd` | **new** — so the view can state the period in words (FR-025) |

### `CurrentWorkOrderDto` (new)

Projected from the in-progress work order with the latest `started_at` for the machine.

| Field | Source |
|-------|--------|
| `WorkOrderId`, `WorkOrderNumber` | `work_orders` |
| `OutputProductName` | `work_orders.output_product_id` → `products.name` |
| `QtyToManufacture` | `work_orders.qty_to_manufacture` |
| `StartedAt`, `ElapsedTime` | `work_orders.started_at`, computed against now |
| `TotalInputWeight` | sum of `work_order_inputs.weight` (the existing `WorkOrder.TotalInputWeight`) |

**Selection rule**: among work orders with `machine_id = X` and `status = InProgress`, the one with the
latest `started_at`. Ties break on the highest `work_order_id`. If more than one exists,
`HasOtherWorkOrdersInProgress` is true.

### `MachineLiveDto` (new)

The JSON payload for the machine details poll — see
[contracts/machine-live-data.md](./contracts/machine-live-data.md).

### User administration view models (new)

| View model | Fields | Validation |
|-----------|--------|-----------|
| `UserListViewModel` | id, email, full name, role, is active, created at | — |
| `UserFormViewModel` | email, full name, role, is active, password (create only) | email required + well-formed + unique; full name required; role must be `Admin` or `Client`; password meets the Identity policy |
| `ResetPasswordViewModel` | user id, new password, confirm | both required, must match, meet policy |
| `ChangePasswordViewModel` | current, new, confirm | current must verify; new must differ and meet policy |

---

## Roles

| Role | Seeded | May do |
|------|--------|--------|
| `Admin` | already exists (001) | everything, including user administration and master data |
| `Client` | **new**, seeded by `DbSeeder` | view all operational screens; create/edit work orders, record and remove input weights, change work order status |

Enforced by `[Authorize(Roles = "Admin")]` on administrator-only actions, on top of the existing global
`FallbackPolicy` requiring an authenticated user. Views hide controls the current user cannot use, so
a client user never sees a button that would fail.

### Account lifecycle

```
Created (active) ──deactivate──> Inactive ──reactivate──> Active
       │                              │
   can sign in                 sign-in refused; any existing
                               session rejected within 1 minute
```

Deactivation rotates the user's Identity security stamp, which invalidates issued cookies (research
T4). The last remaining active `Admin` account cannot be deactivated, deleted, or demoted to `Client`
(FR-013).

---

## Validation rules

| Rule | Where enforced | Requirement |
|------|----------------|-------------|
| Email unique across accounts | Identity `RequireUniqueEmail` + explicit pre-check for a friendly message | FR-012 |
| Password meets the configured policy, and the policy is shown to the user | Identity options + form help text | FR-015 |
| Cannot remove/deactivate/demote the last active administrator | `UserAdminService`, checked inside the same request | FR-013 |
| Inactive account cannot sign in | `AuthController` sign-in path | FR-014 |
| Input weight is non-negative | existing work order input validation, unchanged | — |
| Status shown identically on every screen | one rule class, no per-view branching | FR-001, FR-002 |
