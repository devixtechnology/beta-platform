# Contract: Creation Pages (Side-by-Side Form + Live Preview)

**Feature**: 002-ui-modernization | Presentation contract (no HTTP API change)

Applies to **Machine**, **Product**, and **Work Order** Create (and Edit, for parity). Routes,
HTTP verbs, posted field names, and server-side validation are **unchanged** — this contract governs
only layout and the client-side live preview.

> **As built:** the side-by-side scaffold is **inlined in each view** as
> `<form class="form-layout" data-live-form>` wrapping a `.form-pane` (the form `card`) and a
> `.preview-pane` (the sticky `.preview-card`) — a CSS-grid layout in `site.css`, **not** a
> `_FormLayout.cshtml` partial. Views bind directly to the entities (no create/edit view models).

## 1. Layout contract

```
┌────────────────────────────────────────────────────────────┐
│  Page header:  <Title>                         [primary?]   │
├───────────────────────────────┬────────────────────────────┤
│  FORM (primary column)         │  LIVE PREVIEW (secondary)  │
│  ── Section: Basic info        │  ┌──────────────────────┐  │
│   Label * [input]  helper      │  │  rendered record card │  │
│   Label   [select]             │  │  (updates on input)   │  │
│  ── Section: Status/details    │  └──────────────────────┘  │
│   ...                          │  Contextual help / tips     │
│  [ Save (busy-aware) ] [Cancel]│                            │
└───────────────────────────────┴────────────────────────────┘
   Narrow viewport → columns stack: form first, preview below.
```

**Rules**:
1. Fields grouped into labeled sections (no bare stack). Required fields visibly marked +
   `aria-required="true"`. Helper text where input is non-obvious (FR-001/002).
2. Primary action **Save** is emphasized; **Cancel** is a distinct secondary action. On submit, Save
   enters a **busy state** (spinner + disabled) to prevent duplicate submission (FR-004).
3. Invalid submit shows **field-level** messages and **retains entered values** (FR-003) — existing
   jQuery-unobtrusive client validation + server `ModelState` (unchanged).
4. On success: success toast + redirect to the entity's Index (FR-005), as today.
5. The two columns **stack** on narrow viewports (FR-012); preview never causes horizontal scroll.
6. Full parity in **light/dark** and **LTR/RTL**.

## 2. Live-preview contract (`form-preview.js`)

- Activates on every `form[data-live-form]`; binds `input`/`change` on the form and re-renders the
  preview from current values — **client-side only, no network** (FR-001a).
- **Progressive enhancement**: with JS disabled the form fully works; the preview shows its static
  server-rendered placeholders. No data entry depends on JS.
- **Declarative binding**: each preview element declares `data-preview-for="<input id>"` plus a mode:
  - `data-preview="text"` — mirrors the field's text value;
  - `data-preview="option"` — mirrors the **selected `<option>` text** (resolved name, not the id);
  - `data-preview="badge"` — for a checkbox, swaps `data-on`/`data-off` text + a running/stopped class.
  - `data-empty="…"` supplies the placeholder shown (with a `.preview-empty` class) when the source is
    blank. Edit pages get an initial paint from existing values on load.
- **Submit guard** (same file): on submit of a valid form, the primary `[data-submit-btn]` gets an
  `is-busy` spinner + `aria-busy` to prevent duplicate submission; skipped when client validation
  blocks the submit, so the button never spins on an invalid form (FR-004).

## 3. Per-entity preview field maps

### Machine (`Views/Machines/Create.cshtml`, `Edit.cshtml`)
Reuse the dashboard machine-card visual as the preview.

| Form field | Preview element |
|-----------|-----------------|
| MachineName | Card title |
| MachineCode | Card code/subtitle |
| MachineTypeId | Type label (selected option text) |
| IsRunning | Status badge: Running (success) / Stopped (danger) |

### Product (`Views/Products/Create.cshtml`, `Edit.cshtml`)

| Form field | Preview element |
|-----------|-----------------|
| ProductCode | Preview code |
| ProductName | Preview primary title |
| ProductNameEnglish | Preview secondary title |
| Category | Category chip |
| Unit | Unit label |

### Work Order (`Views/WorkOrders/Create.cshtml`, `Edit.cshtml`)

| Form field | Preview element |
|-----------|-----------------|
| WorkOrderNumber | Summary title |
| InputProductId | "From" product (selected option text) |
| OutputProductId | "To" product (selected option text) |
| MachineId | Assigned machine (selected option text) |
| PlannedStartTime | Planned start (formatted) |
| QtyToManufacture | Quantity (+ unit if resolvable) |

## 4. Non-goals

- No new fields, validations, or endpoints.
- No server-side preview rendering (preview is client-side only).
- Details pages get visual parity but no live preview (they are read-only).
