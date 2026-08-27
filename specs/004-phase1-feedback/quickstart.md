# Quickstart & Validation: Phase 1 Client Feedback

**Feature**: `004-phase1-feedback` | **Date**: 2026-08-23

How to run the feature and prove all eight client comments are addressed. Scenarios map 1:1 to the
comments in *BETA Platform Comments (Phase 1)*.

## Prerequisites

- .NET 9 SDK, MySQL 8.0 running with the `beta_platform` database from 001–003 migrations applied
- Two browsers (or one plus a private window) to hold an admin session and a client session at once
- Write access to `oee_data` **for testing only** — telemetry is normally written by the IoT team

## Setup

```powershell
# From the repository root
dotnet build BetaPlatform.sln

# Set the administrator password — there is no default any more (FR-006)
dotnet user-secrets --project src/BetaPlatform set "AdminSeed:Password" "<a-strong-password>"

dotnet run --project src/BetaPlatform
```

No `dotnet ef database update` is needed — **this feature adds no migration**. Startup seeds the
`Client` role.

Configuration added by this feature (`appsettings.json`):

```jsonc
"Telemetry": { "StaleAfterMinutes": 5 }   // FR-005; tune per site
```

If startup fails in Production with a message about `AdminSeed:Password`, that is FR-006 working —
configure the key and restart.

## Test data helper

Several scenarios need telemetry with a controlled timestamp and status. Against the test database:

```sql
-- machine 1 reporting "stopped" right now
INSERT INTO oee_data (machine_id, timestamp, availability, performance, quality,
                      total_weight, total_count, total_goods, status, created_at)
VALUES (1, NOW(), 100, 100, 95, 1250, 5000, 4870, 0, NOW());
```

Change `status` to `1` for running, and `NOW()` to `NOW() - INTERVAL 10 MINUTE` to test staleness.

---

## Scenario 1 — Machine status is the same everywhere *(comment 2 — US1)*

1. Insert a reading for machine 1 with `status = 0` at `NOW()`.
2. Open, in four places: `/Dashboard`, `/Machines` (List View), `/Machines` (Card View),
   `/Machines/Details/1`.
3. **Expect** *Stopped* in all four.
4. Insert a reading with `status = 1`. Wait ~5 s, refresh the list pages.
5. **Expect** *Running* in all four.
6. Insert a reading with `status = 1` timestamped `NOW() - INTERVAL 10 MINUTE`.
7. **Expect** *Stopped* in all four — a stale reading is not reported as live (FR-005).
8. Pick a machine with no `oee_data` rows at all. **Expect** *Stopped* everywhere, never *Running*.
9. Start a work order on that machine (status *In Progress*). **Expect** *Running* in all four even
   with no telemetry — an open job overrides the sensor (FR-001).
10. Finish the order. **Expect** the machine to fall back to what telemetry says.
11. Deactivate a machine. **Expect** it marked inactive and visually distinct from running state.

**Pass**: zero contradictions across the four screens in any of the states (SC-001).

## Scenario 2 — Accounts and access management *(comment 1 — US2)*

1. Sign in as the administrator. Open the account menu → **Change password**. Change it.
2. Sign out; sign in with the new password. **Expect** success. The old password fails.
3. Go to `/Users` → **Create**. Create five accounts with the `Client` role.
4. In a second browser, sign in as a client account. **Expect** the dashboard.
5. **Expect** no *Users* item in the client's navbar. Enter `/Users` directly.
   **Expect** access denied, not the page (SC-005).
6. As the client, open `/Machines`. **Expect** no *Add Machine*, *Edit*, or *Deactivate* controls.
   Enter `/Machines/Create` directly. **Expect** access denied.
7. As the client, open `/WorkOrders`. **Expect** full access — create an order, add an input weight,
   start it. All succeed (FR-010a).
8. As the administrator, deactivate that client account. In the client's browser, click any link.
   **Expect** access lost within a minute; sign-in refused with a clear message (FR-014).
9. Reactivate, then **Reset password**. **Expect** the client signs in with the new password only.
10. Try to deactivate the last remaining administrator. **Expect** refusal with an explanation (FR-013).
11. Try to create an account with an email that already exists. **Expect** refusal, no duplicate.

**Pass**: no default credential remains, five client accounts work, and every administrator-only
route is refused to clients (SC-003, SC-004, SC-005).

## Scenario 3 — Machine details updates itself *(comment 4 — US3)*

1. Open `/Machines/Details/1` and scroll halfway down. **Do not touch the page.**
2. Insert a new reading with different values.
3. **Expect** within ~5 s: status, OEE, power, production, quality rate, the three bars, uptime,
   downtime, and energy all change on their own.
