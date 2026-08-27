# Quickstart & Validation Guide: Beta Platform — Phase 1

**Feature**: `001-phase-1-core` | **Date**: 2026-07-07

This guide proves Phase 1 works end-to-end. It maps each user story to a runnable validation.
Implementation details (entity code, migrations, service bodies) belong in `tasks.md` and the
implementation phase — this is a run/validate guide. See [data-model.md](./data-model.md),
[contracts/](./contracts/), and [research.md](./research.md) for specifics.

## Prerequisites

- **.NET 9 SDK** installed (`dotnet --version` → 9.x).
- **MySQL 8.0** reachable; a `beta_platform` database (or configured name) and a connection string
  in `appsettings.json` → `ConnectionStrings:DefaultConnection`.
- EF Core tools: `dotnet tool install --global dotnet-ef` (if not present).

## Setup

```powershell
# from the project root
dotnet restore
dotnet ef database update      # applies migrations: creates all tables + seeds machine_types + admin user
dotnet run                     # starts Kestrel; note the https URL
```

Migrations create every table (constitution: EF Core migrations only), seed the two Phase 1
`machine_types` (Forming Machine / Flat Washer Line) and a default admin user. Open the URL and sign
in with the seeded admin credentials.

## Validation scenarios

Each scenario is independently testable (matches the spec's per-story Independent Test).

### US1 — Machine Management (P1)

1. Sign in → open **Machines** from the sidebar.
2. Create machines of both Phase 1 types (Forming Machine, Flat Washer Line). Confirm the type
   dropdown offers **only** those two.
3. Confirm each appears in **both list and card views** (FR-015).
4. Edit a machine's name/type → changes persist (FR-010).
5. Deactivate a machine → it disappears from active selections but its record remains (FR-016).
6. Try to create a machine with an existing `machine_code` → rejected with a clear message (FR-012).

**Expected**: create→visible < 1 min; 0 duplicate codes accepted (SC-001).

### US2 — Product Management (P2)

1. Open **Products** → create a product with a unique code, name, optional English name, category,
   unit.
2. List, then **search/filter** to find it (FR-023).
3. Edit details → persist (FR-020).
4. Deactivate a product → hidden from new selections, existing references intact (FR-024).
5. Submit a duplicate `product_code` → rejected (FR-022).

**Expected**: uniqueness enforced; bilingual name shown per selected language (FR-062).

### US3 — Work Order Management & Workflow (P3)

1. With machines + products present, open **Work Orders** → create an order: unique number, **one**
   input product, one output product, an assigned machine, planned start, quantity. Saved as
   **Ready** (FR-031, scenario 1).
2. **Start** it → status **In Progress**, `started_at` recorded (FR-035, scenario 2).
3. **Hold** → **On Hold**; **Resume** → In Progress (FR-034, scenario 3).
4. Record outputs (each unique code + weight) → order shows total count + total weight (FR-036,
   scenario 5).
5. **Finish** → status **Finished**, `finished_at` recorded (scenario 4).
6. Attempt an invalid transition (e.g. Finish a Ready order) → prevented with explanation (FR-034,
   scenario 6).
7. Create a second order with a duplicate number → rejected (FR-037, scenario 7).
8. **Single-active-machine**: with one order In Progress/On Hold on a machine, try to **Start**
   another order on the *same* machine → rejected with a clear message; **Finish** the first, then
   Start the second → succeeds (FR-039, scenario 8).

**Expected**: full lifecycle Ready→InProgress→OnHold→InProgress→Finished with correct timestamps;
100% of invalid transitions rejected (SC-003); create product + full order < 3 min (SC-002).

### US4 — Monitoring Dashboard (P4)

*Seed representative telemetry to exercise this (the IoT team writes these tables in production; for
validation, insert sample rows into `oee_data` / `power_data` for existing `machine_id`s per the
[telemetry contract](./contracts/telemetry-db-contract.md)).*

1. Open **Dashboard**. Each active machine shows a card with a **status tag** (Running/Stopped),
   **live OEE %**, and **live power (kW)** (FR-040, scenario 1).
2. A card shows OEE components **Availability / Performance / Quality** and **units produced
   (total vs. good)** from the latest OEE row (FR-041, scenario 2).
3. Insert a newer `oee_data` / `power_data` row → within one refresh cycle (~5 s poll) the card
   updates (FR-043, scenario 3; SC-004).
4. A machine with **no** telemetry shows an **unknown/empty** state, not an error (FR-044,
   scenario 4; SC-005).

**Expected**: dashboard reflects newest telemetry within one poll; renders without error when up to
100% of machines lack telemetry (SC-005).

### Cross-cutting — Navigation, auth, bilingual/RTL

- Sidebar shows exactly **Dashboard, Machines, Products, Work Orders** (FR-060); a first-time user
  finds each on the first attempt (SC-007).
- Accessing any page while signed out → redirected to login (FR-001). Sign out works (FR-003).
- Switch language EN⇄AR via the switcher → labels translate, layout flips to **RTL** in Arabic,
  product names show per language (FR-062).

## Service unit tests (constitution quality gate)

Run business-logic tests without the web host:

```powershell
dotnet test
```

Cover: status-transition validity (all valid + representative invalid), single-active-machine rule,
machine/product/work-order uniqueness, OEE computation `(A×P×Q)/10000`, latest-telemetry selection,
and graceful handling of missing telemetry.

## Done / acceptance

Phase 1 is validated when every scenario above passes and the measurable outcomes SC-001…SC-007 in
[spec.md](./spec.md) are met.
