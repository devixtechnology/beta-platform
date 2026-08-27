# Phase 0 Research: Beta Platform — Phase 1

**Feature**: `001-phase-1-core` | **Date**: 2026-07-07

This document resolves the technical unknowns for Phase 1 and records the decisions that
constrain the design. The dominant constraint is **schema compatibility with the reference
SPackEdgeView project** (`E:\Projects\Work\S\web`) for the OEE and power telemetry tables, plus
**front-end reuse** of that project's layout, localization, and theming stack. Both were examined
directly; findings below quote the reference code.

## 1. Platform, framework, and data stack

**Decision**: ASP.NET Core MVC on **.NET 9**, C#, Entity Framework Core 9 with the
**Pomelo.EntityFrameworkCore.MySql 9.0.0** provider against **MySQL 8.0**. Single monolithic
solution/deployable. Schema managed exclusively via EF Core migrations.

**Rationale**: Mandated by the project constitution (Monolithic Architecture, EF Core Migrations,
MySQL provider). It is also exactly the reference project's stack, which is required for the IoT
team's existing writers to work unchanged against the OEE/power tables.

**Alternatives considered**: None viable — the constitution fixes the framework, ORM, migration
approach, and database engine; the reference project fixes the provider and server version.

**Confirmed from reference** (`Program.cs`): `new MySqlServerVersion(new Version(8, 0, 35))`,
`options.UseMySql(connectionString, serverVersion)`; timestamps written in KSA (Asia/Riyadh, UTC+3)
via a timezone interceptor and a `TimeZoneHelper.GetKsaNow()` default on `created_at` columns.

## 2. OEE and power telemetry schema (compatibility contract)

**Decision**: Reproduce the reference `oee_data` and `power_data` tables **byte-for-byte** at the
column level (names, MySQL types, precision/scale, nullability, indexes, and the `machine_id`
foreign key), so IoT writers that currently `INSERT` into SPackEdgeView work unchanged. These two
tables are **read-only to the Beta application** (no ingestion API, no manual entry — FR-042). The
application only defines them (via EF Core migrations) and reads the latest row per machine.

**Rationale**: FR-050 / FR-051 make schema compatibility a hard data contract. Verified column
types directly from the reference migration `20260412213438_InitialCreate.cs` and DbContext fluent
config. Exact shapes are captured in [data-model.md](./data-model.md).

Key facts that must be preserved exactly:

- `oee_data.timestamp` is **`datetime(6)`**; `power_data.timestamp` is **`datetime(3)`**;
  `created_at` is `datetime(6)` in both.
- `oee_data.availability/quality/performance` are `decimal(5,2)`; `total_count/total_goods` are
  MySQL default `decimal(65,30)` (no precision configured in the reference).
- `oee_data.status` is `tinyint unsigned` (`0=Stopped, 1=Running`).
- `oee_data.order_id` is a nullable `int` FK to `work_orders`, written by the IoT system.
- OEE is a computed (`[NotMapped]`) value: **`(Availability × Performance × Quality) / 10000`** —
  the three components are stored as percentages (0–100).
- `power_data` measurement columns are exactly: `kw_hr decimal(10,2)`, `v1/v2/v3/v12/v23/v13/a1/a2/a3/a_avg decimal(8,2)`, `frequency decimal(5,2)` (all nullable). The reference power table has **no** peak-power / power-factor / phase-unbalance columns; FR-051 lists those as *optional forward-compat* only, and to keep the writer contract identical they are **omitted in Phase 1**.
- Both telemetry tables reference the machine by the integer PK `machines.machine_id` (not by
  `machine_code`), with `ON DELETE CASCADE`.

**Alternatives considered**:
- *Adding forward-compat power columns now (peak power, power factor, unbalance)* — rejected:
  they are not in the reference writer contract and are not displayed in Phase 1 (YAGNI). They can
  be added by a later migration without breaking writers.
- *A separate ingestion/staging API* — rejected: explicitly out of scope (FR-042); IoT writes
  directly to the DB.

## 3. Machine-type modeling

