---
description: "Task list for feature 002-ui-modernization implementation"
---

# Tasks: UI Modernization & Enhanced Creation Pages

**Input**: Design documents from `/specs/002-ui-modernization/`

**Prerequisites**: plan.md, spec.md, research.md, data-model.md, contracts/ (all present)

**Tests**: No automated UI tests requested. This is a presentation-only feature validated
**visually/interactively** per `quickstart.md`. Existing xUnit service tests must remain green
(unchanged). No test tasks are generated; validation tasks live in the Polish phase.

**Organization**: Tasks are grouped by user story (US1 â†’ US2 â†’ US3) so each is an independently
deliverable increment.

> **As-built reconciliation (docs match code):** three planned items were simplified during
> implementation and the tasks below are annotated accordingly — (1) **no `_FormLayout.cshtml`
> partial** was created; the side-by-side scaffold is inlined per view via the
> `.form-layout`/`.form-pane`/`.preview-pane` CSS grid (T004/T012). (2) **No create/edit view
> models** were added; views bind directly to the entities with `ViewBag` select lists. (3) The
> effective **default theme is light**, not dark, when no `beta_theme` cookie is present (T021).
> Validation tasks T041/T043/T046 remain **unchecked** — they are manual QA/sign-off steps not yet
> formally recorded.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel â€” **different files**, no dependency on an incomplete task.
- **[Story]**: US1 / US2 / US3. Setup, Foundational, and Polish tasks carry no story label.

## âš ï¸ Shared-file serialization notes (read before parallelizing)

- **`wwwroot/css/site.css` is ONE file.** Every task that edits it is **sequential** â€” never mark two
  `site.css` tasks `[P]` together, even across stories/phases.
- **`Views/Shared/_Layout.cshtml` is ONE file**, edited in Setup (asset wiring), US2 (navbar + theme),
  and US3 (toasts). Those edits are sequential.
- **`Resources/SharedResource.{en,ar}.resx`** localization pairs are edited additively; keep keys
  unique to avoid collisions.
- Different `.cshtml` view files (per entity/page) ARE independent â†’ `[P]` across them is safe.

---

## Phase 1: Setup (Shared Infrastructure)

**Purpose**: Establish the new asset/partial scaffolding and confirm a working baseline before any
styling work.

- [X] T001 Confirm baseline: `dotnet build BetaPlatform.sln` and `dotnet run --project src/BetaPlatform`, sign in, and screenshot the current Dashboard/Machines/Products/WorkOrders + one Create page as the "before" reference (per `quickstart.md` Run section)
- [X] T002 [P] Create empty asset file `src/BetaPlatform/wwwroot/js/theme.js` (placeholder; implemented in US2)
- [X] T003 [P] Create empty asset file `src/BetaPlatform/wwwroot/js/form-preview.js` (placeholder; implemented in US1)
- [X] T004 [P] Create shared partial stubs `src/BetaPlatform/Views/Shared/_ThemeToggle.cshtml` and `_EmptyState.cshtml` (implemented in their stories). **As built:** `_FormLayout.cshtml` was *not* created — the side-by-side scaffold is inlined per view via CSS classes instead (see T012).
- [X] T005 Wire new assets into `src/BetaPlatform/Views/Shared/_Layout.cshtml`: add `<script src="~/js/theme.js" asp-append-version="true">` (load early, before body content, so the theme applies without flash) and `<script src="~/js/form-preview.js" asp-append-version="true" defer>`; keep existing bootstrap/jquery order

**Checkpoint**: App still builds/runs; new (empty) assets load with no errors.

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: The shared **dark-theme (`:root` baseline)** visual foundation â€” tokens, elevation/glass system,
and token-based restyle of the shared components (cards, buttons, forms, tables, badges). Every user
story inherits this globally via CSS. Implements `contracts/design-system.md` Â§1â€“Â§5 for the **dark**
column only; the light token set is added in US2.

**âš ï¸ CRITICAL**: No user story styling should begin until this foundation exists. All tasks below
edit `wwwroot/css/site.css` â†’ **run them sequentially (no [P])**.

