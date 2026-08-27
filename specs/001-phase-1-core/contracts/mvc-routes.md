# Contract: MVC Controller Routes

**Feature**: `001-phase-1-core`

Beta Platform is a server-rendered ASP.NET Core MVC monolith. Its "interface" is the set of
controller actions below. All actions except the auth endpoints require `[Authorize]` (single admin
role — FR-001/FR-002). Controllers are thin: they delegate to services (constitution Principle V).

Convention: `[HttpGet]` returns a Razor view; `[HttpPost]` performs a mutation then redirects
(PRG pattern) with a `TempData` toast. Validation failures re-render the form with messages.

## AuthController (anonymous)

| Method | Route | Purpose |
|--------|-------|---------|
| GET | `/Auth/Login` | Standalone login page (`Layout = null`) |
| POST | `/Auth/Login` | Sign in; on success redirect to `/Dashboard` (FR-001) |
| POST | `/Auth/Logout` | Sign out (FR-003) |

## DashboardController `[Authorize]`

| Method | Route | Purpose |
|--------|-------|---------|
| GET | `/` or `/Dashboard` | Render dashboard shell with initial machine cards (FR-040/041) |
| GET | `/Dashboard/Data` | **JSON** latest status/OEE/power per active machine for polling (FR-043) — see [dashboard-data.md](./dashboard-data.md) |

## MachinesController `[Authorize]`

| Method | Route | Purpose |
|--------|-------|---------|
| GET | `/Machines` | List **and** card view of machines (FR-015) |
| GET | `/Machines/Create` | New-machine form (machine-type dropdown from active seeded types) |
| POST | `/Machines/Create` | Create; enforce unique `machine_code` (FR-012) |
| GET | `/Machines/Edit/{id}` | Edit form |
| POST | `/Machines/Edit/{id}` | Update name/type (FR-010) |
| POST | `/Machines/Deactivate/{id}` | Set `is_active = false`; preserve history (FR-016) |

## ProductsController `[Authorize]`

| Method | Route | Purpose |
|--------|-------|---------|
| GET | `/Products` | List with search/filter (FR-023) |
| GET | `/Products/Create` | New-product form |
| POST | `/Products/Create` | Create; enforce unique `product_code` (FR-022) |
| GET | `/Products/Edit/{id}` | Edit form |
| POST | `/Products/Edit/{id}` | Update (FR-020) |
| POST | `/Products/Deactivate/{id}` | Hide from new selections; keep references (FR-024) |

## WorkOrdersController `[Authorize]`

| Method | Route | Purpose |
|--------|-------|---------|
| GET | `/WorkOrders` | List work orders with status |
| GET | `/WorkOrders/Details/{id}` | Order detail + outputs + aggregates |
| GET | `/WorkOrders/Create` | New-order form (single input/output product, machine, qty) |
| POST | `/WorkOrders/Create` | Create with status `Ready`; unique number (FR-031/037) |
| GET | `/WorkOrders/Edit/{id}` | Edit while not Finished (FR-038) |
| POST | `/WorkOrders/Edit/{id}` | Update assignment/qty (FR-038) |
| POST | `/WorkOrders/Start/{id}` | `Ready → InProgress`; set `started_at`; enforce single-active-machine (FR-035/039) |
| POST | `/WorkOrders/Hold/{id}` | `InProgress → OnHold` (FR-033/034) |
| POST | `/WorkOrders/Resume/{id}` | `OnHold → InProgress` (FR-034) |
| POST | `/WorkOrders/Finish/{id}` | `InProgress → Finished`; set `finished_at` (FR-035) |
| POST | `/WorkOrders/AddOutput/{id}` | Record an output (unique code + weight) (FR-036) |
| POST | `/WorkOrders/Delete/{id}` | Delete/cancel a work order (FR-030) |

**Invalid transition responses**: `Start`/`Hold`/`Resume`/`Finish` validate the current status
against the state machine in [data-model.md](./data-model.md); an invalid transition returns to the
detail page with an explanatory `TempData["Error"]` (FR-034, acceptance scenario 6).

## CultureController `[Authorize]`

| Method | Route | Purpose |
|--------|-------|---------|
| POST | `/Culture/SetLanguage` | Set culture cookie (`en`/`ar`), `LocalRedirect(returnUrl)` (FR-062) |

## Out of scope (Phase 1)

No OEE-reports, Power-monitoring, Financial, Alerts, Administration/Users/Roles, or ERP endpoints
(FR-045 and spec "Out of scope"). No telemetry ingestion endpoint of any kind (FR-042).
