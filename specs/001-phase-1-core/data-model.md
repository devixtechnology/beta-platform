# Phase 1 Data Model: Beta Platform — Phase 1

**Feature**: `001-phase-1-core` | **Date**: 2026-07-07 | **Provider**: EF Core 9 / Pomelo MySQL 8.0

All tables are created and altered **exclusively via EF Core migrations** (constitution
Principle IV). Two tables — `oee_data` and `power_data` — are a **compatibility contract** with the
reference SPackEdgeView project and MUST reproduce its columns exactly; they are **read-only** to
the Beta application. All other tables are Beta-owned and designed fresh.

Legend: 🔒 = compatibility-locked (must match reference exactly); ✍️ = written by IoT team, read-only
to app; 🆕 = Beta-owned.

---

## Entity overview

| Entity | Table | Owner | Notes |
|--------|-------|-------|-------|
| MachineType | `machine_types` | 🆕 app | Lookup; seeded, extensible (FR-013) |
| Machine | `machines` | 🆕 app | Anchor entity; PK referenced by telemetry |
| Product | `products` | 🆕 app | Reused shape from reference |
| WorkOrder | `work_orders` | 🆕 app | Independent orders; incl. On Hold |
| WorkOrderOutput | `work_order_outputs` | 🆕 app | Produced units per order |
| OeeData | `oee_data` | 🔒 ✍️ IoT | Read-only telemetry |
| PowerData | `power_data` | 🔒 ✍️ IoT | Read-only telemetry |
| ApplicationUser | ASP.NET Identity tables | 🆕 app | Single admin role |

---

## MachineType — `machine_types` 🆕

Flat, extensible list of machine types; each carries a production-line grouping label. Seeded with
the two Phase 1 types only.

| Field | Type | Constraints |
|-------|------|-------------|
| `machine_type_id` | `int` | PK, identity |
| `name` | `varchar(80)` | Required |
| `name_english` | `varchar(80)` | Nullable |
| `production_line` | `varchar(100)` | Required — grouping label (e.g. "Armor Rod & Guy Grip line") |
| `is_active` | `bool` | Default `true` |
| `created_at` | `datetime(6)` | Default KSA now |

**Indexes**: unique(`name`).

**Seed (via migration)**:
1. `name` = "Forming Machine", `production_line` = "Armor Rod & Guy Grip line"
2. `name` = "Flat Washer Line", `production_line` = "Flat Washer Line"

> The other seven Beta types (Round Head, Gluing and Sanding, Twisting, Bending, Spring Washer,
> Bolts, Nuts) are **not** seeded in Phase 1 and MUST NOT be selectable; they are added by a future
> migration seed with no code change (FR-013).

**Validation**: name unique; a machine type referenced by any machine cannot be hard-deleted —
deactivate (`is_active = false`) instead.

---

## Machine — `machines` 🆕 (PK referenced by telemetry)

Column names/PK match the reference so `oee_data.machine_id` / `power_data.machine_id` resolve. The
only structural change vs. reference is `machine_type` (enum) → `machine_type_id` (FK to
`machine_types`).

| Field | Type | Constraints |
|-------|------|-------------|
| `machine_id` | `int` | PK, identity — **referenced by `oee_data`/`power_data`** |
| `machine_name` | `varchar(50)` | Required, **unique** |
| `machine_code` | `varchar(20)` | Required, **unique** (FR-011/FR-012) |
| `machine_type_id` | `int` | Required, FK → `machine_types.machine_type_id` (`RESTRICT`) |
| `is_active` | `bool` | Default `true` — enabled in system (FR-014) |
| `is_running` | `bool` | Default `false` — currently running (FR-014) |
| `created_at` | `datetime(6)` | Default KSA now |

**Indexes**: unique(`machine_code`), unique(`machine_name`), index(`machine_type_id`).

**Validation / rules**:
- `machine_code` unique — duplicate rejected with a clear message (FR-012).
- Only `machine_type_id` values that are active and in the Phase 1 seed are selectable (FR-013).
- **Deactivation** sets `is_active = false`; it MUST NOT delete telemetry or work-order links
  (FR-016, FR-052). Deactivated machines are excluded from new selections but remain resolvable for
  display.

**Relationships**: one Machine → many OeeData, many PowerData, many WorkOrder (assigned).

---

## Product — `products` 🆕 (shape reused from reference)

