# Implementation Plan: Beta Platform — Phase 1 (Machines, Products, Work Orders & Monitoring Dashboard)

**Branch**: `001-phase-1-core` | **Date**: 2026-07-07 | **Spec**: [spec.md](./spec.md)

**Input**: Feature specification from `/specs/001-phase-1-core/spec.md`

## Summary

Phase 1 delivers the foundational Beta Platform: master-data CRUD for **Machines** and **Products**,
the full **Work Order** lifecycle (Ready → In Progress ↔ On Hold → Finished with a
single-active-order-per-machine rule), and a live **Monitoring Dashboard** showing per-machine
status, OEE, and power. It is built as a single **ASP.NET Core .NET 9 MVC** monolith with **EF Core
9 + Pomelo MySQL**, per the project constitution. The `oee_data` and `power_data` tables are
reproduced **byte-for-byte** from the reference SPackEdgeView project so the IoT team's existing
writers work unchanged; those tables are read-only to the app (no ingestion API). The front end
reuses SPackEdgeView's Bootstrap 5 dark shell, `IStringLocalizer`/resx bilingual AR-EN + RTL stack,
and card patterns — re-skinned with Beta's own palette/logo and reflowed into a **sidebar**
navigation. The dashboard refreshes by **~5-second client polling** of a JSON endpoint (no SignalR).

## Technical Context

**Language/Version**: C# on **.NET 9** (ASP.NET Core MVC)

**Primary Dependencies**: EF Core 9, **Pomelo.EntityFrameworkCore.MySql 9.0.0**, ASP.NET Core
Identity, ASP.NET Core Localization (`IStringLocalizer` + `.resx`), Bootstrap 5 + Bootstrap Icons +
jQuery (served locally from `wwwroot/lib`). No SignalR, no Chart.js requirement in Phase 1.

**Storage**: **MySQL 8.0** (server version `8.0.35`); schema via EF Core migrations only.

**Testing**: xUnit for service-layer unit tests (business rules without the web host); EF Core
in-memory / SQLite-in-memory for `DbContext`-touching service tests.

**Target Platform**: Linux/Windows server (Kestrel); modern browsers; KSA (Asia/Riyadh, UTC+3)
timezone for timestamps.

**Project Type**: Web application — single monolithic ASP.NET Core MVC solution (one deployable).

**Performance Goals**: Phase 1 factory scale (reference targets ~15 machines, 20–50 concurrent
users); dashboard reflects new telemetry within one ~5 s poll cycle (SC-004). No high-throughput
ingestion path in the app (IoT writes directly to DB).

**Constraints**: `oee_data`/`power_data` schema is a hard compatibility contract (FR-050/051);
app is read-only on telemetry (FR-042); bilingual AR/EN + RTL (FR-062); sidebar nav limited to
Dashboard/Machines/Products/Work Orders (FR-060); single admin role (FR-002).

**Scale/Scope**: 4 user stories, ~7 entities, ~5 controllers, ~15 views; two compatibility-locked
telemetry tables; ~5 s polling dashboard.

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-checked after Phase 1 design.*

| Principle | Status | How the design complies |
|-----------|--------|-------------------------|
| I. Monolithic Architecture | ✅ PASS | Single ASP.NET Core .NET 9 MVC project, one deployable. No microservices, brokers, or service processes. Layering via folders (Controllers → Services → Data). |
| II. SOLID | ✅ PASS | Thin controllers delegate to focused services (MachineService, ProductService, WorkOrderService, DashboardService); dependencies via built-in DI; machine-type extensibility via a data-driven lookup table (Open/Closed). |
| III. Simplicity First (YAGNI) | ✅ PASS | Deliberately **drops** reference machinery not needed in Phase 1: SignalR (→ polling), permission/roles system (→ single-role `[Authorize]`), work-order chaining/ERP sync, alerts, financial, OEE/power analytics, export. No speculative abstractions. |
| IV. EF Core Migrations for All Schema | ✅ PASS | Every table (incl. the compat-locked telemetry tables and machine-type/admin seeds) created via EF Core migrations; MySQL via Pomelo; no hand-written DDL or raw SQL. |
| V. MVC Separation of Concerns | ✅ PASS | Controllers coordinate only; business rules (status transitions, single-active-machine, uniqueness, OEE math, latest-telemetry selection) live in services; persistence behind EF Core; views logic-free. |

**Result**: PASS (initial and post-design). **No violations → Complexity Tracking is empty/N/A.**

One design choice worth noting for review: machine type is a **lookup table** rather than a C# enum
(as the reference uses). This is not speculative — FR-013 explicitly requires an *extensible* list
where each type carries a *production-line grouping label*, which an enum cannot express. It is the
simplest design that satisfies the stated requirement, so it is consistent with Principle III.

