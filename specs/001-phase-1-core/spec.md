# Feature Specification: Beta Platform — Phase 1 (Machines, Products, Work Orders & Monitoring Dashboard)

**Feature Branch**: `001-phase-1-core`

**Created**: 2026-07-06

**Status**: Draft

**Input**: User description: "Phase 1 of Beta Platform: Machine CRUD, Product CRUD, Work Order CRUD and the work-order workflow, plus a dashboard showing only machine status, OEE data, and power. OEE and power tables match the previous SPackEdgeView reference project. Front end mirrors the Spack front end but with different colors, logo, and design."

## Overview

Beta Platform is an Industrial IoT (IIoT) / Manufacturing Execution System (MES) for the Beta
factory's Industry 4.0 digital transformation. Phase 1 delivers the foundational master-data
management, the work-order lifecycle, and a live monitoring dashboard. It is the first slice of a
larger platform whose later phases add analytics/reporting, a financial module, and Odoo ERP
integration.

**In scope for Phase 1**: Machine management, Product management, Work Order management and its
workflow, basic authentication, and a monitoring dashboard limited to machine status, OEE, and
power. OEE and power readings are written **directly into the database tables by the IoT team**;
Phase 1 only defines those tables and displays their data.

**Out of scope for Phase 1**: Machine types other than the Forming Machine and Flat Washer Line
(i.e., Round Head, Gluing and Sanding, Twisting, Bending, Spring Washer, Bolts, and Nuts), Odoo ERP
integration, analytics/OEE reports interface, power analytics dashboard, financial/cost module,
Excel/PDF export, alerting, granular role-based permissions, and any application-side ingestion API
or manual entry of OEE/power telemetry.

## Clarifications

### Session 2026-07-06

- Q: How should machine types be modeled (Armor Rod line has 5 sub-machines; others are single lines)? → A: A flat list of machine types, each carrying a production-line grouping label — no separate production-line hierarchy/entity.
- Q: What language(s) must the Phase 1 UI support? → A: Bilingual Arabic + English with right-to-left (RTL) layout.
- Q: How should the monitoring dashboard update to show "live" data? → A: Periodic auto-refresh (the page polls and refreshes every few seconds; ~5-second default).
- Q: How should a work order's input product(s) be captured? → A: A single input product per work order.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Machine Management (Priority: P1)

A production administrator maintains the registry of factory machines so that work orders,
monitoring, and telemetry can all reference a consistent, up-to-date list of machines.

**Why this priority**: Machines are the anchor entity for the entire platform — work orders are
assigned to them and all OEE/power telemetry references them. Nothing else can function without a
machine registry, and managing machines alone is already a usable, demonstrable capability.

**Independent Test**: Sign in, create several machines of different machine types, view them in a
list and card view, edit one, deactivate one, and confirm the changes persist and display
correctly — all without any other module being present.

**Acceptance Scenarios**:

1. **Given** an authenticated admin on the machines page, **When** they add a machine with a name,
   code, and machine type, **Then** the machine appears in the list and card views and is available
   for selection elsewhere.
2. **Given** an existing machine, **When** the admin edits its name or type, **Then** the updated
   values are saved and shown.
3. **Given** an existing machine, **When** the admin marks it inactive, **Then** it is excluded
   from active-machine selections but its historical data remains intact.
4. **Given** a machine code that already exists, **When** the admin tries to create another machine
   with the same code, **Then** the system rejects it with a clear validation message.

---

### User Story 2 - Product Management (Priority: P2)

A production administrator maintains the catalog of products (raw materials, intermediates, and
finished goods) so that work orders can reference valid input and output products.

**Why this priority**: Work orders require input and output products to exist. The product catalog
is a prerequisite for the work-order module but is independently testable and valuable as a
managed reference list.

**Independent Test**: Create products with code, name, category, and unit; list, search, edit, and
deactivate them; verify uniqueness of product code is enforced.

**Acceptance Scenarios**:

1. **Given** an authenticated admin, **When** they create a product with a unique code, name, and
   unit, **Then** it is saved and appears in the product list.
2. **Given** an existing product, **When** the admin edits its details, **Then** the changes
   persist.
3. **Given** a product referenced by a work order, **When** the admin deactivates it, **Then** it
   is hidden from new selections while existing references remain valid.
4. **Given** a duplicate product code, **When** the admin submits, **Then** the system rejects it
   with a validation message.

---

### User Story 3 - Work Order Management & Workflow (Priority: P3)

A production administrator creates and manages work orders that consume input product(s) and
produce a finished-product output on an assigned machine, moving each order through its lifecycle
from Ready to Finished.

**Why this priority**: Work orders are the core production-tracking capability, but they depend on
machines and products already existing. Each work order is self-contained (its output is the end
product and is not fed into another order), so the module is fully testable on its own once master
data exists.

