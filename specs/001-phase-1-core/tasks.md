---
description: "Task list for Beta Platform — Phase 1 implementation"
---

# Tasks: Beta Platform — Phase 1 (Machines, Products, Work Orders & Monitoring Dashboard)

**Input**: Design documents from `/specs/001-phase-1-core/`

**Prerequisites**: [plan.md](./plan.md), [spec.md](./spec.md), [research.md](./research.md),
[data-model.md](./data-model.md), [contracts/](./contracts/), [quickstart.md](./quickstart.md)

**Tests**: Service-layer unit tests ARE included — the constitution requires business logic to be
covered by unit tests that exercise services without the web host, and [research.md](./research.md)
§8 commits to xUnit. Test tasks are scoped to services (not full-stack TDD).

**Organization**: Tasks are grouped by user story (US1 P1 → US4 P4) for independent implementation
and testing.

**Stack** (from plan.md): ASP.NET Core .NET 9 MVC monolith · EF Core 9 + Pomelo MySQL 8.0 · ASP.NET
Core Identity (single admin role) · IStringLocalizer/.resx AR-EN + RTL · Bootstrap 5 (local). All
schema via EF Core migrations only.

**Paths**: `src/BetaPlatform/` (the MVC project), `tests/BetaPlatform.Tests/` (xUnit).

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel (different files, no dependencies on incomplete tasks)
- **[Story]**: US1–US4 (user-story phases only)

---

## Phase 1: Setup (Shared Infrastructure)

**Purpose**: Solution, project, dependencies, and static front-end assets.

- [X] T001 Create the solution and projects: `BetaPlatform.sln`, ASP.NET Core .NET 9 MVC project at `src/BetaPlatform/BetaPlatform.csproj`, and xUnit test project at `tests/BetaPlatform.Tests/BetaPlatform.Tests.csproj` referencing the MVC project
- [X] T002 [P] Add NuGet packages to `src/BetaPlatform/BetaPlatform.csproj`: `Pomelo.EntityFrameworkCore.MySql` 9.0.0, `Microsoft.EntityFrameworkCore.Design` 9.0.0, `Microsoft.AspNetCore.Identity.EntityFrameworkCore` 9.0.0
- [X] T003 [P] Add the local front-end libraries under `src/BetaPlatform/wwwroot/lib/`: Bootstrap 5 (both `bootstrap.min.css` and `bootstrap.rtl.min.css` builds + JS bundle), Bootstrap Icons, jQuery (no CDN, no build step)
- [X] T004 [P] Create `src/BetaPlatform/appsettings.json` with `ConnectionStrings:DefaultConnection` (MySQL 8.0) and `src/BetaPlatform/Helpers/TimeZoneHelper.cs` exposing `GetKsaNow()` (Asia/Riyadh, UTC+3)
- [X] T005 [P] Create `src/BetaPlatform/wwwroot/css/site.css` with Beta's `:root` palette variables + component classes (`.machine-card`, `.status-badge`, `.kpi-card`, `.oee-bar`) + RTL overrides, and add placeholder Beta logo + `favicon.ico` under `src/BetaPlatform/wwwroot/images/`

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: DbContext, Identity/auth, localization, and the sidebar layout shell — required by ALL
user stories.

**⚠️ CRITICAL**: No user story work can begin until this phase is complete.