4. **Expect** the page did not scroll, jump, or flicker, and your scroll position is unchanged (FR-019).
5. Stop the application. **Expect** the last values stay on screen — no error dialog, no blanking.
6. Restart it. **Expect** updates resume on their own (FR-020).

**Pass**: SC-002.

## Scenario 4 — Current work order on the machine *(comment 5 — US4)*

1. Create a work order, assign it to machine 1, and start it.
2. Open `/Machines/Details/1`. **Expect** a current work order card with order number, output product,
   planned quantity, and elapsed running time.
3. Click it. **Expect** `/WorkOrders/Details/{id}`.
4. Start a second order on the same machine. **Expect** the most recently started shown as current,
   with a notice that another is in progress (FR-024).
5. Finish both orders while the details page is open. **Expect** the card is replaced by a
   "no work order in progress" empty state on the next refresh, without reloading (FR-023).

**Pass**: SC-006.

## Scenario 5 — Input Weight replaces Good Units *(comment 7 — US5)*

1. With an in-progress order on machine 1, add input weights of 500 and 900 through
   `/WorkOrders/Details/{id}`.
2. Open `/Dashboard`. **Expect** machine 1's card shows **Input Weight 1400**, and **Good Units is
   gone** from the card.
3. Add another input of 100. **Expect** the card reads 1500 within ~5 s, with no page reload (FR-030).
4. Finish the order. **Expect** Input Weight falls back to 0, not blank and not an error (FR-029).
5. Switch the language to Arabic. **Expect** the label correctly translated (FR-031).

## Scenario 6 — Uptime and downtime window *(comment 3 — US3)*

1. Open `/Machines/Details/1`. **Expect** the labels state the period in words — "Uptime (last 24 h)"
   — not a bare "24hrs" (FR-025).
2. Insert readings covering the last 24 hours with a deliberate 3-hour gap in the middle.
3. **Expect** uptime + downtime + no-data equals 24 h, and the 3-hour gap appears as **no data**, not
   as uptime (FR-027).
4. With a machine that has no readings at all, **expect** all three read zero, with no error.

**Pass**: SC-010 — confirm the rolling-window reading with the client (research D2 is provisional).

## Scenario 7 — Production-floor display *(comment 6 — US6)*

1. As an administrator, create a display account (`Client` role).
2. On the display machine, sign in with **remember me** ticked, open `/Dashboard/Display`, press F11.
3. **Expect** every active machine visible at once, no scrollbar, no navbar, no buttons (FR-033, FR-036).
4. Step back across the room. **Expect** names, statuses, and key figures legible (SC-008).
5. Insert new telemetry. **Expect** the display updates on its own (FR-035).
6. With more than 6 active machines, **expect** automatic rotation every ~15 s with a page indicator.
7. Stop the application for two minutes. **Expect** the last-good screen stays up — no error page, no
   login form on the wall. Restart. **Expect** it recovers by itself (FR-039).
8. Leave it running for a full shift. **Expect** it still shows current data, untouched (SC-007).

## Scenario 8 — EdgeView spelling *(comment 8 — US7)*

1. Visit the footer of the dashboard, machines, products, work orders, and sign-in screens, in both
   English and Arabic.
2. **Expect** the product name reads **EdgeView** as one word everywhere, including browser tab
   titles (SC-009).

```powershell
# Nothing should match except this spec folder
Select-String -Path src/BetaPlatform/**/*.* -Pattern "Edge View"
```

## Cross-cutting checks

- **Both languages, both themes**: walk every new and changed screen in EN and AR, light and dark.
  Check RTL layout on `/Users`, `/Account/ChangePassword`, and the current work order card.
- **Accessibility**: keyboard-navigate the user administration forms; confirm focus outlines, label
  associations, and contrast in both themes (SC-011).
- **No dashboard regression**: `/Dashboard` still refreshes every ~5 s with no flicker, no layout
  shift, and no slower response than before (SC-012).
- **Query count**: with 20+ machines, confirm one dashboard refresh no longer issues two queries per
  machine (research T1).

## Automated tests

```powershell
dotnet test BetaPlatform.sln
```

New coverage: `MachineStatusRulesTests` (status and staleness boundaries), `UserAdminServiceTests`
(duplicate email, last-admin guard, deactivation, password reset), plus additions to
`MachineServiceTests` (current work order selection, uptime/downtime/no-data accounting) and
`DashboardServiceTests` (status consistency, input weight). All existing tests must stay green.