**Independent Test**: Create a work order selecting a single input product, an output (end) product,
an assigned machine, and quantity; advance it Ready → In Progress, place it On Hold and resume,
record outputs, and finish it; confirm status transitions and recorded production are correct.

**Acceptance Scenarios**:

1. **Given** existing machines and products, **When** the admin creates a work order with a unique
   number, a single input product, an output product, an assigned machine, and quantity to
   manufacture, **Then** the work order is saved with status Ready.
2. **Given** a Ready work order, **When** the admin starts it, **Then** its status becomes In
   Progress and its start time is recorded.
3. **Given** an In Progress work order, **When** the admin places it On Hold, **Then** its status
   reflects On Hold and it can later be resumed to In Progress.
4. **Given** an In Progress work order, **When** the admin finishes it, **Then** its status becomes
   Finished and its finish time is recorded.
5. **Given** a work order in production, **When** outputs (finished units, each with weight and a
   unique code) are recorded against it, **Then** total output count and weight are reflected on the
   order.
6. **Given** an invalid transition (e.g., finishing a Ready order that never started), **When**
   attempted, **Then** the system prevents it and explains why.
7. **Given** a work order number that already exists, **When** the admin creates another with the
   same number, **Then** the system rejects it.
8. **Given** a machine that already has an active (In Progress or On Hold) work order, **When** the
   admin tries to start another work order on that same machine, **Then** the system rejects it with
   a clear message; **and** once the first order is Finished, starting the second order succeeds.

---

### User Story 4 - Monitoring Dashboard (Priority: P4)

A factory supervisor opens the dashboard to see, at a glance, the live status of every machine
along with its current OEE and power draw, so they can spot stopped machines and performance issues
immediately.

**Why this priority**: The dashboard is the headline visibility feature, but it depends on machines
existing and on OEE/power tables being populated (by the IoT team). It is independently testable by
seeding representative telemetry rows and confirming they render correctly.

**Independent Test**: With machines registered and sample OEE/power rows present in the database,
open the dashboard and verify each machine card shows the correct running/stopped status, live OEE
percentage (with Availability, Performance, Quality), units produced (total vs. good), and live
power (kW); confirm values refresh to reflect the latest telemetry.

**Acceptance Scenarios**:

1. **Given** registered machines with recent OEE and power telemetry, **When** the supervisor opens
   the dashboard, **Then** each active machine is shown with a status tag (e.g., Running/Stopped),
   live OEE %, and live power (kW).
2. **Given** a machine's latest OEE record, **When** the card renders, **Then** it displays
   Availability, Performance, and Quality and units produced (total vs. good).
3. **Given** new telemetry is written to the tables, **When** the dashboard updates, **Then** the
   displayed values reflect the most recent readings for each machine.
4. **Given** a machine with no telemetry yet, **When** the dashboard renders, **Then** it shows the
   machine with an empty/unknown status rather than failing.

---

### Edge Cases

- What happens when a machine referenced by historical telemetry or work orders is deactivated? It
  must remain resolvable for display; it is only hidden from new selections.
- How does the system handle a work order whose assigned machine is later deactivated?
- What happens when OEE/power tables contain readings for a machine that no longer exists or is
  malformed? The dashboard must degrade gracefully without erroring.
- What happens on duplicate machine code, product code, or work order number?
- How are invalid work-order status transitions handled (e.g., On Hold from Ready, Finish without
  Start)?
- What happens when a work order is finished with zero recorded outputs?
- What happens when a second work order is started on a machine that already has an active (In
  Progress or On Hold) work order? The start MUST be blocked until the machine is free.
- How does the dashboard behave when telemetry is stale (no recent readings)?

## Requirements *(mandatory)*

### Functional Requirements

#### Authentication (Basic)

- **FR-001**: The system MUST require a user to sign in before accessing any management or dashboard
  functionality.
- **FR-002**: The system MUST support a single administrative role in Phase 1 (no granular
  role-based permissions); an authenticated user may perform all Phase 1 actions.
- **FR-003**: The system MUST allow an authenticated user to sign out.

#### Machine Management

- **FR-010**: Users MUST be able to create, view, edit, and deactivate machines.
- **FR-011**: Each machine MUST have a unique machine code, a display name, and a machine type.
- **FR-012**: The system MUST enforce uniqueness of machine code and reject duplicates with a clear
  message.
- **FR-013**: In Phase 1 the system MUST support only the two machine types explicitly marked
  "(Phase 1)" in the requirements document: the **Forming Machine** (of the Armor Rod & Guy Grip
  line) and the **Flat Washer Line**. Each machine type is held in a single flat list and carries a
  production-line grouping label (no separate production-line entity/hierarchy). The remaining
  types — Round Head Machine, Gluing and Sanding Machine, Twisting Machine, Bending Machine, Spring
  Washer Line, Bolts Production Line, and Nuts Production Line — are out of scope for Phase 1 and MUST
  NOT be selectable; the machine-type list MUST be extensible so they can be added in a later phase.