- [X] T006 Create `src/BetaPlatform/Data/Entities/ApplicationUser.cs` (`IdentityUser` + `FullName`, `CreatedAt`, `IsActive`)
- [X] T007 Create `src/BetaPlatform/Data/ApplicationDbContext.cs` as `IdentityDbContext<ApplicationUser>` (entity `DbSet`s added per story) with `OnModelCreating` scaffold
- [X] T008 Configure `src/BetaPlatform/Program.cs`: DI, `AddDbContext` with `UseMySql(conn, new MySqlServerVersion(new Version(8,0,35)))` + KSA timezone handling, ASP.NET Core Identity, `AddLocalization`, supported cultures `en`+`ar` (default `en`) via `CookieRequestCultureProvider`, `UseRequestLocalization`, MVC routing, and a global `[Authorize]` fallback policy
- [X] T009 [P] Create localization infrastructure: `src/BetaPlatform/Resources/SharedResource.cs` (empty marker class in root namespace `BetaPlatform`) and `SharedResource.en.resx` / `SharedResource.ar.resx` seeded with shell keys (Dashboard, Machines, Products, WorkOrders, Login, Logout, Running, Stopped, etc.)
- [X] T010 [P] Create `src/BetaPlatform/Controllers/CultureController.cs` with `SetLanguage` (writes culture cookie via `CookieRequestCultureProvider.MakeCookieValue`, `LocalRedirect(returnUrl)`)
- [X] T011 Create the shell views: `src/BetaPlatform/Views/Shared/_Layout.cshtml` (left **sidebar** nav with Dashboard/Machines/Products/WorkOrders only, dark theme, `dir`/`bootstrap.rtl.min.css` swap when culture=`ar`, language switcher, user/logout dropdown, `TempData` toast container), plus `Views/_ViewStart.cshtml` and `Views/_ViewImports.cshtml`
- [X] T012 Create `src/BetaPlatform/Controllers/AuthController.cs` (GET/POST `Login` with `Layout=null`, POST `Logout`) + `Views/Auth/Login.cshtml`, and seed a default admin user + single admin role on startup
- [X] T013 Create the initial EF Core migration for the Identity tables in `src/BetaPlatform/Migrations/` and verify `dotnet ef database update` applies cleanly

**Checkpoint**: App runs, admin can sign in/out, sidebar renders, language switch + RTL work.

---

## Phase 3: User Story 1 - Machine Management (Priority: P1) 🎯 MVP

**Goal**: Full CRUD + deactivation for machines across list and card views, with unique code
enforcement and the two Phase 1 machine types.

**Independent Test**: Sign in, create machines of both types, view in list + card, edit one,
deactivate one, and confirm a duplicate code is rejected — no other module present.

- [X] T014 [P] [US1] Create `src/BetaPlatform/Data/Entities/MachineType.cs` (id, name, name_english?, production_line, is_active, created_at) per data-model.md
- [X] T015 [P] [US1] Create `src/BetaPlatform/Data/Entities/Machine.cs` (machine_id PK, machine_name unique, machine_code unique, machine_type_id FK, is_active, is_running, created_at)
- [X] T016 [US1] Add `Machines` + `MachineTypes` `DbSet`s and fluent config in `src/BetaPlatform/Data/ApplicationDbContext.cs` (unique `machine_code`, unique `machine_name`, unique type `name`, `machine_type_id` FK `RESTRICT`)
- [X] T017 [US1] Create EF migration for `machine_types` + `machines` in `src/BetaPlatform/Migrations/` and **seed the two Phase 1 types** (Forming Machine → "Armor Rod & Guy Grip line"; Flat Washer Line → "Flat Washer Line")
- [X] T018 [US1] Implement `src/BetaPlatform/Services/MachineService.cs`: create/edit/deactivate, unique-code enforcement with clear message, list active machines, list active selectable types (FR-010/011/012/013/014/016)
- [X] T019 [P] [US1] Create machine view models in `src/BetaPlatform/ViewModels/Machines/` (list/card item + create/edit form)
- [X] T020 [US1] Implement thin `src/BetaPlatform/Controllers/MachinesController.cs` (`Index` list+card, `Create`, `Edit`, `Deactivate`) delegating to `MachineService`
- [X] T021 [US1] Create views `src/BetaPlatform/Views/Machines/{Index,Create,Edit}.cshtml` (Index shows both list AND card views; type dropdown limited to active seeded types) with localized strings + resx keys
- [X] T022 [P] [US1] Write `tests/BetaPlatform.Tests/Services/MachineServiceTests.cs`: duplicate-code rejected, deactivation preserves record, only active types selectable

