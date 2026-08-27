using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using BetaPlatform.Data;
using BetaPlatform.Data.Entities;
using BetaPlatform.Helpers;
using BetaPlatform.ViewModels.Machines;

namespace BetaPlatform.Services;

public interface IMachineService
{
    Task<List<Machine>> GetAllAsync();

    /// <summary>Every machine with the running state resolved by the shared status rule — what the
    /// machines list and card view render (004 — FR-002).</summary>
    Task<List<MachineListItemViewModel>> GetAllWithStatusAsync();

    Task<List<Machine>> GetActiveAsync();
    Task<Machine?> GetByIdAsync(int id);
    Task<MachineDetailsViewModel?> GetDetailsAsync(int id);

    /// <summary>The payload polled every 5 s by the machine details page; null for an unknown
    /// machine (004 - contracts/machine-live-data.md).</summary>
    Task<MachineLiveDto?> GetLiveAsync(int id);
    Task<List<MachineType>> GetActiveTypesAsync();
    Task<ServiceResult<Machine>> CreateAsync(Machine machine);
    Task<ServiceResult<Machine>> UpdateAsync(Machine machine);
    Task<ServiceResult> DeactivateAsync(int id);
}

public class MachineService : IMachineService
{
    private readonly ApplicationDbContext _db;
    private readonly IMachineStatusService _status;
    private readonly TelemetryOptions _telemetry;

    public MachineService(ApplicationDbContext db, IMachineStatusService status, IOptions<TelemetryOptions> telemetry)
    {
        _db = db;
        _status = status;
        _telemetry = telemetry.Value;
    }

    public Task<List<Machine>> GetAllAsync() =>
        _db.Machines.Include(m => m.MachineType).OrderBy(m => m.MachineName).ToListAsync();

    public async Task<List<MachineListItemViewModel>> GetAllWithStatusAsync()
    {
        var machines = await GetAllAsync();
        // One lookup for the whole list — never one query per row.
        var states = await _status.GetStatesAsync(machines.Select(m => m.MachineId).ToList());

        return machines
            .Select(m => new MachineListItemViewModel
            {
                Machine = m,
                RunningState = states.TryGetValue(m.MachineId, out var state) ? state : MachineRunningState.Unknown
            })
            .ToList();
    }

    public Task<List<Machine>> GetActiveAsync() =>
        _db.Machines.Include(m => m.MachineType).Where(m => m.IsActive).OrderBy(m => m.MachineName).ToListAsync();

    public Task<Machine?> GetByIdAsync(int id) =>
        _db.Machines.Include(m => m.MachineType).FirstOrDefaultAsync(m => m.MachineId == id);

