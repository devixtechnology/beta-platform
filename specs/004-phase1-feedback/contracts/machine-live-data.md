# Contract: Machine Details Live Data

**Feature**: `004-phase1-feedback` | Satisfies FR-018 … FR-024, FR-025 … FR-027

Makes the machine details page keep itself current (comment 4) and show the work order in progress
(comment 5), following the polling pattern the dashboard already uses.

## Endpoint

```
GET /Machines/Data/{id}
Accept: application/json
```

Authenticated (global fallback policy). Any signed-in user, both roles. Read-only — no side effects.

**Responses**

| Status | When |
|--------|------|
| `200` | machine exists; payload below |
| `404` | no machine with that id |
| `302 → /Auth/Login` | session expired (client keeps its last-good view and retries) |

## Payload

```jsonc
{
  "machineId": 1,
  "status": "Running",              // "Running" | "Stopped" | "Unknown" — machine-status contract
  "generatedAt": "2026-08-23T14:03:11",
  "latest": {
    "oee": 72.4, "availability": 91.0, "performance": 88.2, "quality": 90.1,
    "totalWeight": 1250.0, "totalCount": 5000, "totalGoods": 4870,
    "timestamp": "2026-08-23T14:03:05"
  },
  "power": { "kw": 12.7, "timestamp": "2026-08-23T14:03:04" },
  "window": {
    "start": "2026-08-22T14:03:11", "end": "2026-08-23T14:03:11",
    "uptimeSeconds": 61200, "downtimeSeconds": 21600, "noDataSeconds": 3600,
    "averageOee": 68.9, "totalProduction": 5000, "totalGoods": 4870, "totalEnergyKwh": 284.5
  },
  "currentWorkOrder": {
    "workOrderId": 12, "workOrderNumber": "WO-2026-0012",
    "outputProductName": "Armor Rod 12mm",
    "qtyToManufacture": 5000,
    "startedAt": "2026-08-23T06:00:00", "elapsedSeconds": 28991,
    "totalInputWeight": 1400.0
  },
  "hasOtherWorkOrdersInProgress": false
}
```

`latest`, `power`, and `currentWorkOrder` are **null** when absent — a machine with no telemetry and
no active order returns a valid payload with three nulls and a zeroed window, never an error.

`uptimeSeconds + downtimeSeconds + noDataSeconds` equals the full window length (86400) whenever any
telemetry exists in the window; all three are 0 when none does.

## Client behaviour — `wwwroot/js/machine-details.js`

- Polls every **5000 ms**, matching `dashboard.js`. First poll fires immediately on load.
- Updates values **in place, element by element** (`textContent`, width styles, class swaps). It must
  never rewrite a container's `innerHTML`, so scroll position and focus survive (FR-019).
- On a failed or non-OK response: swallow the error, keep the last-good values on screen, and let the
  next tick retry (FR-020). No error dialog, no spinner replacing content.
- Charts: the three Chart.js instances are updated by assigning new data and calling `.update()` on
  the existing chart at a **60 s** cadence, not on every poll — a rebuilt canvas flickers and the
  series only moves at the 30-minute sampling interval anyway.
- The current-work-order card is shown, updated, or replaced by its empty state as the payload
  changes, so finishing an order clears the card on the next tick without a page reload.

## Elements updated per poll

Status badge · current OEE · current power · production (24 h) · quality rate · availability /
performance / quality bars · uptime · downtime · no-data (shown only when non-zero) · energy (24 h) ·
current work order card · last-updated stamp.

## Uptime and downtime

Computed by `MachineService` per research T2: each reading contributes the duration until the next
reading to uptime or downtime by its status, each interval capped at the staleness threshold; the
remainder is `noDataSeconds`. Labels state the period in words — "Uptime (last 24 h)" — in both
languages (FR-025).

## Current work order selection

Work orders with `machine_id = {id}` and status `InProgress`, ordered by `started_at` descending then
`work_order_id` descending; the first is `currentWorkOrder`, and `hasOtherWorkOrdersInProgress` is
true when more than one matched (FR-024).

The card links to `/WorkOrders/Details/{workOrderId}` (FR-022). With no match, the page shows the
existing `_EmptyState` partial with a "no work order in progress" message (FR-023).

## Tests

`MachineServiceTests`: current work order picked by latest start; ties broken by id; `HasOther…` set
with two in-progress orders; null with none. Uptime/downtime/no-data sum to the window; a telemetry
gap lands in no-data rather than uptime (FR-027); no telemetry at all yields three zeros.