**Checkpoint**: US1 fully functional and independently testable (MVP).

---

## Phase 4: User Story 2 - Product Management (Priority: P2)

**Goal**: CRUD + search + deactivation for products with unique code and bilingual names.

**Independent Test**: Create products with code/name/category/unit, list/search/edit/deactivate,
verify code uniqueness enforced.

- [X] T023 [P] [US2] Create `src/BetaPlatform/Data/Entities/Product.cs` (product_id, product_code unique, product_name, product_name_english?, category?, unit, is_active, created_at)
- [X] T024 [US2] Add `Products` `DbSet` + fluent config in `src/BetaPlatform/Data/ApplicationDbContext.cs` (unique `product_code`, index `category`, index `is_active`)
- [X] T025 [US2] Create EF migration for `products` in `src/BetaPlatform/Migrations/`
- [X] T026 [US2] Implement `src/BetaPlatform/Services/ProductService.cs`: create/edit/deactivate, unique-code enforcement, search/filter by name/code/category, deactivation hides from new selections (FR-020/021/022/023/024)
- [X] T027 [P] [US2] Create product view models in `src/BetaPlatform/ViewModels/Products/`
- [X] T028 [US2] Implement thin `src/BetaPlatform/Controllers/ProductsController.cs` (`Index` with search, `Create`, `Edit`, `Deactivate`)
- [X] T029 [US2] Create views `src/BetaPlatform/Views/Products/{Index,Create,Edit}.cshtml` (search box; show `product_name` vs `product_name_english` per current culture) + resx keys
- [X] T030 [P] [US2] Write `tests/BetaPlatform.Tests/Services/ProductServiceTests.cs`: duplicate-code rejected, search/filter, deactivation keeps references

**Checkpoint**: US1 + US2 both work independently.

---

## Phase 5: User Story 3 - Work Order Management & Workflow (Priority: P3)

**Goal**: Create/manage independent work orders and drive the Ready → In Progress ↔ On Hold →
Finished lifecycle with output recording and the single-active-machine rule.

**Independent Test**: Create an order (single input, output, machine, qty); advance Ready → In
Progress, Hold/Resume, record outputs, Finish; confirm transitions, timestamps, and totals; verify
invalid transitions and the single-active-machine block are rejected.