- [X] T006 Extend the dark `:root` token set in `src/BetaPlatform/wwwroot/css/site.css`: add `--surface`, `--surface-glass`, `--elev-1/2/3`, `--radius`, `--radius-lg`, `--focus-ring`, `--text`, `--text-muted`, and alias existing brand/status vars to the contract names (`contracts/design-system.md` Â§1, dark column) â€” keep current values backward-compatible
- [X] T007 Add the elevation + glass base system in `site.css`: multi-layer shadow definitions for `--elev-1/2/3`, and a `.glass`/glass-surface treatment using `backdrop-filter: blur()` with a `@supports not (backdrop-filter: blur(1px))` **opaque fallback** (`contracts/design-system.md` Â§2)
- [X] T008 Restyle shared components to tokens in `site.css` (`contracts/design-system.md` Â§3): cards (`--surface`, `--radius-lg`, `--elev-1`, hairline border; glass header), buttons (orange gradient primary + **`:focus-visible` ring**, outline secondary, busy/disabled state), `.form-control`/`.form-select` token bg + orange focus ring, tables themed via tokens (remove hard `table-dark` reliance), status badges (running/stopped/warning/idle) with AA contrast
- [X] T009 Add a global `:focus-visible` ring + dark-theme AA contrast baseline in `site.css` (`contracts/design-system.md` Â§4): ensure text â‰¥ 4.5:1 and UI/graphical â‰¥ 3:1 against dark surfaces; never leave `outline:none` without a replacement ring
- [X] T010 Add RTL base parity for the new component utilities in `site.css` (`contracts/design-system.md` Â§5): logical spacing/mirroring for glass chrome, cards, and the elevation utilities so they render correctly under `dir="rtl"`

**Checkpoint**: Every existing page already looks modernized (dark) via inherited component styles;
foundation ready for story-specific work.

---

## Phase 3: User Story 1 - Modern, Guided Creation Forms (Priority: P1) ðŸŽ¯ MVP

**Goal**: Rework the **Create** pages for Machine, Product, and Work Order into a modern side-by-side
layout (form + client-side live preview) with grouped sections, required markers, inline validation,
helper text, and clear busy-aware Save / Cancel actions.

**Independent Test**: On each of the 3 Create pages (in EN-LTR and AR-RTL): fields are grouped with
required markers and helper text; the preview updates live as you type/select; submitting empty shows
field-level errors **without losing entered values**; a valid submit shows a success toast and
redirects to the list. (Ref: `contracts/creation-pages.md`, spec US1.)

### Implementation for User Story 1

- [X] T011 [US1] Add US1 localization keys to `src/BetaPlatform/Resources/SharedResource.en.resx` and `SharedResource.ar.resx`: section headings (e.g., "Basic info", "Status/Details"), required indicator/aria text, per-field helper text, and preview placeholder strings (`contracts/creation-pages.md` Â§1â€“Â§3)
- [X] T012 [US1] Implement the side-by-side scaffold **as inline markup + CSS** (no partial): `<form class="form-layout" data-live-form>` wrapping a `.form-pane` (form) and `.preview-pane` (sticky preview), a two-column CSS grid in `site.css` that **stacks on narrow viewports** (preview below) and is RTL-correct (`contracts/creation-pages.md` Â§1). Each Create/Edit view carries its own form/preview/actions markup rather than a shared partial's slots.
- [X] T013 [US1] Implement `src/BetaPlatform/wwwroot/js/form-preview.js`: bind `input`/`change` on form fields â†’ re-render the preview panel from current values; for `<select>` show the **selected option text** (not id); graceful placeholders for empty input; **progressive enhancement** (form fully works with JS off) (`contracts/creation-pages.md` Â§2)
- [X] T014 [US1] Add creation-page CSS to `site.css` (sequential â€” same file): two-column form/preview grid, preview-panel glass card, section styling, required-marker style, inline-validation message style, and the Save **busy/spinner** style (`contracts/design-system.md` Â§3, `contracts/creation-pages.md`)
- [X] T015 [P] [US1] Rework `src/BetaPlatform/Views/Machines/Create.cshtml` using the inline `.form-layout` scaffold: grouped fields (MachineName*, MachineCode*, MachineType, IsRunning), required markers + helper text, machine-card **live-preview** target, busy-aware Save + Cancel, `aria-required` on required fields (`contracts/creation-pages.md` Â§3 Machine)
- [X] T016 [P] [US1] Rework `src/BetaPlatform/Views/Products/Create.cshtml` using the inline `.form-layout` scaffold: ProductCode*, ProductName*, ProductNameEnglish, Category, Unit; product preview target (`contracts/creation-pages.md` Â§3 Product)
- [X] T017 [P] [US1] Rework `src/BetaPlatform/Views/WorkOrders/Create.cshtml` using the inline `.form-layout` scaffold: WorkOrderNumber*, InputProduct*, OutputProduct*, Machine*, PlannedStartTime, QtyToManufacture; preview resolves selected product/machine **names** (`contracts/creation-pages.md` Â§3 Work Order)
- [X] T018 [US1] Add submit-guard behavior (in `form-preview.js` or a small handler): on form submit, disable Save + show spinner to prevent duplicate submission; re-enable on validation failure so values are retained (spec FR-004)

