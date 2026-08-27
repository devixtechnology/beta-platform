# Phase 0 Research & Decisions: UI Modernization

**Feature**: 002-ui-modernization | **Date**: 2026-07-08

All four `NEEDS CLARIFICATION` items were resolved in `/speckit-clarify` (see spec §Clarifications).
This document records the *technical* decisions that implement those clarified choices, plus the
supporting research. No open unknowns remain.

---

## D1 — Theming strategy: dual CSS custom-property token sets + `data-bs-theme`

**Decision**: Keep a single `site.css`. Define **two token sets** as CSS custom properties — the dark
palette under `:root, [data-bs-theme="dark"]` and a `[data-bs-theme="light"]` override block. Every
component reads tokens (`var(--card-bg)`, `var(--text-primary)`, …), never hard-coded colors, so
switching the root attribute reskins the whole app with zero per-component JS. **As implemented**,
`_Layout` stamps `data-bs-theme="light"` when no `beta_theme` cookie is present, so **light is the
rendered default**; the dark set remains the `:root` baseline and the alternate theme.
Piggyback on Bootstrap 5's native `data-bs-theme` so Bootstrap's own components (dropdowns, forms,
tables) theme correctly in both modes.

**Rationale**: The current CSS already centralizes color in `:root` variables — this is a natural,
low-risk extension. No build step, no second stylesheet, no FOUC when the initial attribute is
server-rendered. Bootstrap 5.3+ ships first-class light/dark support via `data-bs-theme`, so we
inherit correct component theming for free.

**Alternatives considered**:
- *Separate `light.css`/`dark.css` files* — rejected: duplicate maintenance, extra request, and the
  existing single-file approach already works.
- *A DB-persisted per-user theme setting* — rejected (YAGNI, Principle III): a cookie is sufficient
  for a UI preference and avoids a migration/entity.
- *CSS `prefers-color-scheme` only (no toggle)* — rejected: the clarification requires an explicit
  user toggle with a remembered choice and a fixed default (implemented as **light**).

## D2 — Theme persistence: essential cookie, mirroring the culture cookie

**Decision**: Persist the choice in an **essential cookie** `beta_theme` = `dark` | `light`
(1-year expiry), written client-side by `theme.js` when the toggle is used, and **read server-side
in `_Layout.cshtml`** to stamp the initial `data-bs-theme` attribute on `<html>` before first paint.
When the cookie is absent the layout renders **light** (`beta_theme == "dark" ? "dark" : "light"`).
The toggle also flips the attribute immediately in JS so there is **no reload**.

**Rationale**: Directly mirrors the established `CultureController` cookie pattern, so it's familiar
and consistent. Server-side read eliminates the flash-of-wrong-theme that a pure-`localStorage`
approach causes on first paint. No new controller/endpoint is required because JS can set the cookie
directly; a POST-to-controller round trip (like culture) is unnecessary and would force a reload.

**Alternatives considered**:
- *`localStorage` only* — rejected: causes a dark→light flash on load because the server renders
  before JS runs.
- *New `ThemeController` POST + redirect* (exact culture clone) — rejected: forces a full reload for
  a purely visual change; the cookie-from-JS approach is simpler and instant.

## D3 — "Elevated depth + glass" visual language (the clarified design direction)

**Decision**: Build the modern look from a small set of reusable techniques layered on the existing
components:
- **Depth**: multi-layer soft box-shadows (ambient + key), subtle 1px gradient/hairline borders,
  and a restrained elevation scale (`--elev-1/2/3`) instead of the current flat single shadow.
- **Glass**: frosted surfaces for elevated chrome (card headers, navbar, modals, the preview panel)
  via `backdrop-filter: blur()` + a semi-transparent token background, **with an opaque fallback**
  where `backdrop-filter` is unsupported.
- **Gradients**: keep the existing orange gradient accents (buttons, KPI tiles) and extend a subtle
  surface gradient to cards; use gradients purposefully for emphasis/status, not everywhere.
- **Type & rhythm**: tighten the type scale and standardize a spacing rhythm (consistent card
  padding, section gaps, page headers) so all screens feel like one product.

**Rationale**: This is the "modern, low-risk evolution" the user chose over a flat-minimal or
bold-expressive direction. It reuses the brand's existing gradient/orange motifs and the current
card/badge/button classes, so it elevates rather than rewrites, minimizing regression risk.

**Alternatives considered**: Flat-minimal and bold-expressive directions — rejected per the
clarification (user chose elevated depth + glass).

**Accessibility guard**: Glass/blur and gradients must not drop text contrast below WCAG AA
(see D6). Contrast is validated against the *rendered* surface, not the token in isolation.

