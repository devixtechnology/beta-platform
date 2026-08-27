using System.Text.Json.Serialization;
using BetaPlatform.Data.Entities;

namespace BetaPlatform.ViewModels.Machines;

/// <summary>
/// The payload for <c>GET /Machines/Data/{id}</c>, polled every 5 s by <c>machine-details.js</c>
/// (004 — contracts/machine-live-data.md). <see cref="Latest"/>, <see cref="Power"/> and
/// <see cref="CurrentWorkOrder"/> are null when absent: a machine with no telemetry and no active
/// order returns a valid payload with three nulls and a zeroed window, never an error.
/// </summary>
public class MachineLiveDto
{
    public int MachineId { get; set; }

    /// <summary>Serialized as <c>"Running"</c> / <c>"Stopped"</c> / <c>"Unknown"</c>, the same wire
    /// format as the dashboard payload.</summary>
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public MachineRunningState Status { get; set; } = MachineRunningState.Unknown;

    public DateTime GeneratedAt { get; set; }

    public LiveOeeDto? Latest { get; set; }
    public LivePowerDto? Power { get; set; }
    public LiveWindowDto Window { get; set; } = new();
    public CurrentWorkOrderDto? CurrentWorkOrder { get; set; }
    public bool HasOtherWorkOrdersInProgress { get; set; }
}

/// <summary>The machine's most recent OEE reading.</summary>
public class LiveOeeDto
{
    public decimal Oee { get; set; }
    public decimal Availability { get; set; }
    public decimal Performance { get; set; }
    public decimal Quality { get; set; }
    public decimal TotalWeight { get; set; }
    public decimal TotalCount { get; set; }
    public decimal TotalGoods { get; set; }
    public DateTime Timestamp { get; set; }
}

public class LivePowerDto
{
    public decimal? Kw { get; set; }
    public DateTime Timestamp { get; set; }
    public decimal? V1 { get; set; }
    public decimal? V2 { get; set; }
    public decimal? V3 { get; set; }
    public decimal? A1 { get; set; }
    public decimal? A2 { get; set; }
    public decimal? A3 { get; set; }
    public decimal? AAvg { get; set; }
    public decimal? Frequency { get; set; }
}

/// <summary>
/// The 24-hour window the figures cover. <c>uptime + downtime + noData</c> equals the full window
/// length whenever any telemetry exists in it, and all three are 0 when none does.
/// </summary>
public class LiveWindowDto
{
    public DateTime Start { get; set; }
    public DateTime End { get; set; }
    public double UptimeSeconds { get; set; }
    public double DowntimeSeconds { get; set; }
    public double NoDataSeconds { get; set; }
    public decimal AverageOee { get; set; }
    public decimal TotalProduction { get; set; }
    public decimal TotalGoods { get; set; }
    public decimal TotalEnergyKwh { get; set; }
}