    public async Task<MachineDetailsViewModel?> GetDetailsAsync(int id)
    {
        var machine = await _db.Machines.Include(m => m.MachineType)
            .FirstOrDefaultAsync(m => m.MachineId == id);
        if (machine is null) return null;

        var now = TimeZoneHelper.GetKsaNow();
        var last24h = now.AddHours(-24);

        var latestOee = await _db.OeeData
            .Where(o => o.MachineId == id)
            .OrderByDescending(o => o.Timestamp)
            .FirstOrDefaultAsync();

        var latestPower = await _db.PowerData
            .Where(p => p.MachineId == id)
            .OrderByDescending(p => p.Timestamp)
            .FirstOrDefaultAsync();

        // Pull the 24h windows into memory (OEE is a computed [NotMapped] property).
        var oee24h = await _db.OeeData
            .Where(o => o.MachineId == id && o.Timestamp >= last24h)
            .OrderBy(o => o.Timestamp)
            .ToListAsync();

        var power24h = await _db.PowerData
            .Where(p => p.MachineId == id && p.Timestamp >= last24h)
            .OrderBy(p => p.Timestamp)
            .ToListAsync();

        var current = await GetCurrentWorkOrderAsync(id, now);

        var vm = new MachineDetailsViewModel
        {
            Machine = machine,
            LatestOee = latestOee,
            LatestPower = latestPower,
            AverageOee24h = oee24h.Count > 0 ? Math.Round(oee24h.Average(o => o.OEE), 1) : 0,
            TotalProduction24h = latestOee?.TotalCount ?? 0,
            TotalGoods24h = latestOee?.TotalGoods ?? 0,
            TotalEnergy24h = Math.Round(IntegrateEnergyKwh(power24h), 1),
            RunningState = MachineStatusRules.Resolve(
                latestOee, now, _telemetry.StaleAfter, current.Order is not null),
            WindowStart = last24h,
            WindowEnd = now
        };

        var accounting = AccountUptime(oee24h, last24h, now, _telemetry.StaleAfter);
        vm.Uptime24h = accounting.Uptime;
        vm.Downtime24h = accounting.Downtime;
        vm.NoDataTime24h = accounting.NoData;

        vm.CurrentWorkOrder = current.Order;
        vm.HasOtherWorkOrdersInProgress = current.HasOthers;

        // Sample the series (~every 30 min) so charts stay readable.
        vm.OeeChartData = SampleByMinutes(oee24h, 30).Select(o => new OeeChartPoint
        {
            Timestamp = o.Timestamp.ToString("HH:mm"),
            Availability = o.Availability,
            Performance = o.Performance,
            Quality = o.Quality,
            OEE = Math.Round(o.OEE, 1)
        }).ToList();

        vm.PowerChartData = SampleByMinutes(power24h, 30, p => p.Timestamp).Select(p => new PowerChartPoint
        {
            Timestamp = p.Timestamp.ToString("HH:mm"),
            KwHr = p.KwHr,
            Voltage = ((p.V1 ?? 0) + (p.V2 ?? 0) + (p.V3 ?? 0)) / 3,
            Current = p.AAvg
        }).ToList();

        // Production per hour of day (delta of the cumulative counters within each hour).
        vm.ProductionChartData = oee24h
            .GroupBy(o => o.Timestamp.Hour)
            .Select(g =>
            {
                var count = g.Max(o => o.TotalCount) - g.Min(o => o.TotalCount);
                var goods = g.Max(o => o.TotalGoods) - g.Min(o => o.TotalGoods);
                if (count < 0) count = 0;
                if (goods < 0) goods = 0;
                return new ProductionChartPoint
                {
                    Hour = $"{g.Key:D2}:00",
                    TotalCount = count,
                    GoodCount = goods,
                    DefectCount = count - goods < 0 ? 0 : count - goods
                };
            })
            .OrderBy(p => p.Hour)
            .ToList();

        return vm;
    }

    public async Task<MachineLiveDto?> GetLiveAsync(int id)
    {
        if (!await _db.Machines.AnyAsync(m => m.MachineId == id)) return null;

        var now = TimeZoneHelper.GetKsaNow();
        var last24h = now.AddHours(-24);

        var latestOee = await _db.OeeData
            .Where(o => o.MachineId == id)
            .OrderByDescending(o => o.Timestamp)
            .FirstOrDefaultAsync();

        var latestPower = await _db.PowerData
            .Where(p => p.MachineId == id)
            .OrderByDescending(p => p.Timestamp)
            .FirstOrDefaultAsync();

        var oee24h = await _db.OeeData
            .Where(o => o.MachineId == id && o.Timestamp >= last24h)
            .OrderBy(o => o.Timestamp)
            .ToListAsync();

        var power24h = await _db.PowerData
            .Where(p => p.MachineId == id && p.Timestamp >= last24h)
            .OrderBy(p => p.Timestamp)
            .ToListAsync();

        var accounting = AccountUptime(oee24h, last24h, now, _telemetry.StaleAfter);
        var current = await GetCurrentWorkOrderAsync(id, now);

        return new MachineLiveDto
        {
            MachineId = id,
            Status = MachineStatusRules.Resolve(
                latestOee, now, _telemetry.StaleAfter, current.Order is not null),
            GeneratedAt = now,
            Latest = latestOee is null ? null : new LiveOeeDto
            {
                Oee = Math.Round(latestOee.OEE, 1),
                Availability = latestOee.Availability,
                Performance = latestOee.Performance,
                Quality = latestOee.Quality,
                TotalWeight = latestOee.TotalWeight,
                TotalCount = latestOee.TotalCount,
                TotalGoods = latestOee.TotalGoods,
                Timestamp = latestOee.Timestamp
            },
            Power = latestPower is null ? null : new LivePowerDto
            {
                Kw = latestPower.KwHr,
                Timestamp = latestPower.Timestamp,
                V1 = latestPower.V1,
                V2 = latestPower.V2,
                V3 = latestPower.V3,
                A1 = latestPower.A1,
                A2 = latestPower.A2,
                A3 = latestPower.A3,
                AAvg = latestPower.AAvg,
                Frequency = latestPower.Frequency
            },
            Window = new LiveWindowDto
            {
                Start = last24h,
                End = now,
                UptimeSeconds = Math.Round(accounting.Uptime.TotalSeconds),
                DowntimeSeconds = Math.Round(accounting.Downtime.TotalSeconds),
                NoDataSeconds = Math.Round(accounting.NoData.TotalSeconds),
                AverageOee = oee24h.Count > 0 ? Math.Round(oee24h.Average(o => o.OEE), 1) : 0,
                TotalProduction = latestOee?.TotalCount ?? 0,
                TotalGoods = latestOee?.TotalGoods ?? 0,
                TotalEnergyKwh = Math.Round(IntegrateEnergyKwh(power24h), 1)
            },
            CurrentWorkOrder = current.Order,
            HasOtherWorkOrdersInProgress = current.HasOthers
        };
    }