**Decision**: Model machine type as a small **`machine_types` lookup table** (not a C# enum),
each row carrying a display name, an optional English name, and a **production-line grouping
label**; seeded via migration with the only two Phase 1 types — **Forming Machine** (grouping:
*Armor Rod & Guy Grip line*) and **Flat Washer Line**. `machines.machine_type_id` is a FK to it.

**Rationale**: FR-013 requires a *flat, extensible* list where each type *carries a production-line
grouping label*, and the seven other Beta types must be addable later "without editing stable code
paths." A C# enum (as the reference uses: `MachineType { Extruding, Printing, Cutting }`) cannot
carry a grouping label and forces a code+migration change per new type. A lookup table satisfies
Open/Closed with the least machinery. This is a concrete present requirement, not speculative
generality, so it is consistent with Simplicity-First.

**Alternatives considered**:
- *Enum like the reference* — rejected: cannot hold the grouping label; not extensible by data.
- *A full production-line entity/hierarchy* — rejected by spec clarification (2026-07-06): "no
  separate production-line hierarchy/entity"; the grouping is just a label on the type.

## 4. Work-order model (independent orders)

**Decision**: A single, self-contained `work_orders` table with exactly **one input product** and
**one output product** per order, an optional assigned machine, planned/actual start & finish
times, quantity, and status. Outputs recorded in `work_order_outputs`. **Drop** all of the
reference's multi-stage chaining machinery (`work_order_inputs`, `work_order_input_products`,
`order_type`, `sync_status`, ERP sync, `hour_rate`, `workstation_capability_per_hour`,
`line_setup_time`, `total_mixed_data`, output `print_status`/consumption links).

**Rationale**: FR-031/FR-032 and the spec assumptions state Beta orders are independent — the
output is a final end product, never consumed by a subsequent order — unlike the reference's
Extruding→Printing→Cutting chain. The reference `WorkOrder` already stores a single
`input_product_id` + `output_product_id`, so the shape is reused and the chaining columns are
removed (YAGNI). ERP sync is a later phase.

**Status lifecycle**: The reference enum is `Ready=1, InProgress=2, Finished=3`. Beta adds
**On Hold** (FR-033). Since `work_orders` is *not* a compatibility-locked table (only `oee_data`
and `power_data` are), Beta defines its own status enum: `Ready=1, InProgress=2, OnHold=3,
Finished=4`. Allowed transitions (FR-034): `Ready→InProgress`, `InProgress↔OnHold`,
`InProgress→Finished`; everything else is rejected with an explanatory message.

**Single-active-order-per-machine rule** (FR-039): Enforced in the work-order service at *start*
time — a machine may have at most one work order in `InProgress` or `OnHold`. Attempting to start
a second is rejected; once the occupier is `Finished`, the machine frees up. This is application
logic, not a DB constraint (a partial-unique index can't express "InProgress OR OnHold" portably
in MySQL), so it lives in the service layer with a clear message.

**Alternatives considered**:
- *Reuse the reference chaining tables* — rejected: out of scope and adds unused complexity.
- *DB-level enforcement of the single-active rule* — rejected: not cleanly expressible in MySQL;
  service-layer check is simpler and testable without the web host.

## 5. Authentication & authorization

**Decision**: **ASP.NET Core Identity** with `ApplicationUser : IdentityUser` (adding `FullName`,
`CreatedAt`, `IsActive`), cookie sign-in, and a **single administrative role**. Every management
and dashboard action is gated by a plain `[Authorize]` attribute — **no granular permission
system**.

**Rationale**: FR-001/FR-002/FR-003 require sign-in, a single admin role, and sign-out only. The
reference's permission cache, `RequirePermission` attributes, roles/permissions tables, and JWT
API stack are all out of scope for Phase 1 (YAGNI); dropping them is a deliberate simplification.

**Alternatives considered**: *Port the reference permission system* — rejected: explicitly
deferred; single role needs none of it.

## 6. Front-end: layout, theming, localization

**Decision**: Reuse the reference's front-end **stack and patterns**, re-skinned for Beta:

- **Shell**: one `_Layout.cshtml` with Bootstrap 5 (served locally from `wwwroot/lib/`, no CDN/npm
  build), Bootstrap Icons, dark theme via `data-bs-theme="dark"` + `site.css`. Content via
  `@RenderBody()`; toast container driven by `TempData`.
- **Navigation**: FR-060 requires a **sidebar** with exactly Dashboard, Machines, Products, Work
  Orders. The reference uses a *top navbar*; Beta reflows the same pattern (localized labels +
  Bootstrap Icons + `[Authorize]`-gated links) into a **left sidebar**. The reference's OEE
  Reports, Power Monitoring, Financial, Alerts, and Administration entries are **omitted** (out of
  Phase 1 scope).
- **Theming / re-skin**: swap the `:root` CSS-variable palette in `wwwroot/css/site.css`
  (`--primary-color`, `--card-bg`, etc.) and the hardcoded gradient/hex spots, replace the logo
  PNGs in `wwwroot/images/`, and change the `<title>` suffix. Beta chooses its own distinct palette
  and logo (exact brand colors TBD by design; a placeholder Beta palette is used until provided).
