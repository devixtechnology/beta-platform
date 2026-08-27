# Contract: Dashboard Polling Endpoint

**Feature**: `001-phase-1-core` | **Endpoint**: `GET /Dashboard/Data` | **Auth**: `[Authorize]`

The dashboard renders once server-side, then a client `setInterval` (default **~5 s**, FR-043)
`fetch`es this endpoint and patches each machine card in place. No SignalR (FR-043 — real-time push
not required). Response is JSON.

## Response shape

```jsonc
{
  "generatedAt": "2026-07-07T14:32:05+03:00",   // KSA time
  "machines": [
    {
      "machineId": 1,
      "machineCode": "FRM-01",
      "machineName": "Forming Machine 1",
      "productionLine": "Armor Rod & Guy Grip line",
      "isActive": true,
      "isRunning": true,
      "status": "Running",                 // "Running" | "Stopped" | "Unknown"
      "hasTelemetry": true,                // false => render unknown/empty state (FR-044)
      "oee": {
        "value": 82.5,                     // (availability*performance*quality)/10000, %
        "availability": 95.0,              // %
        "performance": 90.0,               // %
        "quality": 96.5,                   // %
        "totalCount": 1200.00,
        "totalGoods": 1160.00,
        "timestamp": "2026-07-07T14:32:00+03:00"
      },
      "power": {
        "kw": 12.40,                       // from power_data.kw_hr latest
        "timestamp": "2026-07-07T14:32:04+03:00"
      }
    },
    {
      "machineId": 2,
      "machineCode": "FWL-01",
      "machineName": "Flat Washer Line 1",
      "productionLine": "Flat Washer Line",
      "isActive": true,
      "isRunning": false,
      "status": "Unknown",
      "hasTelemetry": false,               // no oee/power rows yet
      "oee": null,
      "power": null
    }
  ]
}
```

## Rules

- One entry per **active** machine (`is_active = true`) — FR-040.
- `oee` / `power` come from the **latest** `oee_data` / `power_data` row for that `machine_id`
  (each independently; one may be present while the other is null).
- `hasTelemetry = false` and `status = "Unknown"` when no OEE row exists — the client renders an
  empty/unknown card rather than failing (FR-044, SC-005). A malformed or orphaned telemetry row
  for a non-existent machine is ignored (edge case).
- `oee.value` = `(availability × performance × quality) / 10000`, rounded for display.
- Values reflect the most recent readings each poll cycle (FR-043, SC-004).
- Endpoint is read-only; it never writes telemetry (FR-042).