    /// <summary>
    /// Uptime and downtime accounted by <b>duration</b>, not by sample count (research T2). Each
    /// reading contributes the time until the next reading to uptime or downtime according to its
    /// status, capped at the staleness threshold; everything else — the time before the first
    /// reading, the tail beyond each cap, and the stretch after the last reading — is "no data".
    /// <para>
    /// The old calculation was <c>runningSamples / totalSamples x 24 h</c>, which assumes a perfectly
    /// even reporting cadence: a six-hour writer outage while a machine ran was shared across both
    /// figures and reported downtime that never happened. FR-027 forbids counting an absence of
    /// telemetry as uptime, so the third bucket is what makes the total honest —
    /// uptime + downtime + no-data is always exactly the window.
    /// </para>
    /// </summary>
    internal static (TimeSpan Uptime, TimeSpan Downtime, TimeSpan NoData) AccountUptime(
        List<OeeData> readings, DateTime windowStart, DateTime windowEnd, TimeSpan staleAfter)
    {
        if (readings.Count == 0)
            return (TimeSpan.Zero, TimeSpan.Zero, TimeSpan.Zero);

        var uptime = TimeSpan.Zero;
        var downtime = TimeSpan.Zero;

        for (var i = 0; i < readings.Count; i++)
        {
            var reading = readings[i];
            var from = reading.Timestamp < windowStart ? windowStart : reading.Timestamp;
            if (from >= windowEnd) continue;

            // The reading speaks for the time until the next one, but only for as long as it can
            // still be believed.
            var until = i + 1 < readings.Count ? readings[i + 1].Timestamp : windowEnd;
            var capped = from + staleAfter;
            if (until > capped) until = capped;
            if (until > windowEnd) until = windowEnd;
            if (until <= from) continue;

            var span = until - from;
            if (reading.Status == 1) uptime += span;
            else if (reading.Status == 0) downtime += span;
            // Any other status value says nothing trustworthy — it falls through to "no data".
        }

        var window = windowEnd - windowStart;
        var noData = window - uptime - downtime;
        if (noData < TimeSpan.Zero) noData = TimeSpan.Zero;

        return (uptime, downtime, noData);
    }

    /// <summary>
    /// The work order in progress on this machine: among orders with <c>status = InProgress</c>, the
    /// one with the latest <c>started_at</c>, ties broken by the highest id (004 - FR-021/FR-024).
    /// </summary>
    private async Task<(CurrentWorkOrderDto? Order, bool HasOthers)> GetCurrentWorkOrderAsync(int machineId, DateTime now)
    {
        // Projected rather than Included: the product is an optional left join here — a work order
        // whose output product was removed must still show on the machine — and the input total is
        // a subquery, so this is one round trip that fetches only what the card renders.
        var inProgress = await _db.WorkOrders
            .Where(w => w.MachineId == machineId && w.Status == WorkOrderStatus.InProgress)
            .OrderByDescending(w => w.StartedAt)
            .ThenByDescending(w => w.WorkOrderId)
            .Select(w => new CurrentWorkOrderDto
            {
                WorkOrderId = w.WorkOrderId,
                WorkOrderNumber = w.WorkOrderNumber,
                // A correlated subquery rather than the navigation property: a required reference
                // navigation is joined as an inner join by some providers, which would silently
                // drop the work order when its product no longer exists.
                OutputProductName = _db.Products
                    .Where(p => p.ProductId == w.OutputProductId)
                    .Select(p => p.ProductName)
                    .FirstOrDefault(),
                QtyToManufacture = w.QtyToManufacture,
                StartedAt = w.StartedAt,
                BankedRuntimeMinutes = w.TotalRuntime,
                TotalInputWeight = w.Inputs.Sum(i => (decimal?)i.Weight) ?? 0m
            })
            .ToListAsync();

        if (inProgress.Count == 0) return (null, false);

        var order = inProgress[0];
        // Hold-excluded, matching WorkOrder.ActiveDuration: the banked segments plus the segment
        // running now. These orders are all InProgress, so the current segment always counts.
        var minutes = order.BankedRuntimeMinutes +
                      (order.StartedAt is null ? 0m : (decimal)(now - order.StartedAt.Value).TotalMinutes);
        order.ElapsedTime = TimeSpan.FromMinutes((double)minutes);

        return (order, inProgress.Count > 1);
    }