- **FR-014**: The system MUST track whether a machine is active (enabled) and whether it is
  currently running.
- **FR-015**: The system MUST present machines in both a list view and a card view.
- **FR-016**: Deactivating a machine MUST NOT delete its historical telemetry or work-order links.

#### Product Management

- **FR-020**: Users MUST be able to create, view, edit, and deactivate products.
- **FR-021**: Each product MUST have a unique product code, a name, a unit of measure, and an
  optional category; an optional English name MUST be supported.
- **FR-022**: The system MUST enforce uniqueness of product code and reject duplicates.
- **FR-023**: The system MUST allow products to be searched/filtered in the list view.
- **FR-024**: Deactivating a product MUST hide it from new selections while preserving existing
  work-order references.

#### Work Order Management & Workflow

- **FR-030**: Users MUST be able to create, view, edit, and delete/cancel work orders.
- **FR-031**: Each work order MUST have a unique work order number, exactly one input product, one
  output (finished/end) product, an assigned machine, a planned start time, and a quantity to
  manufacture.
- **FR-032**: Work orders MUST be independent: the output of a work order is the final end product
  and is NOT consumed as an input by any subsequent work order (no cross-order production chaining or
  multi-stage traceability in Phase 1).
- **FR-033**: The system MUST support the work order lifecycle statuses: Ready, In Progress, On
  Hold, and Finished.
- **FR-034**: The system MUST enforce valid status transitions (Ready → In Progress; In Progress ↔
  On Hold; In Progress → Finished) and reject invalid ones with an explanatory message.
- **FR-035**: The system MUST record the actual start time when a work order moves to In Progress
  and the finish time when it moves to Finished.
- **FR-036**: The system MUST allow recording production outputs against a work order, each output
  capturing at least a unique code and weight, and MUST maintain total output count and total output
  weight.
- **FR-037**: The system MUST enforce uniqueness of work order number.
- **FR-038**: The system MUST allow assigning/reassigning a work order to a machine while it is not
  yet Finished.
- **FR-039**: The system MUST NOT allow two work orders to run on the same machine at the same time.
  A machine may have at most one active (In Progress or On Hold) work order; the system MUST reject
  any attempt to start (move to In Progress) a work order on a machine that already has another
  active work order, with a clear message. Once the occupying order is Finished, the machine becomes
  available again.

#### Monitoring Dashboard

- **FR-040**: The dashboard MUST display a card for each active machine showing its live status tag
  (e.g., Running/Stopped), live OEE %, and live power (kW).
- **FR-041**: Each machine card MUST display the OEE components — Availability, Performance, and
  Quality — and units produced (total vs. good) from the latest OEE reading.
- **FR-042**: The dashboard MUST read OEE and power values from the database tables that the IoT
  team populates directly; the application MUST NOT provide an ingestion API or manual entry for
  this telemetry in Phase 1.
- **FR-043**: The dashboard MUST reflect the most recent telemetry per machine and automatically
  refresh on a periodic interval (polling; ~5-second default) so displayed status, OEE, and power
  stay current without a manual page reload. Real-time server push is not required in Phase 1.
- **FR-044**: The dashboard MUST handle machines with missing or stale telemetry gracefully,
  showing an empty/unknown state rather than failing.
- **FR-045**: The dashboard scope in Phase 1 is limited to machine status, OEE data, and power; no
  work-order KPIs, analytics, financial, or power-analytics visualizations are included.

#### Data & Compatibility

- **FR-050**: The OEE data table MUST be schema-compatible with the reference SPackEdgeView project
  so the IoT team's existing writers work unchanged — per machine and timestamp, capturing
  availability, performance, quality, total count, total goods, running status, and an optional
  associated work order, with OEE derived as (Availability × Performance × Quality).
- **FR-051**: The power data table MUST be schema-compatible with the reference SPackEdgeView
  project — per machine and timestamp, capturing consumed energy (kWh), phase/line voltages,
  line/phase currents and average current, and frequency (power quality parameters such as peak
  power, power factor, and phase unbalance MAY be included for forward compatibility but are not
  displayed in Phase 1).
- **FR-052**: All persisted data MUST be retained (soft-deactivation preferred over hard deletion
  for master data) so historical records remain intact.

#### User Interface

- **FR-060**: The application MUST provide sidebar navigation with entries for Dashboard, Machines,
  Products, and Work Orders.
- **FR-061**: The user interface MUST reuse the layout and interaction patterns of the reference
  Spack (SPackEdgeView) front end while applying Beta Platform's own distinct color scheme, logo,
  and visual design.
