# Contract: Design System (Tokens & Components)

**Feature**: 002-ui-modernization | Presentation contract (no HTTP API)

This is the visual contract every authenticated screen MUST honor. It defines the token surface and
the component specs for the "elevated depth + glass" language, in **both** the dark and light themes
(light is the rendered default; see §1). Exact color values are tuned during implementation to meet WCAG AA (see
`../research.md` D6); the **contract is the token names, the elevation/blur system, and the
per-component rules** — not the precise hex codes.

## 1. Token surface (CSS custom properties)

All components MUST read these tokens; no component may hard-code a themeable color. Two sets are
defined: the dark palette on `:root, [data-bs-theme="dark"]` and the light palette on
`[data-bs-theme="light"]`. **As implemented**, `_Layout` stamps `data-bs-theme="light"` when no
`beta_theme` cookie is present, so **light is the rendered default** for a first-time user (dark is
the `:root` baseline and the alternate theme).

| Token | Meaning | Dark (`:root` baseline) | Light (rendered default) |
|-------|---------|----------------|-------|
| `--bg` | App background | deep charcoal (`#0f172a`) | near-white (`#f5f7fa`) |
| `--surface` | Card/base surface | `#1e293b` | `#ffffff` |
| `--surface-glass` | Frosted chrome bg (semi-transparent) | `rgba(30,41,59,.6)` | `rgba(255,255,255,.6)` |
| `--border` | Hairline border | `#334155` | `#e2e8f0` |
| `--text` | Primary text | `#f8fafc` | `#0f172a` |
| `--text-muted` | Secondary text | `#94a3b8` | `#475569` |
| `--primary` / `--primary-dark` / `--primary-darker` | Beta orange ramp | `#f7941e` / `#e07d0c` / `#c2680a` | same brand ramp |
| `--success` / `--warning` / `--danger` / `--info` | Status colors | existing values | AA-adjusted for light |
| `--elev-1/2/3` | Elevation shadow stack | soft multi-layer shadows | lighter, tighter shadows |
| `--focus-ring` | Focus outline | `0 0 0 .2rem rgba(247,148,30,.45)` | AA-adjusted |
| `--radius` / `--radius-lg` | Corner radii | `8px` / `14px` | same |

> The dark values above are the current palette (backward compatible). Light values are new and MUST
> be contrast-verified. Brand orange is identical in both themes (FR-009).

## 2. Elevation & glass system

- **Elevation scale**: `--elev-1` (resting cards), `--elev-2` (hover / preview panel / dropdowns),
  `--elev-3` (modals / toasts). Each is a **multi-layer** shadow (ambient + key), not a single flat
  shadow.
- **Glass surfaces**: navbar, card headers, the creation preview panel, modals, and toasts use
  `background: var(--surface-glass)` + `backdrop-filter: blur(10px)`. A `@supports not
  (backdrop-filter: blur(1px))` fallback MUST set an **opaque** `--surface` background so text stays
  legible where blur is unsupported.
- **Borders**: 1px hairline `--border` plus an optional subtle gradient top-border on elevated cards
  for the "crafted" feel. Never rely on shadow alone for card separation in the light theme.

## 3. Component rules (apply in both themes)

| Component | Contract |
|-----------|----------|
| **Navbar** | Keeps the 4 tabs + order (FR-008). Glass background, sticky/fixed-top. Active tab: orange accent + underline/indicator; hover: orange text + subtle bg. Contains language switcher, **theme toggle**, user menu. |
| **Cards** | `--surface`, `--radius-lg`, `--elev-1`, hairline border; header may be glass. Hover lifts to `--elev-2` (existing behavior, refined). |
| **Buttons** | Primary = orange gradient + glow-on-hover (existing). Secondary = outline. All have visible `:focus-visible` ring using `--focus-ring`. Disabled/busy state for in-flight saves (FR-004). |
| **Forms** | Inputs use `--surface`-derived bg, `--border`, orange focus ring. Required fields marked (e.g., asterisk + `aria-required`). Inline validation messages in `--danger` with adequate contrast. |
| **Tables** | Themed via tokens (replace hard `table-dark` dependence). Hover row tint uses orange at low alpha. Responsive: horizontal scroll container on narrow widths, never page-level overflow. |
| **Badges / status** | Running/Stopped/Warning/Idle pills reuse status tokens; AA contrast in both themes. |
| **KPI tiles** | Gradient accent retained; value has reserved width to avoid layout shift on dashboard poll. |
| **Empty state** | `_EmptyState` partial: centered icon + message + primary action. Used by every empty Index. |
| **Toasts/alerts** | TempData success/error styled as elevated glass toasts; dismissible; ARIA live region. |

## 4. Accessibility (WCAG 2.1 AA) — normative

- Text contrast ≥ **4.5:1**; UI-component & graphical-object contrast ≥ **3:1** — verified against
  the **rendered** surface (including glass/gradient) in **both** themes.
- Every interactive element exposes a **visible focus indicator** (`:focus-visible`), never
  `outline:none` without a replacement.
- Full **keyboard** operability: tab order logical in LTR and RTL; toggle, dropdowns, form controls,
  and actions all keyboard-reachable and operable.
- Controls are **labeled** (`<label>`/`aria-label`), including the theme toggle and language
  switcher. Icon-only controls have accessible names.

## 5. Bilingual / RTL — normative

- Every rule above holds in `dir="rtl"` (Arabic): mirrored spacing, directional icons flipped where
  semantically directional, two-column create layout mirrored, no clipped/overlapping elements.
- Logical properties or existing RTL overrides keep margins/padding correct in both directions.

## 6. Responsiveness — normative

- No page-level horizontal scroll at any supported width.
- Create pages' two columns stack on narrow viewports (preview below form).
- Navbar collapses to the existing toggler; tables scroll within their own container.
