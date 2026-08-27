using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using BetaPlatform.Data;
using BetaPlatform.Data.Entities;
using BetaPlatform.Helpers;
using BetaPlatform.ViewModels.Dashboard;

namespace BetaPlatform.Services;

public interface IDashboardService
{
    Task<DashboardViewModel> GetAsync();
}

public class DashboardService : IDashboardService
{
    private readonly ApplicationDbContext _db;
    private readonly IMachineStatusService _status;
    private readonly TelemetryOptions _telemetry;

    public DashboardService(ApplicationDbContext db, IMachineStatusService status, IOptions<TelemetryOptions> telemetry)
    {
        _db = db;
        _status = status;
        _telemetry = telemetry.Value;
    }

    public async Task<DashboardViewModel> GetAsync()
    {
        var machines = await _db.Machines
            .Include(m => m.MachineType)
            .Where(m => m.IsActive)
            .OrderBy(m => m.MachineName)
            .ToListAsync();

        var vm = new DashboardViewModel { GeneratedAt = TimeZoneHelper.GetKsaNow() };

        var machineIds = machines.Select(m => m.MachineId).ToList();

        // One query each for the whole plant, not two per machine: the dashboard and the production
        // display both refresh every 5 s, so the old per-machine loop cost 2 × N round trips a tick
        // (research T1).
        var latestOee = await _status.GetLatestOeeAsync(machineIds);
        var latestPower = await GetLatestPowerAsync(machineIds);
        // Keyed by machine, and *present only* for machines carrying an in-progress order — the
        // same set the status rule needs, so the card's status costs no extra round trip.
        var inputWeights = await GetInputWeightsAsync(machineIds);

        foreach (var m in machines)
        {
            var card = new MachineDashboardDto
            {
                MachineId = m.MachineId,
                MachineCode = m.MachineCode,
                MachineName = m.MachineName,
                ProductionLine = m.MachineType?.ProductionLine,
                IsActive = m.IsActive,
                IsRunning = m.IsRunning,
                // Resolved from the rows already fetched above — the same rule every screen uses,
                // without paying for a second round trip to re-read them.
                Status = MachineStatusRules.Resolve(
                    latestOee.GetValueOrDefault(m.MachineId), vm.GeneratedAt, _telemetry.StaleAfter,
                    inputWeights.ContainsKey(m.MachineId)),
                InputWeight = inputWeights.TryGetValue(m.MachineId, out var weight) ? weight : 0m
            };

            // Telemetry is read-only and may be absent — degrade gracefully (FR-044).
            if (latestOee.TryGetValue(m.MachineId, out var oee))
            {
                card.HasTelemetry = true;
                card.Oee = new OeeDto
                {
                    Value = Math.Round(oee.OEE, 1),
                    Availability = oee.Availability,
                    Performance = oee.Performance,
                    Quality = oee.Quality,
                    TotalWeight = oee.TotalWeight,
                    TotalCount = oee.TotalCount,
                    TotalGoods = oee.TotalGoods,
                    Timestamp = oee.Timestamp
                };
            }

            if (latestPower.TryGetValue(m.MachineId, out var power))
            {
                card.HasTelemetry = true;
                card.Power = new PowerDto { Kw = power.KwHr, Timestamp = power.Timestamp };
            }

            vm.Machines.Add(card);
        }

        vm.Summary = BuildSummary(vm.Machines);
        vm.Summary.FinishedWorkOrders = await _db.WorkOrders
            .CountAsync(w => w.Status == WorkOrderStatus.Finished);

        return vm;
    }

    /// <summary>Latest power reading per machine in a single round trip — the same
    /// group-by-max-timestamp-then-join shape the status lookup uses (research T1).</summary>
    private async Task<Dictionary<int, PowerData>> GetLatestPowerAsync(IReadOnlyList<int> machineIds)
    {
        if (machineIds.Count == 0) return new Dictionary<int, PowerData>();

        var latestTimestamps = _db.PowerData
            .Where(p => machineIds.Contains(p.MachineId))
            .GroupBy(p => p.MachineId)
            .Select(g => new { MachineId = g.Key, Timestamp = g.Max(p => p.Timestamp) });

        var rows = await _db.PowerData
            .Join(latestTimestamps,
                p => new { p.MachineId, p.Timestamp },
                k => new { k.MachineId, k.Timestamp },
                (p, _) => p)
            .ToListAsync();

        return rows
            .GroupBy(p => p.MachineId)
            .ToDictionary(g => g.Key, g => g.OrderByDescending(p => p.Id).First());
    }

    /// <summary>
    /// Total recorded raw-material weight for each machine's current work order — the figure that
    /// replaces Good Units on the card (004, client comment 7). One query for the whole plant:
    /// every in-progress order with its input total, reduced in memory to the most recently started
    /// order per machine (ties on the highest id, matching the machine details card).
    /// </summary>
    /// <summary>Input weight of each machine's current in-progress order. A machine is in the
    /// dictionary <b>iff</b> it has an in-progress order, so the keys double as the
    /// work-in-progress set the status rule consumes.</summary>
    private async Task<Dictionary<int, decimal>> GetInputWeightsAsync(IReadOnlyList<int> machineIds)
    {
        if (machineIds.Count == 0) return new Dictionary<int, decimal>();

        var orders = await _db.WorkOrders
            .Where(w => w.MachineId != null
                        && machineIds.Contains(w.MachineId.Value)
                        && w.Status == WorkOrderStatus.InProgress)
            .Select(w => new
            {
                MachineId = w.MachineId!.Value,
                w.WorkOrderId,
                w.StartedAt,
                Weight = w.Inputs.Sum(i => (decimal?)i.Weight) ?? 0m
            })
            .ToListAsync();

        return orders
            .GroupBy(o => o.MachineId)
            .ToDictionary(
                g => g.Key,
                g => g.OrderByDescending(o => o.StartedAt)
                      .ThenByDescending(o => o.WorkOrderId)
                      .First().Weight);
    }

    /// <summary>Derives the aggregate KPI tiles from the per-machine cards already built.
    /// Averages consider only machines that reported OEE telemetry so absent machines don't
    /// drag the numbers to zero.</summary>
    private static DashboardSummary BuildSummary(List<MachineDashboardDto> machines)
    {
        var withOee = machines.Where(m => m.Oee is not null).Select(m => m.Oee!).ToList();

        return new DashboardSummary
        {
            TotalMachines = machines.Count,
            RunningMachines = machines.Count(m => m.Status == MachineRunningState.Running),
            AverageOee = withOee.Count > 0 ? Math.Round(withOee.Average(o => o.Value), 1) : 0,
            Availability = withOee.Count > 0 ? Math.Round(withOee.Average(o => o.Availability), 1) : 0,
            Performance = withOee.Count > 0 ? Math.Round(withOee.Average(o => o.Performance), 1) : 0,
            Quality = withOee.Count > 0 ? Math.Round(withOee.Average(o => o.Quality), 1) : 0,
            UnitsProduced = withOee.Sum(o => o.TotalCount),
            TotalEnergyKwh = Math.Round(machines
                .Where(m => m.Power?.Kw is not null)
                .Sum(m => m.Power!.Kw!.Value), 1)
        };
    }
}
