using BetaPlatform.Data.Entities;

namespace BetaPlatform.ViewModels.Machines;

/// <summary>
/// Read model for the Machine Details page — the latest telemetry snapshot plus 24-hour
/// aggregates and time-series for the trend charts. Mirrors the reference SPackEdgeView
/// machine details view. All telemetry is read-only (FR-042).
/// </summary>
public class MachineDetailsViewModel
{
    public Machine Machine { get; set; } = null!;
    public OeeData? LatestOee { get; set; }
    public PowerData? LatestPower { get; set; }

    /// <summary>Running state from the single status rule — replaces the view's inline
    /// <c>LatestOee?.Status == 1</c> check (004 — contracts/machine-status.md).</summary>
    public MachineRunningState RunningState { get; set; } = MachineRunningState.Unknown;

    // 24-hour statistics
    public decimal AverageOee24h { get; set; }
    public decimal TotalProduction24h { get; set; }
    public decimal TotalGoods24h { get; set; }
    public decimal TotalEnergy24h { get; set; }

    /// <summary>Duration-weighted, not a share of sample counts: each reading contributes the time
    /// until the next reading, capped at the staleness threshold. Uptime + downtime + no-data equals
    /// the full window, so a gap in telemetry is never counted as uptime (004 — FR-027).</summary>
    public TimeSpan Uptime24h { get; set; }
    public TimeSpan Downtime24h { get; set; }
    public TimeSpan NoDataTime24h { get; set; }

    /// <summary>The window the 24-hour figures cover, so the screen can state the period in words
    /// rather than the ambiguous "24hrs" (004 — FR-025).</summary>
    public DateTime WindowStart { get; set; }
    public DateTime WindowEnd { get; set; }

    /// <summary>The work order in progress on this machine right now, or <c>null</c> (004 — FR-021).</summary>
    public CurrentWorkOrderDto? CurrentWorkOrder { get; set; }

    /// <summary>True when more than one work order is in progress on this machine (004 — FR-024).</summary>
    public bool HasOtherWorkOrdersInProgress { get; set; }

    // Chart series
    public List<OeeChartPoint> OeeChartData { get; set; } = new();
    public List<PowerChartPoint> PowerChartData { get; set; } = new();
    public List<ProductionChartPoint> ProductionChartData { get; set; } = new();
}

public class OeeChartPoint
{
    public string Timestamp { get; set; } = string.Empty;
    public decimal Availability { get; set; }
    public decimal Performance { get; set; }
    public decimal Quality { get; set; }
    public decimal OEE { get; set; }
}

public class PowerChartPoint
{
    public string Timestamp { get; set; } = string.Empty;
    public decimal? KwHr { get; set; }
    public decimal? Voltage { get; set; }
    public decimal? Current { get; set; }
}

public class ProductionChartPoint
{
    public string Hour { get; set; } = string.Empty;
    public decimal TotalCount { get; set; }
    public decimal GoodCount { get; set; }
    public decimal DefectCount { get; set; }
}