**Checkpoint**: All three Create flows are modern, preview live, validate inline, and behave in EN &
AR â€” US1 is independently demoable as the MVP.

---

## Phase 4: User Story 2 - Modern, Distinctive Visual Design System (Priority: P2)

**Goal**: Complete the "elevated depth + glass" system across the app chrome and remaining primary
screens, add the **light theme** token set, and ship the **light/dark theme toggle** (light default,
persisted, no reload/FOUC) while preserving the four navbar tabs and Beta brand.

**Independent Test**: On any page: shared components show the consistent elevated/glass style; the
navbar keeps exactly the 4 tabs in order with modern active/hover states; the theme toggle flips the
whole UI **instantly with no reload**, the choice **persists** across a reload, and a fresh browser
(no cookie) defaults to **light**. (Ref: `contracts/theme-toggle.md`, `contracts/design-system.md`,
spec US2.)

### Implementation for User Story 2

- [X] T019 [US2] Add the **light theme** token override set to `site.css` (sequential â€” same file): `[data-bs-theme="light"]` block redefining `--bg`, `--surface`, `--surface-glass`, `--border`, `--text`, `--text-muted`, status colors, `--elev-*`, `--focus-ring` per `contracts/design-system.md` Â§1 (light column); brand orange unchanged
- [X] T020 [US2] Add US2 localization keys to `SharedResource.en.resx`/`.ar.resx`: theme-toggle accessible label/tooltip (e.g., "Toggle light/dark theme"), light/dark names
- [X] T021 [US2] Read the `beta_theme` cookie server-side in `src/BetaPlatform/Views/Shared/_Layout.cshtml` and stamp `data-bs-theme="@theme"` on `<html>` before first paint â€” **no FOUC** (`contracts/theme-toggle.md` Â§2). **As built:** `theme = cookie == "dark" ? "dark" : "light"`, i.e. the effective default when the cookie is absent/invalid is **light**.
- [X] T022 [US2] Implement `src/BetaPlatform/Views/Shared/_ThemeToggle.cshtml`: an accessible, labeled, keyboard-operable toggle reflecting current theme (sun/moon icon + `aria-label` + `aria-pressed`) (`contracts/theme-toggle.md` Â§3)
- [X] T023 [US2] Implement `src/BetaPlatform/wwwroot/js/theme.js`: on activate, flip root `data-bs-theme` **immediately** (no reload), write the essential `beta_theme` cookie (`dark`|`light`, 1-yr, `SameSite=Lax`, `Path=/`), and update the toggle's visual + `aria` state (`contracts/theme-toggle.md` Â§1, Â§3)
- [X] T024 [US2] Modernize the navbar in `_Layout.cshtml` (sequential â€” same file as T021): glass background, modern active-tab indicator + hover, refined spacing/iconography; **preserve the exact 4 tabs and order** (Dashboard, Machines, Products, Work Orders); place `_ThemeToggle` beside the language switcher (spec FR-008, SC-005)
- [X] T025 [US2] Apply navbar/design-system CSS in `site.css` (sequential â€” same file): active-tab indicator, glass navbar, toggle control styling in both themes (`contracts/design-system.md` Â§3 Navbar)
- [X] T026 [P] [US2] Restyle `src/BetaPlatform/Views/Dashboard/Index.cshtml` and `Views/Dashboard/_MachineCard.cshtml` to the design system with **no logic change**; reserve fixed widths for dynamic metric values so the ~5 s poll causes **no layout shift** (spec FR-016, SC-007)
- [X] T027 [P] [US2] Restyle `src/BetaPlatform/Views/Auth/Login.cshtml` to the refreshed visual language; verify it renders correctly in both themes (login is pre-auth â€” confirm theme default/cookie behavior there)

