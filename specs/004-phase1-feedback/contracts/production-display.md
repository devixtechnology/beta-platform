# Contract: Production-Floor Display

**Feature**: `004-phase1-feedback` | Satisfies FR-032 … FR-039

Comment 6: one big screen for the production area. It presents data the platform already produces —
no new calculation, no new payload.

## Route

```
GET /Dashboard/Display
```

Authenticated, both roles (research D3). Renders `Views/Dashboard/Display.cshtml` using
`Views/Shared/_DisplayLayout.cshtml`, and polls the **existing** `GET /Dashboard/Data` endpoint. No
second data contract is introduced, so the display can never disagree with the dashboard (FR-002).

## Layout contract — `_DisplayLayout.cshtml`

A separate chromeless layout, not `_Layout` with things hidden:

- no navbar, no footer, no breadcrumbs, no buttons, no links (FR-036)
- no language switcher and no theme toggle — the display is configured once, then left alone
- dark theme fixed, for a screen that runs all day in a lit workspace
- `overflow: hidden` on the body — the view fits or it rotates; it never scrolls (FR-033)
- the same `site.css` design tokens as the rest of the platform, so a colour change lands everywhere

## Content per machine tile

Machine name · machine code · status badge · OEE gauge · availability / performance / quality bars ·
power (kW) · total weight · total count · input weight.

Same fields as the dashboard card, at display scale.

## Legibility (FR-037)

| Element | Minimum |
|---------|---------|
| Machine name | 2.5 rem |
| OEE value | 4 rem |
| Status badge text | 1.75 rem |
| Stat values | 2 rem |
| Stat labels | 1.25 rem |

Sized in `rem` against a root size the layout scales with the viewport, so the same view works on a
43″ and an 86″ panel. Status is carried by badge text and position as well as colour, so it survives a
miscalibrated screen and colour-vision differences (WCAG 2.1 AA, SC-011).

## Grid and rotation (FR-034)

- Up to **6** machines: one page, 3 × 2.
- More than 6: paginate into 6-tile pages and rotate every **15 s**, with a page indicator
  (`2 / 3`) so a viewer knows more exists.
- Rotation pauses while a page is mid-refresh so a tile never changes underneath a transition.
- Page count is recalculated when the machine list changes, keeping the current page index if it is
  still valid and resetting to page 1 if not.

## Refresh and resilience

- Polls `/Dashboard/Data` every **5000 ms**, same as the dashboard (FR-035).
- Failed poll: keep the last-good render, retry on the next tick. No dialog, no redirect, no blank
  screen (FR-039).
- A `302` to the login page is treated as a failed poll — the view keeps rendering the last-good data
  rather than navigating away, so a session problem never leaves a login form on the wall.
- The session stays alive on its own: the persistent "remember me" cookie survives a power cut, and
  sliding expiration is renewed by every poll (research T3). Nobody has to attend the display.
- A "last updated" stamp is shown small in a corner so a viewer can tell live data from a frozen
  screen.

## Empty states

| Situation | Display |
|-----------|---------|
| No active machines | full-screen message, styled like the existing `_EmptyState` |
| Machines exist, none reporting telemetry | tiles render with `Stopped` status and dashes for values |
| Single machine | one tile, centred, no rotation |

## Setup, for the quickstart

1. An administrator creates a display account at `/Users/Create` with the `Client` role.
2. On the display machine, sign in with that account and **remember me** ticked.
3. Navigate to `/Dashboard/Display` and put the browser in full screen (F11).
4. Disable the operating system's screen blanking and sleep.

## Tests

Verified by the quickstart rather than by unit tests — the view has no logic of its own beyond
rendering and rotation, and its data comes from `DashboardService`, which is already covered by
`DashboardServiceTests`.
