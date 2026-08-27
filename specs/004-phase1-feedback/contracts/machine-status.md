# Contract: Machine Running Status

**Feature**: `004-phase1-feedback` | Satisfies FR-001 … FR-005

The single rule for "is this machine running?". Every screen consumes it; no screen re-derives it.

## The rule

```csharp
// Services/MachineStatusRules.cs — pure, static, no dependencies.
public static class MachineStatusRules
{
    public static MachineRunningState Resolve(
    OeeData? latest, DateTime now, TimeSpan staleAfter, bool hasWorkInProgress);
}
```

| Input | Output |
|-------|--------|
| `hasWorkInProgress` | `Running` — checked first, wins outright |
| `latest` is null | `Stopped` |
| `now - latest.Timestamp > staleAfter` | `Stopped` |
| `latest.Status == 1` | `Running` |
| `latest.Status == 0` | `Stopped` |
| any other `latest.Status` | `Unknown` |

**An open job means running** (amended 2026-08-27). `hasWorkInProgress` — a work order on this
machine with status `InProgress` — is evaluated before telemetry and overrides it, including a fresh
reading that says stopped. Operator-entered intent is treated as the stronger claim. Consequence to
accept: a job nobody closed keeps its machine *Running* indefinitely, and a machine that jams
mid-order still reads *Running* until the order is finished or put on hold.

**Silence means stopped** (amended 2026-08-27, superseding the original *absence → Unknown*). With no
open job, a machine that has never reported, or whose last reading has aged out, is presented as
*Stopped*: a machine that is not confirming production is not producing. `Unknown` survives only for
a reading that arrived but carries a status byte the platform cannot interpret. Consequence to
accept: a dead sensor is indistinguishable from a genuinely idle machine on the status badge alone.

Boundary: an age exactly equal to `staleAfter` is **not** stale. A reading with a timestamp in the
future is treated as current, not stale.

`staleAfter` comes from `Telemetry:StaleAfterMinutes` in configuration, default `5`.

## The lookup

```csharp
// Services/MachineStatusService.cs
public interface IMachineStatusService
{
    Task<IReadOnlyDictionary<int, MachineRunningState>> GetStatesAsync(IEnumerable<int> machineIds);
    Task<IReadOnlyDictionary<int, OeeData>> GetLatestOeeAsync(IEnumerable<int> machineIds);
    Task<IReadOnlySet<int>> GetMachinesWithWorkInProgressAsync(IEnumerable<int> machineIds);
}
```

- **One database round trip** regardless of machine count — group `oee_data` by `machine_id`, take
  `max(timestamp)`, join back to recover the rows (see research T1). Plain LINQ; no raw SQL.
- A machine with no telemetry is **absent** from `GetLatestOeeAsync` and maps to `Stopped` in
  `GetStatesAsync`. Callers must not assume every requested id is present in the OEE dictionary.
- An empty input yields an empty dictionary without querying.
- `GetMachinesWithWorkInProgressAsync` is one plant-wide query, not one per machine.
  `DashboardService` derives the same set from the in-progress orders it already reads for input
  weight, so the card's status costs it no extra round trip.

## Consumers

| Screen | Consumed via | Replaces |
|--------|--------------|----------|
| Dashboard (`/Dashboard`, `/Dashboard/Data`) | `DashboardService` | its own inline `latestOee.Status == 1` |
| Machines list + card view (`/Machines`) | `MachineService.GetAllAsync` projection | `Machine.IsRunning` — the defect in comment 2 |
| Machine details (`/Machines/Details/{id}`, `/Machines/Data/{id}`) | `MachineDetailsViewModel.RunningState` | the view's inline `LatestOee?.Status == 1` |
| Production display (`/Dashboard/Display`) | the dashboard payload | — |

No Razor view may branch on `Machine.IsRunning` or on `OeeData.Status` after this feature.

## Presentation

| State | Badge class | English | Arabic |
|-------|-------------|---------|--------|
| `Running` | `status-badge status-running` | Running | قيد التشغيل |
| `Stopped` | `status-badge status-stopped` | Stopped | متوقف |
| `Unknown` | `status-badge status-idle` | Unknown | غير معروف |

Existing resource keys `Running` / `Stopped` / `Unknown` are reused. The wire format for
`MachineDashboardDto.Status` stays the strings `"Running"` / `"Stopped"` / `"Unknown"`, so
`dashboard.js` needs no change to its `statusInfo()` mapping.

An **inactive** machine (`Machine.IsActive == false`) is shown with a separate inactive marker and its
running state is not presented as live (FR-004).

## Tests

`tests/BetaPlatform.Tests/MachineStatusRulesTests.cs`:

- work in progress → `Running`, over a silent, stale, stopped, or unreadable reading
- a Ready or Finished order → no override; telemetry decides
- null reading → `Stopped`
- `status = 1`, fresh → `Running`
- `status = 0`, fresh → `Stopped`
- `status = 1`, aged past the threshold → `Stopped`
- age exactly at the threshold → not stale
- unrecognised status byte → `Unknown`

`MachineServiceTests` / `DashboardServiceTests`: the same machine resolves to the same state through
the list projection, the details view model, and the dashboard DTO (FR-002).
