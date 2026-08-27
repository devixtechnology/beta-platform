# Quickstart & Validation Guide: UI Modernization

**Feature**: 002-ui-modernization | **Date**: 2026-07-08

How to run the app and validate the UI refresh end-to-end. This feature is presentation-only, so
validation is primarily **visual and interactive** across a defined matrix, plus WCAG 2.1 AA checks.
No new automated tests are required; existing service unit tests must still pass unchanged.

## Prerequisites

- .NET 9 SDK; MySQL 8.0 reachable per `appsettings.json` `ConnectionStrings:DefaultConnection`
  (same as feature 001 — no schema change, **no new migration** for this feature).
- A browser with devtools accessibility auditing (Chromium Lighthouse / axe, or Firefox).
- Seed/admin login from feature 001 to reach authenticated pages.

## Run

```powershell
# from repo root
dotnet build BetaPlatform.sln
dotnet run --project src/BetaPlatform
# browse to the shown localhost URL and sign in as the admin
```

> No `dotnet ef database update` step is needed for this feature — it adds no schema.

## Validation matrix

Run each check in **every** cell of: **{English LTR, Arabic RTL} × {Dark, Light} × {Wide desktop,
Narrow phone}**. Use the language switcher for AR/EN and the new navbar theme toggle for dark/light.

| # | Area | What to verify | Spec ref |
|---|------|----------------|----------|
| V1 | Machine Create | Side-by-side form + live preview; preview updates as you type/select; required markers; inline validation on empty submit **without losing input**; success toast → redirect to Index | US1, FR-001..005, contracts/creation-pages.md |
| V2 | Product Create | Same as V1 for product fields | US1, FR-001..005 |
| V3 | Work Order Create | Same as V1; selected product/machine names shown in preview (not ids) | US1, FR-001a |
| V4 | Edit pages | Machine/Product/Work Order Edit match Create's modern layout | FR-006 |
| V5 | Details (Work Order) | Read-only detail styled to parity with the rest | FR-006 |
| V6 | Theme toggle | Toggle in navbar flips whole UI **instantly, no reload**; reload page → theme **persisted**; fresh browser (no cookie) → **light** default | FR-009a, SC-009, contracts/theme-toggle.md |
| V7 | Navbar | Exactly the 4 tabs (Dashboard, Machines, Products, Work Orders) in the same order; modern active/hover states | FR-008, SC-005 |
| V8 | Empty states | Empty each Index (or view with no data) → styled empty state with primary "add" action; no bare table | FR-013, SC-004 |
| V9 | Dashboard | Restyled cards; ~5 s polling still refreshes with **no flicker or layout shift** | FR-016, SC-007 |
| V10 | Design consistency | Cards/buttons/tables/badges share the elevated depth+glass style; consistent headers/spacing across sections | US2, FR-007/010 |
| V11 | Responsive | No page-level horizontal scroll; create columns stack; tables scroll in-container; navbar collapses | FR-012, SC-002 |
| V12 | RTL | Arabic mirrors correctly: two-column layout, glass chrome, directional icons, spacing — no clipping/overlap | FR-011, SC-002 |

## Accessibility checks (WCAG 2.1 AA) — both themes

| # | Check | Pass bar | Spec ref |
|---|-------|----------|----------|
| A1 | Text contrast | ≥ 4.5:1 against rendered surface (incl. glass/gradient) | FR-014, SC-006 |
| A2 | UI/graphical contrast | ≥ 3:1 (borders, icons, focus rings, status pills) | FR-014 |
| A3 | Visible focus | Every interactive element shows a focus ring on keyboard focus | FR-014, SC-006 |
| A4 | Keyboard operability | Toggle, language switcher, dropdowns, forms, actions all reachable & operable by keyboard in LTR & RTL | FR-014 |
| A5 | Labels | Theme toggle, language switcher, and icon-only controls have accessible names | FR-014 |

Run an automated audit (Lighthouse/axe) per theme as a first pass, then a **manual keyboard walk**
(automated tools miss focus-order and operability issues).

## Regression guard (no functional change — FR-016 / SC-007)

- All existing create/edit/list/dashboard workflows behave exactly as before.
- `/Dashboard/Data` polling cadence and JSON contract unchanged.
- Existing service unit tests pass without modification:
  ```powershell
  dotnet test
  ```
- No new EF migration exists in `Migrations/` for this feature (if one appears, non-presentational
  scope leaked in — reject).

## Definition of done (validation)

- All V1–V12 pass in every matrix cell.
- All A1–A5 pass in both themes.
- Stakeholder sign-off against agreed reference screens for each major page (SC-008).
- `dotnet test` green; no new migration; navbar tab set unchanged (SC-005).