## Project Structure

### Documentation (this feature)

```text
specs/001-phase-1-core/
├── plan.md              # This file
├── research.md          # Phase 0 output — decisions & rationale
├── data-model.md        # Phase 1 output — entities, tables, rules
├── quickstart.md        # Phase 1 output — validation guide
├── contracts/           # Phase 1 output
│   ├── mvc-routes.md            # Controller action surface
│   ├── telemetry-db-contract.md # IoT writer DB contract (oee_data/power_data)
│   └── dashboard-data.md        # /Dashboard/Data polling JSON schema
├── checklists/
│   └── requirements.md  # Spec quality checklist (pre-existing)
└── tasks.md             # Phase 2 output (/speckit-tasks — NOT created here)
```

### Source Code (repository root)

Single monolithic ASP.NET Core MVC project (constitution Principle I). Layout mirrors the reference
project's proven structure, trimmed to Phase 1 scope:

```text
src/BetaPlatform/                    # the single MVC project (csproj)
├── Program.cs                       # DI, EF Core (Pomelo MySQL), Identity, localization, routing
├── appsettings.json                 # ConnectionStrings:DefaultConnection (MySQL)
├── Controllers/
│   ├── AuthController.cs
│   ├── DashboardController.cs        # Index (view) + Data (polling JSON)
│   ├── MachinesController.cs
│   ├── ProductsController.cs
│   ├── WorkOrdersController.cs
│   └── CultureController.cs          # SetLanguage
├── Data/
│   ├── ApplicationDbContext.cs       # IdentityDbContext<ApplicationUser>
│   └── Entities/
│       ├── MachineType.cs  Machine.cs  Product.cs
│       ├── WorkOrder.cs  WorkOrderOutput.cs  WorkOrderStatus.cs
│       ├── OeeData.cs  PowerData.cs           # compat-locked, read-only
│       └── ApplicationUser.cs
├── Services/                         # business logic (thin controllers delegate here)
│   ├── MachineService.cs  ProductService.cs
│   ├── WorkOrderService.cs           # transitions + single-active-machine rule
│   └── DashboardService.cs           # latest-telemetry read model + OEE math
├── ViewModels/                       # per-feature view models
├── Migrations/                       # EF Core migrations (all schema + seeds)
├── Resources/
│   └── SharedResource.{en,ar}.resx   # bilingual strings; SharedResource.cs marker (root ns)
├── Helpers/
│   └── TimeZoneHelper.cs             # KSA now for created_at defaults
├── Views/
│   ├── Shared/_Layout.cshtml         # sidebar shell, dark theme, RTL swap, language switcher
│   ├── Auth/Login.cshtml
│   ├── Dashboard/Index.cshtml
│   ├── Machines/{Index,Create,Edit}.cshtml
│   ├── Products/{Index,Create,Edit}.cshtml
│   └── WorkOrders/{Index,Details,Create,Edit}.cshtml
└── wwwroot/
    ├── css/site.css                  # Beta :root palette + component classes + RTL overrides
    ├── js/dashboard.js               # ~5 s poll of /Dashboard/Data, patch cards
    ├── images/                       # Beta logo(s) + favicon (re-skin)
    └── lib/                          # bootstrap (LTR+RTL), bootstrap-icons, jquery (local)

tests/BetaPlatform.Tests/            # xUnit service-layer unit tests
└── Services/
    ├── WorkOrderServiceTests.cs      # transitions, single-active-machine, uniqueness
    ├── DashboardServiceTests.cs      # OEE math, latest-telemetry, missing-telemetry
    └── Machine/ProductServiceTests.cs
```

**Structure Decision**: A single monolithic ASP.NET Core MVC project (`src/BetaPlatform`) plus a
sibling test project (`tests/BetaPlatform.Tests`), reflecting the constitution's monolith mandate
and mirroring the reference SPackEdgeView layout (Controllers / Data+Entities / Services / Views /
wwwroot / Resources / Migrations). Reference-only concerns out of Phase 1 scope (Hubs, permission
Modules, ERP, Alerts, Financial) are omitted.

## Complexity Tracking

No constitution violations — this section is intentionally empty.

## Phase outputs

- **Phase 0** → [research.md](./research.md) — all unknowns resolved; no `NEEDS CLARIFICATION`
  remain.
- **Phase 1** → [data-model.md](./data-model.md), [contracts/](./contracts/),
  [quickstart.md](./quickstart.md), and the agent context update (CLAUDE.md).
- **Phase 2** → `tasks.md` is generated by `/speckit-tasks` (not by this command).
