using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using BetaPlatform.Data;
using BetaPlatform.Data.Entities;
using BetaPlatform.Helpers;

namespace BetaPlatform.Services;

/// <summary>
/// Latest-telemetry lookup behind the single status rule (contracts/machine-status.md). One
/// database round trip regardless of machine count — the dashboard and the production display both
/// resolve every machine's state at once, so a per-machine query would multiply across the whole
/// plant every 5 seconds.
/// </summary>
public interface IMachineStatusService
{
    /// <summary>Running state for each requested machine. Machines with no telemetry map to
    /// <see cref="MachineRunningState.Unknown"/>.</summary>
    Task<IReadOnlyDictionary<int, MachineRunningState>> GetStatesAsync(IEnumerable<int> machineIds);

    /// <summary>Latest OEE row per requested machine. Machines with no telemetry are
    /// <b>absent</b> from the dictionary — callers must not assume every id is present.</summary>
    Task<IReadOnlyDictionary<int, OeeData>> GetLatestOeeAsync(IEnumerable<int> machineIds);

    /// <summary>The subset of the requested machines carrying an in-progress work order. One
    /// round trip; an in-progress order forces <see cref="MachineRunningState.Running"/>.</summary>
    Task<IReadOnlySet<int>> GetMachinesWithWorkInProgressAsync(IEnumerable<int> machineIds);
}

public class MachineStatusService : IMachineStatusService
{
    private readonly ApplicationDbContext _db;
    private readonly TelemetryOptions _telemetry;

    public MachineStatusService(ApplicationDbContext db, IOptions<TelemetryOptions> telemetry)
    {
        _db = db;
        _telemetry = telemetry.Value;
    }

    public async Task<IReadOnlyDictionary<int, MachineRunningState>> GetStatesAsync(IEnumerable<int> machineIds)
    {
        var ids = Materialize(machineIds);
        if (ids.Count == 0) return new Dictionary<int, MachineRunningState>();

        var latest = await GetLatestOeeAsync(ids);
        var working = await GetMachinesWithWorkInProgressAsync(ids);
        var now = TimeZoneHelper.GetKsaNow();

        return ids.Distinct().ToDictionary(
            id => id,
            id => MachineStatusRules.Resolve(
                latest.GetValueOrDefault(id), now, _telemetry.StaleAfter, working.Contains(id)));
    }

    public async Task<IReadOnlySet<int>> GetMachinesWithWorkInProgressAsync(IEnumerable<int> machineIds)
    {
        var ids = Materialize(machineIds);
        if (ids.Count == 0) return new HashSet<int>();

        var machines = await _db.WorkOrders
            .Where(w => w.MachineId != null
                        && ids.Contains(w.MachineId.Value)
                        && w.Status == WorkOrderStatus.InProgress)
            .Select(w => w.MachineId!.Value)
            .Distinct()
            .ToListAsync();

        return machines.ToHashSet();
    }

    public async Task<IReadOnlyDictionary<int, OeeData>> GetLatestOeeAsync(IEnumerable<int> machineIds)
    {
        var ids = Materialize(machineIds);
        if (ids.Count == 0) return new Dictionary<int, OeeData>();

        // Group by machine for its max timestamp, then join that back to recover the whole row —
        // one translatable SQL statement, no raw SQL (research T1). GroupBy(...).First() is
        // deliberately avoided: its translation is provider-dependent and falls back to client
        // evaluation.
        var latestTimestamps = _db.OeeData
            .Where(o => ids.Contains(o.MachineId))
            .GroupBy(o => o.MachineId)
            .Select(g => new { MachineId = g.Key, Timestamp = g.Max(o => o.Timestamp) });

        var rows = await _db.OeeData
            .Join(latestTimestamps,
                o => new { o.MachineId, o.Timestamp },
                k => new { k.MachineId, k.Timestamp },
                (o, _) => o)
            .ToListAsync();

        // Two rows can share the exact max timestamp; settle on the highest id so the answer is
        // deterministic rather than dependent on row order.
        return rows
            .GroupBy(o => o.MachineId)
            .ToDictionary(g => g.Key, g => g.OrderByDescending(o => o.Id).First());
    }

    private static IReadOnlyList<int> Materialize(IEnumerable<int> machineIds) =>
        machineIds as IReadOnlyList<int> ?? machineIds.ToList();
}