**Note**: Consumes machines (US1) and products (US2) as reference data; those phases should be done
first (matches the spec's P1→P2→P3 ordering).

- [X] T031 [P] [US3] Create `src/BetaPlatform/Data/Entities/WorkOrderStatus.cs` enum (`Ready=1, InProgress=2, OnHold=3, Finished=4`)
- [X] T032 [P] [US3] Create `src/BetaPlatform/Data/Entities/WorkOrder.cs` (number unique, single input+output product FKs, nullable machine FK, planned_start_time, qty, status, started_at?, finished_at?, created_at)
- [X] T033 [P] [US3] Create `src/BetaPlatform/Data/Entities/WorkOrderOutput.cs` (output_id, work_order_id FK, unique_code unique, weight, sequence_number, notes?, created_at)
- [X] T034 [US3] Add `WorkOrders` + `WorkOrderOutputs` `DbSet`s + fluent config in `ApplicationDbContext.cs` (unique `work_order_number`, unique output `unique_code`, product FKs `RESTRICT`, machine FK `SET NULL`, output FK `CASCADE`, status/machine/created indexes)
- [X] T035 [US3] Create EF migration for `work_orders` + `work_order_outputs` in `src/BetaPlatform/Migrations/`
- [X] T036 [US3] Implement `src/BetaPlatform/Services/WorkOrderService.cs`: CRUD + unique number; status **state machine** (Ready→InProgress; InProgress↔OnHold; InProgress→Finished) rejecting invalid transitions with a message; set `started_at`/`finished_at`; **single-active-machine rule** on start (FR-039); output recording + total count/weight aggregates (FR-030–039)
- [X] T037 [P] [US3] Create work-order view models in `src/BetaPlatform/ViewModels/WorkOrders/`
- [X] T038 [US3] Implement thin `src/BetaPlatform/Controllers/WorkOrdersController.cs` (`Index`, `Details`, `Create`, `Edit`, `Start`, `Hold`, `Resume`, `Finish`, `AddOutput`, `Delete`) with `TempData` messages on invalid transitions
- [X] T039 [US3] Create views `src/BetaPlatform/Views/WorkOrders/{Index,Details,Create,Edit}.cshtml` (Details shows outputs, aggregates, and status-appropriate transition buttons) + resx keys
- [X] T040 [P] [US3] Write `tests/BetaPlatform.Tests/Services/WorkOrderServiceTests.cs`: all valid transitions + representative invalid ones rejected, single-active-machine rule, unique number, output aggregation, finish-with-zero-outputs

**Checkpoint**: US1 + US2 + US3 all independently functional.

---

## Phase 6: User Story 4 - Monitoring Dashboard (Priority: P4)

**Goal**: Live dashboard of per-machine status, OEE, and power via ~5 s polling, reading the
compatibility-locked telemetry tables, degrading gracefully when telemetry is missing.

**Independent Test**: With machines registered and sample `oee_data`/`power_data` rows present,
open the dashboard and verify each card shows status, OEE % (+ A/P/Q, units total vs good), and
power (kW), that values refresh, and that machines without telemetry show an unknown state.

**Note**: Consumes machines (US1). Telemetry tables are read-only (FR-042).

- [X] T041 [P] [US4] Create `src/BetaPlatform/Data/Entities/OeeData.cs` matching the reference EXACTLY per [contracts/telemetry-db-contract.md](./contracts/telemetry-db-contract.md) (columns, `[NotMapped] OEE = (A*P*Q)/10000`, `status` byte, nullable `order_id`)
- [X] T042 [P] [US4] Create `src/BetaPlatform/Data/Entities/PowerData.cs` matching the reference EXACTLY (kw_hr, v1–v3/v12/v23/v13, a1–a3/a_avg, frequency — all nullable)
- [X] T043 [US4] Add `OeeData` + `PowerData` `DbSet`s + fluent config in `ApplicationDbContext.cs`: `oee_data.timestamp` `datetime(6)`, `power_data.timestamp` `datetime(3)`; `availability/quality/performance` `decimal(5,2)`, `frequency` `decimal(5,2)`, `kw_hr` `decimal(10,2)`, voltages/currents `decimal(8,2)`; **do NOT set precision** on `total_count`/`total_goods`; indexes `(machine_id,timestamp)`,`(timestamp)`,`(order_id)`; machine FK `CASCADE`
- [X] T044 [US4] Create EF migration for `oee_data` + `power_data` in `src/BetaPlatform/Migrations/` and diff the generated SQL against the telemetry contract to confirm exact compatibility
- [X] T045 [US4] Implement `src/BetaPlatform/Services/DashboardService.cs`: for each active machine, select latest `oee_data` and latest `power_data` (`ORDER BY timestamp DESC`), compute OEE %, derive status, and return an unknown/empty state when telemetry is absent or malformed (FR-040/041/044)
- [X] T046 [US4] Implement `src/BetaPlatform/Controllers/DashboardController.cs`: `Index` (server-rendered cards) + `Data` (JSON matching [contracts/dashboard-data.md](./contracts/dashboard-data.md))
- [X] T047 [P] [US4] Create dashboard view models / DTOs in `src/BetaPlatform/ViewModels/Dashboard/`
- [X] T048 [US4] Create `src/BetaPlatform/Views/Dashboard/Index.cshtml` (machine cards: status tag, OEE %, A/P/Q bars, units total vs good, power kW) + `src/BetaPlatform/wwwroot/js/dashboard.js` polling `/Dashboard/Data` every ~5 s and patching cards (no SignalR)
- [X] T049 [P] [US4] Write `tests/BetaPlatform.Tests/Services/DashboardServiceTests.cs`: OEE math `(A*P*Q)/10000`, latest-row selection per machine, missing/stale telemetry → unknown state

**Checkpoint**: All four user stories independently functional.

---

## Phase 7: Polish & Cross-Cutting Concerns

- [X] T050 [P] Complete AR/EN translations for every UI string across all views in `src/BetaPlatform/Resources/SharedResource.{en,ar}.resx`; verify RTL layout on each page
- [X] T051 [P] Finalize Beta re-skin: confirm palette variables + hardcoded gradient/hex spots and logos/favicon in `src/BetaPlatform/wwwroot/` reflect Beta's brand (swap in real assets when supplied)
- [X] T052 Run the full [quickstart.md](./quickstart.md) validation end-to-end and confirm SC-001…SC-007
- [X] T053 [P] Constitution compliance pass: verify no raw SQL DDL/DML, correct Controller→Service→Data layering, all schema via migrations, no speculative abstractions
- [X] T054 Add a dev-only sample-telemetry seed script under `specs/001-phase-1-core/` (or `scripts/`) to insert `oee_data`/`power_data` rows for dashboard validation (NOT application code; app never writes telemetry)

---

## Dependencies & Execution Order

### Phase dependencies

- **Setup (Phase 1)**: no dependencies.
- **Foundational (Phase 2)**: depends on Setup — **blocks all user stories**.
- **User stories (Phases 3–6)**: all depend on Foundational. Recommended order P1→P2→P3→P4.
- **Polish (Phase 7)**: depends on the desired user stories being complete.

### User-story dependencies

- **US1 (P1)**: after Foundational. No dependency on other stories.
- **US2 (P2)**: after Foundational. Independent of US1.
- **US3 (P3)**: after Foundational. **Uses machines (US1) + products (US2)** as reference data to
  create orders — do US1/US2 first; the work-order code itself is self-contained.
- **US4 (P4)**: after Foundational. **Uses machines (US1)**; reads telemetry tables it creates.
  Independent of US2/US3.

### Within each story

Entities (→ [P]) → DbContext config → migration → service → controller → views; view models [P]
alongside; service tests [P].

### Parallel opportunities

- Setup: T002, T003, T004, T005 in parallel.
- Foundational: T009, T010 in parallel (after T006–T008 scaffolding).
- Within a story, entity files marked [P] and the service-test file [P] can run in parallel.
- With capacity, after Foundational: US1, US2, US4 can proceed in parallel; US3 joins once US1/US2
  entities exist.

---

## Parallel Example: User Story 1

```bash
# Entities in parallel:
Task: "Create MachineType entity in src/BetaPlatform/Data/Entities/MachineType.cs"
Task: "Create Machine entity in src/BetaPlatform/Data/Entities/Machine.cs"
# View models + tests in parallel with later steps:
Task: "Create machine view models in src/BetaPlatform/ViewModels/Machines/"
Task: "Write MachineServiceTests in tests/BetaPlatform.Tests/Services/MachineServiceTests.cs"
```

---

## Implementation Strategy

### MVP first (User Story 1 only)

1. Phase 1 Setup → 2. Phase 2 Foundational (blocks everything) → 3. Phase 3 US1 →
4. **STOP & VALIDATE** US1 independently → demo.

### Incremental delivery

Setup + Foundational → US1 (MVP) → US2 → US3 → US4, validating each independently before the next.
The compatibility-locked telemetry tables (US4) are additive migrations and don't touch earlier
stories.

### Parallel team strategy

After Foundational: Dev A → US1, Dev B → US2, Dev C → US4; US3 starts once US1/US2 entities land.

---

## Notes

- [P] = different files, no incomplete dependencies. [USx] maps each task to its story.
- Each schema change ships as an EF Core migration in the same change set (constitution IV).
- `oee_data`/`power_data` are read-only compatibility contracts — never add an ingestion path.
- Commit after each task or logical group; stop at any checkpoint to validate a story independently.