**Checkpoint**: Design system + theme toggle live across chrome, dashboard, and login; US1 Create
pages automatically pick up both themes. US1 + US2 both demoable independently.

---

## Phase 5: User Story 3 - UI Defect Cleanup & Consistency Pass (Priority: P3)

**Goal**: Finish the remaining screens â€” styled empty states, Edit/Details parity with Create, list
pages, toasts â€” and run the RTL/responsive/long-content defect audit so the whole app feels finished.

**Independent Test**: Empty each list â†’ a styled empty state with a primary "add" action (no bare
table); Edit and Details pages match Create's modern styling; every touched screen is clean in AR-RTL
and on a narrow viewport (no clipping/overlap/horizontal scroll). (Ref: spec US3.)

### Implementation for User Story 3

- [X] T028 [US3] Implement `src/BetaPlatform/Views/Shared/_EmptyState.cshtml`: centered icon + localized message + primary action slot; RTL-correct; used by empty Index views (`contracts/design-system.md` Â§3 Empty state)
- [X] T029 [US3] Add US3 localization keys to `SharedResource.en.resx`/`.ar.resx`: empty-state messages per list (no machines/products/work orders yet) and any Details/Edit labels
- [X] T030 [P] [US3] Modernize `src/BetaPlatform/Views/Machines/Index.cshtml`: design-system table/cards, page header + primary action, and `_EmptyState` when empty
- [X] T031 [P] [US3] Modernize `src/BetaPlatform/Views/Products/Index.cshtml`: same treatment + `_EmptyState`
- [X] T032 [P] [US3] Modernize `src/BetaPlatform/Views/WorkOrders/Index.cshtml`: same treatment + `_EmptyState`
- [X] T033 [P] [US3] Bring `src/BetaPlatform/Views/Machines/Edit.cshtml` to Create parity (reuse the inline `.form-layout` scaffold + `form-preview.js`)
- [X] T034 [P] [US3] Bring `src/BetaPlatform/Views/Products/Edit.cshtml` to Create parity
- [X] T035 [P] [US3] Bring `src/BetaPlatform/Views/WorkOrders/Edit.cshtml` to Create parity
- [X] T036 [P] [US3] Restyle `src/BetaPlatform/Views/WorkOrders/Details.cshtml` (read-only) to visual parity with the rest (no live preview needed) (spec FR-006)
- [X] T037 [US3] Style TempData success/error as elevated **glass toasts** with an ARIA live region in `_Layout.cshtml` (sequential â€” same file) (`contracts/design-system.md` Â§3 Toasts)
- [X] T038 [US3] RTL audit & fixes in `site.css` (sequential â€” same file): walk every touched screen in Arabic; fix mirrored two-column layout, directional icons, spacing/overflow (spec FR-011)
- [X] T039 [US3] Responsive audit & fixes in `site.css` (sequential â€” same file): narrow-viewport column stacking, table scroll-in-container, navbar collapse, **no page-level horizontal scroll** (spec FR-012)
- [X] T040 [US3] Long-content handling in `site.css` (sequential â€” same file): graceful wrap/truncate for long machine/product/work-order names & codes in cards, tables, and the preview panel (spec Edge Cases)

**Checkpoint**: All authenticated screens are consistent, defect-free in RTL and narrow widths, with
styled empty states and Edit/Details parity. All three stories independently functional.

---

## Phase 6: Polish & Cross-Cutting Concerns

**Purpose**: Verify the acceptance matrix and non-functional guarantees; no new behavior.

- [ ] T041 WCAG 2.1 AA verification pass in **both themes** per `quickstart.md` A1â€“A5 (contrast â‰¥4.5:1 text / â‰¥3:1 UI, visible focus, full keyboard operability incl. toggle & language switcher in LTR+RTL, labeled controls); fix any gaps in `site.css`/views (spec FR-014, SC-006)
- [X] T042 Dashboard polling regression check per `quickstart.md` V9: confirm the ~5 s refresh updates cards with **no flicker or layout shift** and the `/Dashboard/Data` contract/cadence is unchanged (spec FR-016, SC-007)
- [ ] T043 Full cross-matrix visual validation per `quickstart.md` V1â€“V12: {EN-LTR, AR-RTL} Ã— {dark, light} Ã— {wide, narrow}; record pass/fail and remediate
- [X] T044 Regression guard: run `dotnet test` (existing service tests must be green, unchanged) and confirm **no new EF migration** was added under `src/BetaPlatform/Migrations/` and the navbar tab set is unchanged (spec FR-016, SC-005, SC-007)
- [X] T045 [P] Confirm `asp-append-version` cache-busting is present on all changed `site.css`/`theme.js`/`form-preview.js` references so users receive the refreshed assets
- [ ] T046 Capture "after" reference screens for each major page and obtain stakeholder sign-off that the UI reads as modern/professional (spec SC-008)

