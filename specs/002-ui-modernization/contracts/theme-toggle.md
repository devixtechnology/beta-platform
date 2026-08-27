# Contract: Theme Toggle & Persistence

**Feature**: 002-ui-modernization | Presentation contract (no HTTP API)

Implements the clarified light/dark theme toggle (remembered choice) with **no page reload** and
**no flash of the wrong theme** on load. As implemented, the default when no cookie is present is the
**light** theme (see §2).

## 1. Cookie contract

| Property | Value |
|----------|-------|
| Name | `beta_theme` |
| Values | `dark` \| `light` |
| Default when absent | `light` |
| Expiry | 1 year (refreshed on each change) |
| Flags | `IsEssential = true`, `Path=/`, `SameSite=Lax` |
| Written by | `theme.js` (client) on toggle |
| Read by | `_Layout.cshtml` (server) at render time |

The cookie is independent of the culture cookie. No controller/endpoint is required (JS sets the
cookie directly); no server round-trip on toggle.

## 2. Rendering contract (`_Layout.cshtml`)

1. On the server, read `beta_theme` from `Request.Cookies`; render `dark` only when the cookie value
   is exactly `"dark"`, otherwise `light` (so missing/invalid ⇒ **light**). As coded:
   `var theme = Context.Request.Cookies["beta_theme"] == "dark" ? "dark" : "light";`
2. Stamp the resolved theme on the root element **before first paint**:
   `<html data-bs-theme="@theme" ...>`. (No separate `data-theme` hook is used — the CSS keys off
   `data-bs-theme` only.)
3. This guarantees the first paint already matches the saved theme → **no FOUC**.
4. `dir` (LTR/RTL) continues to be driven by culture, orthogonally to theme.

## 3. Toggle behavior contract (`theme.js`)

1. Render a labeled toggle control in the navbar (partial `_ThemeToggle.cshtml`) — accessible name
   (e.g., `aria-label="Toggle light/dark theme"`), reflecting current state, keyboard-operable.
2. On activate:
   - Compute next theme (`dark` ↔ `light`).
   - Set the root `data-bs-theme` attribute **immediately** → whole UI reskins instantly via CSS
     variables (no reload, < 100 ms perceived).
   - Write the `beta_theme` cookie per §1 so the choice persists.
   - Update the toggle's own visual + `aria` state.
3. No layout shift; the toggle must be operable in both LTR and RTL and in both themes.

## 4. Acceptance (maps to spec)

- FR-009a / SC-009: toggle available on every page; applies immediately; persists across sessions;
  first-time user sees **light**.
- FR-011 / SC-002: every screen renders correctly in both themes (× LTR/RTL × widths).
- FR-014 / SC-006: toggle is labeled, keyboard-operable, and has a visible focus state; both themes
  meet AA contrast.

## 5. Non-goals

- No per-user DB-persisted theme (cookie only — Principle III).
- No auto-switch from OS `prefers-color-scheme` (explicit user control with **light** default). *(May
  be used only as the value when no cookie exists in a future iteration; out of scope now.)*