## D4 — Creation pages: single-page side-by-side form + live preview (the clarified layout)

**Decision**: Each create/edit page uses a responsive two-column layout:
- **Primary column** — the form, with fields grouped into labeled sections, required-field markers,
  inline helper text, and clear primary (Save) / secondary (Cancel) actions.
- **Secondary column** — a **live preview** that renders the record as it will appear (e.g., a
  Machine card / Product card / Work Order summary) plus contextual help, updated on every input via
  `form-preview.js` (client-side only, no network).
- On narrow viewports the two columns **stack** (preview below the form) so the page stays usable.

The live preview is **progressive enhancement**: with JS disabled the form still fully works and the
preview panel simply shows a static placeholder/help. Server-side model validation is unchanged and
remains the source of truth; client validation (existing jQuery-unobtrusive) gives inline feedback.

**Rationale**: Implements the clarified "side-by-side panel" choice. Reusing the existing dashboard
`_MachineCard` visual as the machine preview keeps effort low and ties the create experience to what
the user will see on the dashboard. Progressive enhancement keeps it robust and avoids coupling data
entry to JS availability.

**Per-entity preview field maps** are specified in `contracts/creation-pages.md`.

**Alternatives considered**: Multi-step wizard and single-page grouped (no preview) — rejected per
the clarification.

## D5 — Empty states, consistency & defect pass

**Decision**: Introduce a reusable `_EmptyState.cshtml` partial (icon + message + primary action)
used by every Index view when its collection is empty. Standardize page headers (title + primary
action placement), spacing, table styling, and badge usage across all views. Bring Edit/Details
pages to visual parity with Create. Catalogue concrete defects during implementation (US3 audit):
RTL mirroring of the new two-column layout and glass chrome, long-content wrapping/truncation in
cards and tables, small-screen reflow, and toast/alert styling.

**Rationale**: FR-013/FR-015 require styled empty states and a consistency pass. A shared partial
keeps them uniform and DRY.

**Alternatives considered**: Ad-hoc per-page empty markup — rejected (inconsistent, non-DRY).

## D6 — Accessibility: WCAG 2.1 AA (the clarified bar)

**Decision**: Target and verify **WCAG 2.1 AA** in both themes: text contrast ≥ 4.5:1 and
UI-component/graphical contrast ≥ 3:1; a **visible focus indicator** on every interactive element
(a token-based focus ring, not `outline:none`); full keyboard operability (toggle, dropdowns, form,
actions reachable and operable by keyboard); and correctly associated labels/`aria` on controls
(including the theme toggle and language switcher). Validate with browser accessibility tooling and
a manual keyboard pass across the component set.

**Rationale**: Implements the clarified AA choice and makes SC-006 objectively testable. Contrast
must be checked in *both* themes because the light theme is new and easy to get wrong.

**Alternatives considered**: Best-effort/no formal standard — rejected per the clarification.

## D7 — Dashboard polling must survive the restyle

**Decision**: The restyle changes only classes/markup that `dashboard.js` does not depend on for its
DOM patching; verify the ~5 s poll still updates cards with **no flicker or layout shift**. Reserve
space for dynamic values (fixed metric widths / skeleton) so theme and glass effects don't cause
reflow on refresh. No change to the `/Dashboard/Data` contract or polling cadence.

**Rationale**: FR-016 forbids altering existing behavior; the dashboard is the most dynamic screen
and the most likely to regress visually under a restyle.

**Alternatives considered**: None needed — this is a constraint, not a choice.

---

## Summary of resolved decisions

| # | Topic | Decision |
|---|-------|----------|
| D1 | Theming mechanism | Dual CSS-variable token sets under `data-bs-theme`, single `site.css` |
| D2 | Theme persistence | Essential `beta_theme` cookie; JS writes + instant toggle; layout reads for no-flash; **light default** when cookie absent |
| D3 | Visual language | Elevated depth + glass (shadows, hairline/gradient borders, `backdrop-filter` w/ fallback) |
| D4 | Creation pages | Single-page side-by-side form + client-side live preview; progressive enhancement; stacks on mobile |
| D5 | Consistency/defects | Reusable empty-state & form-layout partials; Edit/Details parity; audited defect pass |
| D6 | Accessibility | WCAG 2.1 AA in both themes (contrast, visible focus, keyboard, labels) |
| D7 | Dashboard | Restyle only; no flicker/layout-shift; polling & data contract unchanged |

No `NEEDS CLARIFICATION` remain. Ready for Phase 1 design artifacts.