---

## Dependencies & Execution Order

### Phase Dependencies

- **Setup (Phase 1)**: No dependencies â€” start immediately.
- **Foundational (Phase 2)**: Depends on Setup â€” **BLOCKS all user stories** (all rely on the shared token/component layer).
- **User Stories (Phase 3â€“5)**: All depend on Foundational. In priority order P1 â†’ P2 â†’ P3.
- **Polish (Phase 6)**: Depends on the user stories you intend to ship.

### User Story Dependencies

- **US1 (P1)**: Depends only on Foundational. Delivers the MVP (Create pages). Independently testable.
- **US2 (P2)**: Depends only on Foundational. Independently testable. (Its light-theme tokens also
  make US1's pages themeable, but US1 is complete and testable in dark before US2 exists.)
- **US3 (P3)**: Depends on Foundational. Reuses US1's inline `.form-layout` markup + `form-preview.js`
  for Edit-page parity (T033â€“T035) â€” so run those after US1. The rest of US3 depends only on Foundational.

### Shared-file ordering (within/across stories)

- `site.css` tasks are globally sequential in this order: T006â†’T007â†’T008â†’T009â†’T010 (Foundational) â†’
  T014 (US1) â†’ T019, T025 (US2) â†’ T037â€“T040 (US3). Never parallelize them.
- `_Layout.cshtml` tasks are sequential: T005 (Setup) â†’ T021, T024 (US2) â†’ T037 (US3).

### Parallel Opportunities

- **Setup**: T002, T003, T004 in parallel (different new files).
- **US1**: T015, T016, T017 in parallel (three different Create views) after T012â€“T014 exist.
- **US2**: T026, T027 in parallel (Dashboard vs Login â€” different files).
- **US3**: T030â€“T032 (Index views) and T033â€“T036 (Edit/Details views) in parallel â€” all different files â€” after `_EmptyState` and US1's inline `.form-layout`/`form-preview.js` pattern exist.
- Across teams: once Foundational is done, one dev takes US1, another US2; US3 starts after US1's form partial lands.

---

## Parallel Example: User Story 1

```bash
# After T012 (inline .form-layout scaffold), T013 (form-preview.js), T014 (CSS) are in place,
# rework the three Create views in parallel (different files):
Task: "Rework Views/Machines/Create.cshtml using the inline .form-layout scaffold (T015)"
Task: "Rework Views/Products/Create.cshtml using the inline .form-layout scaffold (T016)"
Task: "Rework Views/WorkOrders/Create.cshtml using the inline .form-layout scaffold (T017)"
```

---

## Implementation Strategy

### MVP First (User Story 1 only)

1. Phase 1 Setup â†’ 2. Phase 2 Foundational (CRITICAL, blocks everything) â†’ 3. Phase 3 US1.
4. **STOP & VALIDATE**: exercise all three Create pages in EN & AR (dark). 5. Demo the MVP.

### Incremental Delivery

1. Setup + Foundational â†’ the whole app already looks modern in dark.
2. US1 â†’ modern Create pages with live preview (**MVP**) â†’ demo.
3. US2 â†’ full design system + light/dark toggle across chrome/dashboard/login â†’ demo.
4. US3 â†’ empty states, Edit/Details parity, RTL/responsive polish â†’ demo.
5. Polish â†’ WCAG AA + regression matrix + sign-off.

### Notes

- No automated UI tests were requested; validation is the `quickstart.md` matrix (Phase 6).
- This feature adds **no EF migration** and **no controller/route changes** â€” if either appears,
  scope has leaked (reject). Keep controllers' action surface identical.
- Commit after each task or logical group; `site.css` and `_Layout.cshtml` are coordination points â€”
  sequence edits to avoid conflicts.