| Field | Type | Constraints |
|-------|------|-------------|
| `product_id` | `int` | PK, identity |
| `product_code` | `varchar(50)` | Required, **unique** (FR-022) |
| `product_name` | `varchar(200)` | Required |
| `product_name_english` | `varchar(200)` | Nullable (FR-021, FR-062) |
| `category` | `varchar(100)` | Nullable |
| `unit` | `varchar(20)` | Required, default `"kg"` |
| `is_active` | `bool` | Default `true` |
| `created_at` | `datetime(6)` | Default KSA now |

**Indexes**: unique(`product_code`), index(`category`), index(`is_active`).

**Validation / rules**:
- `product_code` unique — duplicate rejected (FR-022).
- Searchable/filterable in list view by name/code/category (FR-023).
- **Deactivation** hides from new selections but preserves existing work-order references
  (FR-024, FR-052).
- Arabic (`product_name`) vs English (`product_name_english`) shown per selected culture (FR-062).

---

## WorkOrder — `work_orders` 🆕 (independent orders)

Single input + single output product; independent of other orders (no chaining). Beta-owned enum
adds **On Hold**.

| Field | Type | Constraints |
|-------|------|-------------|
| `work_order_id` | `int` | PK, identity |
| `work_order_number` | `varchar(50)` | Required, **unique** (FR-037) |
| `input_product_id` | `int` | Required, FK → `products.product_id` (`RESTRICT`) — exactly one (FR-031) |
| `output_product_id` | `int` | Required, FK → `products.product_id` (`RESTRICT`) — the end product |
| `machine_id` | `int?` | Nullable, FK → `machines.machine_id` (`SET NULL`) — assigned machine |
| `planned_start_time` | `datetime(6)` | Required |
| `qty_to_manufacture` | `decimal(10,2)` | Required |
| `status` | `int` (enum) | Required, default `Ready` |
| `started_at` | `datetime(6)?` | Set on → InProgress (FR-035) |
| `finished_at` | `datetime(6)?` | Set on → Finished (FR-035) |
| `created_at` | `datetime(6)` | Default KSA now |

**Status enum** (`WorkOrderStatus`): `Ready=1, InProgress=2, OnHold=3, Finished=4`.

**Indexes**: unique(`work_order_number`), index(`status`), index(`machine_id`), index(`created_at`).

**State machine** (FR-034 — reject all others with an explanatory message):

```
Ready ──start──▶ InProgress ──hold──▶ OnHold
                     │  ▲                 │
                     │  └─────resume──────┘
                     └──finish──▶ Finished
```

- `Ready → InProgress`: allowed; set `started_at`; **enforce single-active-machine rule** (below).
- `InProgress → OnHold`: allowed.
- `OnHold → InProgress`: allowed (resume).
- `InProgress → Finished`: allowed; set `finished_at`.
- Any other transition (e.g. `Ready → Finished`, `Ready → OnHold`, `Finished → *`): **rejected**.

**Single-active-machine rule** (FR-039, enforced in service on *start*): a machine may have at most
one work order in `InProgress` **or** `OnHold`. Starting a second order on an occupied machine is
rejected with a clear message; once the occupier is `Finished`, the machine frees up. (Service-layer
check, not a DB constraint — see [research.md](./research.md) §4.)

**Edit/assign**: name/products/machine/qty editable while status ≠ `Finished`; machine may be
assigned/reassigned while not Finished (FR-038). Delete/cancel supported (FR-030).

**Aggregates** (derived, not stored): total output count = `COUNT(outputs)`; total output weight =
`SUM(outputs.weight)` (FR-036).

**Relationships**: one WorkOrder → many WorkOrderOutput; → one input Product, one output Product,
optional Machine.

---

## WorkOrderOutput — `work_order_outputs` 🆕

A produced unit/batch recorded against a work order.

| Field | Type | Constraints |
|-------|------|-------------|
| `output_id` | `int` | PK, identity |
| `work_order_id` | `int` | Required, FK → `work_orders.work_order_id` (`CASCADE`) |
| `unique_code` | `varchar(50)` | Required, **unique** (FR-036) |
| `weight` | `decimal(10,2)` | Required |
| `sequence_number` | `int` | Per-order sequence |
| `notes` | `varchar(500)` | Nullable |
| `created_at` | `datetime(6)` | Default KSA now |

**Indexes**: unique(`unique_code`), index(`work_order_id`, `sequence_number`).