    /// <summary>Approximate energy (kWh) by integrating power over the time between readings.</summary>
    private static decimal IntegrateEnergyKwh(List<PowerData> readings)
    {
        decimal energy = 0;
        for (var i = 1; i < readings.Count; i++)
        {
            var hours = (decimal)(readings[i].Timestamp - readings[i - 1].Timestamp).TotalHours;
            var avgKw = ((readings[i].KwHr ?? 0) + (readings[i - 1].KwHr ?? 0)) / 2;
            energy += avgKw * hours;
        }
        return energy;
    }

    private static List<OeeData> SampleByMinutes(List<OeeData> data, int minutes) =>
        SampleByMinutes(data, minutes, o => o.Timestamp);

    private static List<T> SampleByMinutes<T>(List<T> data, int minutes, Func<T, DateTime> ts)
    {
        var sampled = new List<T>();
        var last = DateTime.MinValue;
        foreach (var item in data)
        {
            if ((ts(item) - last).TotalMinutes >= minutes)
            {
                sampled.Add(item);
                last = ts(item);
            }
        }
        return sampled;
    }

    public Task<List<MachineType>> GetActiveTypesAsync() =>
        _db.MachineTypes.Where(t => t.IsActive).OrderBy(t => t.Name).ToListAsync();

    public async Task<ServiceResult<Machine>> CreateAsync(Machine machine)
    {
        if (await CodeExistsAsync(machine.MachineCode, null))
            return ServiceResult<Machine>.Fail($"Machine code '{machine.MachineCode}' already exists.");
        if (await NameExistsAsync(machine.MachineName, null))
            return ServiceResult<Machine>.Fail($"Machine name '{machine.MachineName}' already exists.");
        if (!await _db.MachineTypes.AnyAsync(t => t.MachineTypeId == machine.MachineTypeId && t.IsActive))
            return ServiceResult<Machine>.Fail("Selected machine type is not available.");

        _db.Machines.Add(machine);
        await _db.SaveChangesAsync();
        return ServiceResult<Machine>.Ok(machine);
    }

    public async Task<ServiceResult<Machine>> UpdateAsync(Machine machine)
    {
        var existing = await _db.Machines.FirstOrDefaultAsync(m => m.MachineId == machine.MachineId);
        if (existing is null)
            return ServiceResult<Machine>.Fail("Machine not found.");
        if (await CodeExistsAsync(machine.MachineCode, machine.MachineId))
            return ServiceResult<Machine>.Fail($"Machine code '{machine.MachineCode}' already exists.");
        if (await NameExistsAsync(machine.MachineName, machine.MachineId))
            return ServiceResult<Machine>.Fail($"Machine name '{machine.MachineName}' already exists.");

        existing.MachineName = machine.MachineName;
        existing.MachineCode = machine.MachineCode;
        existing.MachineTypeId = machine.MachineTypeId;
        existing.IsActive = machine.IsActive;
        // IsRunning is deliberately NOT copied: it is an inert administrative flag after 004 and no
        // screen reads it. Running state comes from telemetry (contracts/machine-status.md).
        await _db.SaveChangesAsync();
        return ServiceResult<Machine>.Ok(existing);
    }

    public async Task<ServiceResult> DeactivateAsync(int id)
    {
        var machine = await _db.Machines.FirstOrDefaultAsync(m => m.MachineId == id);
        if (machine is null)
            return ServiceResult.Fail("Machine not found.");

        // Soft-deactivate only — never delete; preserves telemetry & work-order links (FR-016/FR-052).
        machine.IsActive = false;
        await _db.SaveChangesAsync();
        return ServiceResult.Ok();
    }

    private Task<bool> CodeExistsAsync(string code, int? excludeId) =>
        _db.Machines.AnyAsync(m => m.MachineCode == code && (excludeId == null || m.MachineId != excludeId));

    private Task<bool> NameExistsAsync(string name, int? excludeId) =>
        _db.Machines.AnyAsync(m => m.MachineName == name && (excludeId == null || m.MachineId != excludeId));
}
