# Implementation Plan: UI Modernization & Enhanced Creation Pages

**Branch**: `002-ui-modernization` | **Date**: 2026-07-08 | **Spec**: [spec.md](./spec.md)

**Input**: Feature specification from `/specs/002-ui-modernization/spec.md`

## Summary

A **presentation-only** refresh of the existing Beta Platform Phase-1 UI. It delivers four things:
(1) reworked **creation pages** for Machine, Product, and Work Order into a modern **side-by-side
layout** (input fields on one side, a **live preview** + contextual help on the other); (2) an
**"elevated depth + glass"** visual design system (layered shadows, subtle gradients, frosted card
surfaces, refined type/spacing) applied consistently across every authenticated screen; (3) a
**light/dark theme toggle** (light default as implemented, choice remembered) while preserving the existing four
navbar tabs and the Beta orange/charcoal brand; and (4) a **defect & consistency pass** (empty
states, RTL, responsiveness, edit/detail parity) meeting **WCAG 2.1 AA**.

No new data entities, routes, business logic, or database schema are introduced. All work lives in
the **view/presentation layer**: `Views/**`, `wwwroot/css/site.css`, new small vanilla-JS files
(`theme.js`, `form-preview.js`), the shared `_Layout.cshtml`, and view models used only to shape the
creation pages. The theme preference is persisted with a **cookie** (mirroring the existing culture
cookie), so **no EF Core migration** is required — consistent with Principle III (YAGNI).

## Technical Context

**Language/Version**: C# on **.NET 9** (ASP.NET Core MVC / Razor views); CSS3 (custom properties);
vanilla ES2019 JavaScript. No TypeScript, no new build/bundler tooling.

**Primary Dependencies**: Existing only — **Bootstrap 5** (LTR + RTL builds, served locally),
**Bootstrap Icons**, **jQuery + jQuery-validation-unobtrusive** (already used for client validation).
Theming via native CSS custom properties + Bootstrap's `data-bs-theme`. **No new packages.**

**Storage**: None new. Theme preference persisted via an essential **cookie** (`beta_theme`), read
server-side in `_Layout.cshtml` to set the initial theme (no flash of wrong theme); when the cookie
is absent the layout renders the **light** theme by default (`data-bs-theme="light"`). The existing
culture cookie pattern (`CultureController`) is the template, though theme needs no controller — the
cookie is written client-side by `theme.js`. **No database schema change → no EF Core migration.**

**Testing**: No service/business logic changes, so existing xUnit service tests are unaffected and
require no edits. Validation is primarily **manual/visual** per `quickstart.md`: cross-matrix of
LTR/RTL × light/dark × wide/narrow, plus WCAG 2.1 AA checks (contrast, focus, keyboard) using
browser devtools / an accessibility auditor. A lightweight smoke check confirms every route renders.

**Target Platform**: Modern evergreen browsers (Chromium, Firefox, Safari) on desktop, tablet, and
phone widths; both `dir="ltr"` (English) and `dir="rtl"` (Arabic); both light and dark themes.

**Project Type**: Web application — the existing single monolithic ASP.NET Core MVC project
(`src/BetaPlatform`). This feature touches only its presentation layer.

**Performance Goals**: Theme toggle applies **instantly with no page reload and no layout shift**
(< 100 ms perceived). The dashboard's existing ~5-second polling refresh must continue with **no
flicker or layout shift** introduced by the restyle. Live-preview updates on the creation pages are
instantaneous on input (client-side only, no network call).

**Constraints**: Presentation-only — **no** changes to routes, controllers' action surface, business
rules, entities, or telemetry (FR-016). The four navbar tabs (Dashboard, Machines, Products, Work
Orders) and their order are **fixed** (FR-008). Beta brand (logo + orange) retained; the **light**
theme is the default for a first-time user, with charcoal dark as the alternate (FR-009/009a).
Bilingual AR/EN + RTL parity on every touched screen
(FR-011). WCAG 2.1 AA (FR-014). No SignalR; no new backend endpoints.

**Scale/Scope**: ~12 authenticated Razor views restyled; 3 creation pages reworked into the
side-by-side + live-preview layout (Edit/Details brought to parity); 1 shared `_Layout.cshtml`;
1 `site.css` overhaul (dual-theme token sets); 2 new small JS files (`theme.js`, `form-preview.js`).
As built, **no** per-page creation view models were needed — the create/edit views bind directly to
the existing entities with a client-side declarative preview. No new controllers, entities, or
migrations.

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-checked after Phase 1 design.*