- **FR-062**: The user interface MUST be bilingual (Arabic and English) with right-to-left (RTL)
  layout support, allowing the user to switch languages; product Arabic and English names MUST be
  presented according to the selected language.

### Key Entities *(include if feature involves data)*

- **Machine**: A physical production machine. Attributes: unique code, name, machine type, active
  flag, running flag. Referenced by work orders and all telemetry.
- **Machine Type**: The category of a machine, held as a single flat list where each type also
  carries a production-line grouping label. Phase 1 includes only two types — Forming Machine (Armor
  Rod & Guy Grip line) and Flat Washer Line; the remaining Beta types are deferred to later phases.
  No separate production-line entity exists, and the list is extensible for future types.
- **Product**: A material or finished good. Attributes: unique code, name, optional English name,
  optional category, unit of measure, active flag. Used as work-order inputs and outputs.
- **Work Order**: A production task. Attributes: unique number, a single input product, an output
  (end) product, assigned machine, planned/actual start & finish times, quantity to manufacture,
  status. Independent of other work orders.
- **Work Order Output**: A produced unit/batch recorded against a work order. Attributes: unique
  code, weight, timestamp. Aggregated into total count and total weight for the order.
- **OEE Reading**: A time-stamped machine effectiveness record (availability, performance, quality,
  total count, total goods, running status, optional work-order link) written by the IoT team;
  read-only to the application.
- **Power Reading**: A time-stamped machine energy record (consumed energy, voltages, currents,
  frequency, and related power parameters) written by the IoT team; read-only to the application.
- **User**: An authenticated operator/administrator of the platform (single admin role in Phase 1).

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: An administrator can register a new machine and see it available for selection in
  under 1 minute, with duplicate codes reliably prevented (0 duplicate codes accepted).
- **SC-002**: An administrator can create a product and a complete work order (from empty state
  through assignment) in under 3 minutes.
- **SC-003**: A work order can be driven through its full lifecycle (Ready → In Progress → On Hold →
  In Progress → Finished) with every transition and start/finish timestamp recorded correctly, and
  100% of invalid transitions rejected.
- **SC-004**: The monitoring dashboard displays every active machine's status, OEE %, and power, and
  reflects newly written telemetry within one refresh cycle for at least the number of machines
  targeted in Phase 1.
- **SC-005**: The dashboard renders without error when up to 100% of machines have missing or stale
  telemetry, showing an unknown state instead of failing.
- **SC-006**: OEE and power data written by the IoT team using the reference project's existing
  format is displayed correctly with zero schema-related insertion failures.
- **SC-007**: A first-time user can locate and open each Phase 1 area (Dashboard, Machines,
  Products, Work Orders) from the sidebar on the first attempt.

## Assumptions

- **Data acquisition**: In Phase 1 the IoT team has direct database access and inserts OEE and power
  readings straight into the tables. The application therefore provides no ingestion API and no
  manual telemetry entry; it only defines schema-compatible tables and reads from them.
- **Reference compatibility**: The OEE and power table schemas mirror the previous SPackEdgeView
  project (`E:\Projects\Work\S\web`) so existing IoT writers work unchanged. Other entities
  (machines, products, work orders) are adapted to Beta's requirements and are not required to match
  the reference exactly.
- **Work-order model**: Unlike the reference project's multi-stage chained traceability, Beta's work
  orders are independent — each consumes input product(s) and produces an end-product output that is
  not fed into a subsequent order.
- **Machine types**: Only the two machine types the requirements document marks "(Phase 1)" — the
  Forming Machine and the Flat Washer Line — are in scope for Phase 1. All other Beta machine types
  (Round Head, Gluing and Sanding, Twisting, Bending, Spring Washer, Bolts, Nuts) are deferred to
  later phases; the machine-type list is designed to be extended when they are added.
- **Authentication**: Basic authentication with a single administrative role; granular role-based
  permissions (Production Admin vs. Operator distinctions in the source document) are deferred to a
  later phase.
- **Master/production data entry**: All master and production data is entered manually in the
  application until the Odoo ERP integration in a later phase; Odoo integration is out of Phase 1
  scope.
- **Localization/timezone**: The UI is bilingual Arabic/English with RTL support and a language
  switch. Timestamps are handled consistently in the factory's local (KSA) timezone, as in the
  reference project.
- **Deferred modules**: Analytics/OEE reports, power-analytics dashboard, financial module,
  export (Excel/PDF), and alerting are out of scope for Phase 1.
- **Process-flow & equations detail**: The document's page-12 process flow and page-14 equations are
  diagrams; OEE is computed as Availability × Performance × Quality (consistent with the reference
  project). Any additional equation nuances will be confirmed during planning if they affect Phase 1.