**Rules**: each output has at least a unique code + weight (FR-036); aggregated into the order's
total count and total weight. A work order may be finished with zero outputs (edge case — allowed,
totals are zero).

---

## OeeData — `oee_data` 🔒 ✍️ (read-only; exact reference match)

**MUST reproduce the reference exactly** so IoT writers work unchanged. Application reads only.

| Column | MySQL type | Null | Notes |
|--------|------------|------|-------|
| `id` | `int` identity | no | PK |
| `machine_id` | `int` | no | FK → `machines.machine_id` (`CASCADE`) |
| `timestamp` | `datetime(6)` | no | reading time |
| `availability` | `decimal(5,2)` | no | percentage 0–100, default 0 |
| `quality` | `decimal(5,2)` | no | percentage 0–100, default 0 |
| `performance` | `decimal(5,2)` | no | percentage 0–100, default 0 |
| `total_count` | `decimal(65,30)` | no | default 0 (MySQL default precision — do NOT set) |
| `total_goods` | `decimal(65,30)` | no | default 0 |
| `status` | `tinyint unsigned` | no | `0=Stopped, 1=Running`, default 0 |
| `created_at` | `datetime(6)` | no | KSA now |
| `order_id` | `int` | yes | FK → `work_orders.work_order_id`; set by IoT |

**Indexes** (match reference): index(`machine_id`, `timestamp`), index(`timestamp`),
index(`order_id`).

**Computed (app-side, `[NotMapped]`)**: `OEE = (Availability × Performance × Quality) / 10000`
(components are percentages, so this yields a 0–100 OEE %).

> ⚠️ `availability/quality/performance` are `decimal(5,2)` with precision set in fluent config;
> `total_count/total_goods` intentionally have **no** precision configured (fall back to MySQL
> `decimal(65,30)`) — replicate this to stay writer-compatible.

---

## PowerData — `power_data` 🔒 ✍️ (read-only; exact reference match)

| Column | MySQL type | Null | Notes |
|--------|------------|------|-------|
| `id` | `int` identity | no | PK |
| `machine_id` | `int` | no | FK → `machines.machine_id` (`CASCADE`) |
| `timestamp` | `datetime(3)` | no | **millisecond** precision (differs from oee_data) |
| `kw_hr` | `decimal(10,2)` | yes | consumed energy (kWh) |
| `v1` | `decimal(8,2)` | yes | phase voltage 1 |
| `v2` | `decimal(8,2)` | yes | phase voltage 2 |
| `v3` | `decimal(8,2)` | yes | phase voltage 3 |
| `v12` | `decimal(8,2)` | yes | line voltage 1-2 |
| `v23` | `decimal(8,2)` | yes | line voltage 2-3 |
| `v13` | `decimal(8,2)` | yes | line voltage 1-3 |
| `a1` | `decimal(8,2)` | yes | current 1 |
| `a2` | `decimal(8,2)` | yes | current 2 |
| `a3` | `decimal(8,2)` | yes | current 3 |
| `a_avg` | `decimal(8,2)` | yes | average current |
| `frequency` | `decimal(5,2)` | yes | frequency (Hz) |
| `created_at` | `datetime(6)` | no | KSA now |

**Indexes** (match reference): index(`machine_id`, `timestamp`), index(`timestamp`).

> Phase 1 displays only power (kW, from `kw_hr`). Peak power / power factor / phase unbalance are
> **not** columns in the writer contract and are omitted (see [research.md](./research.md) §2).

---

## ApplicationUser — ASP.NET Identity 🆕

`ApplicationUser : IdentityUser` with added fields: `FullName` (string), `CreatedAt` (`datetime(6)`,
KSA now), `IsActive` (bool, default true). Standard ASP.NET Core Identity tables via
`IdentityDbContext<ApplicationUser>`. **Single admin role** in Phase 1 (FR-002); all Phase 1 actions
permitted to any authenticated user. No permissions/roles-permission tables.

---

## Referential integrity & retention summary

- Telemetry → machine: `ON DELETE CASCADE` (matches reference), but **machines are never hard
  deleted** — deactivation only (FR-016, FR-052), so cascade never fires in practice.
- Work order → products: `RESTRICT` (a product referenced by an order can't be hard-deleted;
  deactivate instead — FR-024).
- Work order → machine: `SET NULL` on machine removal (machines aren't hard-deleted in practice).
- Work order → outputs: `CASCADE`.
- **Soft-deactivation preferred over hard deletion for all master data** (FR-052).
