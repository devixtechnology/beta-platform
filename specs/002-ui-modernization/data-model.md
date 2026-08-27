# Phase 1 Data Model: UI Modernization

**Feature**: 002-ui-modernization | **Date**: 2026-07-08

This feature is **presentation-only**. It introduces **no new database entities, columns, indexes,
or constraints**, and therefore **no EF Core migration** (Constitution Principle IV → N/A here). The
existing entities (Machine, Product, Work Order, MachineType, telemetry tables) are unchanged.

What this feature *does* introduce are (a) a small amount of **client-side/presentation state** and
(b) optional **view models** that shape the reworked creation pages. Both are documented below.

---

## 1. Presentation state (not persisted in the database)

### 1.1 Theme preference

| Attribute | Value |
|-----------|-------|
| Storage | Essential cookie `beta_theme` (see `contracts/theme-toggle.md`) |
| Allowed values | `light` (default) \| `dark` |
| Default (first-time user, no cookie) | `light` (as implemented: `beta_theme == "dark" ? "dark" : "light"`) |
| Lifetime | 1 year, refreshed on change |
| Read | Server-side in `_Layout.cshtml` to set the initial root theme attribute |
| Written | Client-side by `theme.js` on toggle (also flips the attribute live, no reload) |
| Scope | Whole authenticated app; independent of the culture cookie |

No entity, no table, no migration. Rationale in `research.md` D2.

### 1.2 Live-preview state (creation pages)

Transient, in-memory (DOM) only. `form-preview.js` reads current form field values and renders them
into the preview panel. Not persisted anywhere; discarded on navigation. Source of truth for saved
data remains the posted form → existing controller/service/EF path (unchanged).

---

## 2. Optional view models (presentation shaping only)

> **Implementation note (reconciled with code):** none of the optional create/edit view models below
> were added. All three create/edit views bind **directly to the EF entities**
> (`@model BetaPlatform.Data.Entities.{Machine|Product|WorkOrder}`) and carry select-list data via
> `ViewBag`, with the live preview driven by declarative `data-preview-*` attributes. The tables
> below are retained as the field→preview reference map (they match the entity fields the views
> bind). The only view model present is the pre-existing `MachineDetailsViewModel` (Machine Details).

These are thin DTOs used **only** to render the create/edit views if the raw entity is awkward to
bind directly (e.g., to carry select-list data + preview labels alongside the bound fields). They
contain **no business logic** (Principle V) and add no persistence. Using the entity directly (as
built) is acceptable; introduce a view model only where it clarifies the view.

### 2.1 CreateMachineViewModel (optional)

| Field | Source entity field | Preview role | Required |
|-------|--------------------|--------------|----------|
| MachineName | Machine.MachineName | Preview card title | Yes |
| MachineCode | Machine.MachineCode | Preview card subtitle/code | Yes |
| MachineTypeId | Machine.MachineTypeId | Preview type label (resolved name) | No |
| IsRunning | Machine.IsRunning | Preview status badge (Running/Stopped) | — |
| MachineTypes | ViewBag today | Select options | — |

### 2.2 CreateProductViewModel (optional)

| Field | Source entity field | Preview role | Required |
|-------|--------------------|--------------|----------|
| ProductCode | Product.ProductCode | Preview code | Yes |
| ProductName | Product.ProductName | Preview title (localized name) | Yes |
| ProductNameEnglish | Product.ProductNameEnglish | Preview secondary title | No |
| Category | Product.Category | Preview category chip | No |
| Unit | Product.Unit | Preview unit label | No |

### 2.3 CreateWorkOrderViewModel (optional)

| Field | Source entity field | Preview role | Required |
|-------|--------------------|--------------|----------|
| WorkOrderNumber | WorkOrder.WorkOrderNumber | Preview summary title | Yes |
| InputProductId | WorkOrder.InputProductId | Preview "from" product (resolved name) | Yes |
| OutputProductId | WorkOrder.OutputProductId | Preview "to" product (resolved name) | Yes |
| MachineId | WorkOrder.MachineId | Preview assigned machine (resolved name) | Yes |
| PlannedStartTime | WorkOrder.PlannedStartTime | Preview planned start | No |
| QtyToManufacture | WorkOrder.QtyToManufacture | Preview quantity + unit | No |

> Required/optional above reflects the **existing** model validation; this feature does **not**
> change any validation rule. It only surfaces required-ness visually (FR-002) and previews values.

---

## 3. Relationships & lifecycle

Unchanged. No new relationships, states, or transitions are introduced by a presentation refresh.
The Work Order lifecycle, single-active-machine rule, and uniqueness constraints from feature
001 remain the source of truth and are untouched.

## 4. Migration impact

**None.** No `dotnet ef migrations add` is performed for this feature. If a reviewer expects a
migration, that is a signal something non-presentational leaked in — reject it.