| Principle | Status | How the design complies |
|-----------|--------|-------------------------|
| I. Monolithic Architecture | ✅ PASS | All changes stay inside the single `src/BetaPlatform` MVC project (views, wwwroot, layout). No new processes, services, or network hops. |
| II. SOLID | ✅ PASS | No new server abstractions introduced. Any creation view models are simple DTOs shaping a view (Single Responsibility). Theme reading is a thin layout concern, not a new subsystem. |
| III. Simplicity First (YAGNI) | ✅ PASS | Theme preference stored in a **cookie**, not a new DB table/entity/migration — the least machinery that works. No CSS build pipeline, no component framework, no SPA — plain CSS custom properties + vanilla JS. Live preview is progressive-enhancement JS, no server round-trips. |
| IV. EF Core Migrations for All Schema | ✅ PASS (N/A) | **No schema change** — this is presentation-only, so no migration is needed and none is hand-written. No raw SQL added. |
| V. MVC Separation of Concerns | ✅ PASS | Views remain logic-free (only presentation + client-side preview JS). No business logic added to controllers or views; controllers' action surface is unchanged. Theme state is read in the layout only for rendering. |

**Result**: PASS (initial). **No violations → Complexity Tracking is empty/N/A.** Re-evaluated after
Phase 1 design: still PASS (see end of plan).

## Project Structure

### Documentation (this feature)

```text
specs/002-ui-modernization/
├── plan.md              # This file
├── research.md          # Phase 0 output — design-system & theming decisions
├── data-model.md        # Phase 1 output — view models + theme cookie (no DB entities)
├── quickstart.md        # Phase 1 output — validation matrix & steps
├── contracts/           # Phase 1 output — UI contracts (no HTTP API)
│   ├── design-system.md         # Tokens, component specs, both themes
│   ├── creation-pages.md        # Side-by-side layout + live-preview field maps
│   └── theme-toggle.md          # Theme cookie + toggle behavior contract
├── checklists/
│   └── requirements.md  # Spec quality checklist (pre-existing, 16/16)
└── tasks.md             # Phase 2 output (/speckit-tasks — NOT created here)
```

### Source Code (repository root)

Only presentation-layer files are added or modified in the existing project:

```text
src/BetaPlatform/
├── Views/
│   ├── Shared/
│   │   ├── _Layout.cshtml            # MODIFY: dual-theme attr from cookie (light default), theme
│   │   │                             #         toggle in navbar, modernized navbar, TempData toasts
│   │   ├── _ThemeToggle.cshtml       # NEW (partial): light/dark switch control (sun/moon icons)
│   │   └── _EmptyState.cshtml        # NEW (partial): reusable styled empty state
│   │                                 # NOTE: no `_FormLayout.cshtml` partial was built — the
│   │                                 #       side-by-side scaffold is inlined per view via the
│   │                                 #       `.form-layout`/`.form-pane`/`.preview-pane` CSS classes.
│   ├── Machines/{Create,Edit,Details,Index}.cshtml   # MODIFY: side-by-side + preview; Details parity; empty state
│   ├── Products/{Create,Edit,Index}.cshtml           # MODIFY: side-by-side + preview; empty state
│   ├── WorkOrders/{Create,Edit,Details,Index}.cshtml # MODIFY: same treatment + detail parity
│   ├── Dashboard/{Index,_MachineCard}.cshtml         # MODIFY: restyle to new design system (no logic)
│   └── Auth/Login.cshtml                              # MODIFY: align to refreshed visual language
├── ViewModels/                        # UNCHANGED for create/edit: the create/edit views bind
│                                       #   directly to the entities (Machine/Product/WorkOrder) with
│                                       #   ViewBag select lists — no CreateXxxVm was needed. The only
│                                       #   view model in play is the pre-existing MachineDetailsViewModel.
└── wwwroot/
    ├── css/site.css                   # OVERHAUL: two token sets (light/dark), glass/depth
    │                                   #   component styles, WCAG-AA contrast, RTL parity
    └── js/
        ├── theme.js                   # NEW: read/write beta_theme cookie, toggle attr, no reload
        └── form-preview.js            # NEW: declarative live preview via data-preview-* attributes
                                       #      + submit guard on form[data-live-form] (progressive enh.)
```

**Structure Decision**: Reuse the existing monolithic `src/BetaPlatform` project untouched at the
architecture level; confine every change to `Views/**` and `wwwroot/**`. This honors the
constitution's monolith + separation mandate and keeps the refresh a pure presentation concern. As
built, two new reusable Razor partials (`_ThemeToggle`, `_EmptyState`) and two small JS modules keep
the change DRY without introducing a framework or build step. The originally-planned `_FormLayout`
partial and per-page create view models proved unnecessary: the side-by-side layout is expressed as
CSS grid classes reused across views, and the views bind straight to the entities — the simplest
thing that works (Principle III).

## Complexity Tracking

No constitution violations — this section is intentionally empty.

## Phase outputs

- **Phase 0** → [research.md](./research.md) — theming strategy, glass/depth technique, side-by-side
  + live-preview approach, empty-state & accessibility decisions. No `NEEDS CLARIFICATION` remain
  (the four open questions were resolved in `/speckit-clarify`).
- **Phase 1** → [data-model.md](./data-model.md), [contracts/](./contracts/),
  [quickstart.md](./quickstart.md), and the agent-context update (CLAUDE.md).
- **Phase 2** → `tasks.md` is generated by `/speckit-tasks` (not by this command).