- **Localization / RTL**: `AddLocalization` + `IStringLocalizer<SharedResource>` +
  `Resources/SharedResource.{en,ar}.resx`, cookie-based culture via `CookieRequestCultureProvider`,
  a `CultureController.SetLanguage` switcher, and RTL handled by loading `bootstrap.rtl.min.css` +
  `dir="rtl"` when culture is `ar`, plus the custom RTL overrides in `site.css`. Product
  Arabic/English names render per the selected culture (FR-062).

**Rationale**: FR-061 mandates reusing the reference layout/interaction patterns with Beta's own
colors/logo/design; FR-062 mandates bilingual AR/EN with RTL. The reference already implements a
complete, proven localization+RTL+theming stack, so Beta mirrors it rather than reinventing it.

**Alternatives considered**:
- *Keep the top navbar* — rejected: FR-060 explicitly says sidebar. The nav *pattern* is reused,
  its placement changes.
- *A JS-framework SPA (React/Angular)* — rejected: violates the monolithic MVC constitution and
  discards the reusable Razor/Bootstrap shell; no present need.

## 7. Dashboard live-update mechanism

**Decision**: **Periodic client polling** — the dashboard renders server-side once, then a small
JS `setInterval` (default **~5 s**, FR-043) `fetch`es a JSON endpoint
(`GET /Dashboard/Data`) returning the latest status/OEE/power per machine and patches the cards in
place. **No SignalR / no real-time server push** in Phase 1.

**Rationale**: FR-043 explicitly specifies polling (~5-second default) and states "Real-time
server push is not required in Phase 1." The reference uses a SignalR `BackgroundService` pushing
every 3 s; Beta deliberately drops SignalR (the hub, the hosted background service, the client
library) as unneeeded machinery (YAGNI). Polling is simpler to build, deploy, and reason about at
Phase 1 scale.

**Alternatives considered**: *Port the reference SignalR dashboard* — rejected: real-time push is
explicitly not required; polling meets SC-004 ("within one refresh cycle").

**Dashboard read model**: For each active machine, read the **latest** `oee_data` row and the
**latest** `power_data` row (by `machine_id`, ordered by `timestamp desc`). Compute OEE =
`(Availability × Performance × Quality) / 10000`. Status tag derives from the latest OEE row's
`status` byte / the machine's `is_running`. Machines with no telemetry render an **unknown/empty**
state rather than erroring (FR-044, SC-005).

## 8. Testing approach

**Decision**: xUnit for **service-layer unit tests** exercising business rules without the web
host (status-transition validity, single-active-machine rule, uniqueness enforcement, OEE
computation, latest-telemetry selection, graceful handling of missing telemetry). EF Core
**in-memory or SQLite in-memory** provider for service tests that touch a `DbContext`.

**Rationale**: The constitution's quality gate says "Business logic SHOULD be covered by unit tests
that exercise services without requiring the web host." Controllers stay thin, so the meaningful
logic lives in services and is unit-testable. Integration coverage of the full HTTP flow is
validated manually per [quickstart.md](./quickstart.md) in Phase 1.

**Alternatives considered**: *Full end-to-end browser automation* — deferred; not required for
Phase 1 acceptance, which is covered by service unit tests + the quickstart validation guide.

## Resolved unknowns summary

| Unknown | Resolution |
|---------|------------|
| Language/version | .NET 9, C#, ASP.NET Core MVC |
| ORM / provider / DB | EF Core 9, Pomelo MySQL 9.0.0, MySQL 8.0 |
| OEE/power schema | Byte-for-byte reproduction of reference `oee_data`/`power_data`; read-only |
| Machine types | `machine_types` lookup table w/ production-line label; 2 seeded, extensible |
| Work-order model | Independent, single input+output, status incl. On Hold; no chaining/ERP |
| Single-active-machine rule | Service-layer enforcement at start |
| Auth | ASP.NET Core Identity, single admin role, `[Authorize]` only |
| Front-end | Reference Bootstrap 5 shell re-skinned; sidebar nav; AR/EN + RTL via resx/cookie |
| Dashboard refresh | ~5 s client polling of a JSON endpoint; no SignalR |
| Testing | xUnit service unit tests + quickstart manual validation |

No `NEEDS CLARIFICATION` items remain. Beta's specific brand palette and logo assets are a design
input to be supplied during implementation; a placeholder Beta palette is used until then and does
not block planning.
