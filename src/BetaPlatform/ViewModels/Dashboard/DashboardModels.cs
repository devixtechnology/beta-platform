using System.Text.Json.Serialization;
using BetaPlatform.Data.Entities;

namespace BetaPlatform.ViewModels.Dashboard;

/// <summary>Per-machine dashboard card model (mirrors contracts/dashboard-data.md).</summary>
public class MachineDashboardDto
{
    public int MachineId { get; set; }
    public string MachineCode { get; set; } = string.Empty;
    public string MachineName { get; set; } = string.Empty;
    public string? ProductionLine { get; set; }
    public bool IsActive { get; set; }
    public bool IsRunning { get; set; }

    /// <summary>Running state from the single status rule (004 — contracts/machine-status.md).
    /// Serialized as the same <c>"Running"</c> / <c>"Stopped"</c> / <c>"Unknown"</c> strings as
    /// before, so the wire format — and <c>dashboard.js</c> — are unchanged.</summary>
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public MachineRunningState Status { get; set; } = MachineRunningState.Unknown;

    public bool HasTelemetry { get; set; }
    public OeeDto? Oee { get; set; }
    public PowerDto? Power { get; set; }

    /// <summary>Total <c>work_order_inputs.weight</c> for the machine's current in-progress work
    /// order; 0 when there is none. Replaces Good Units on the card (004 — client comment 7).</summary>
    public decimal InputWeight { get; set; }
}

public class OeeDto
{
    public decimal Value { get; set; }        // (A*P*Q)/10000, %
    public decimal Availability { get; set; }
    public decimal Performance { get; set; }
    public decimal Quality { get; set; }
    public decimal TotalWeight { get; set; }
    public decimal TotalCount { get; set; }
    public decimal TotalGoods { get; set; }
    public DateTime Timestamp { get; set; }
}

public class PowerDto
{
    public decimal? Kw { get; set; }
    public DateTime Timestamp { get; set; }
}

public class DashboardViewModel
{
    public DateTime GeneratedAt { get; set; }
    public List<MachineDashboardDto> Machines { get; set; } = new();
    public DashboardSummary Summary { get; set; } = new();
}

/// <summary>Aggregate KPI tiles shown above the machine grid. Purely derived from the same
/// read-only telemetry the cards use, so it refreshes with the existing ~5s poll.</summary>
public class DashboardSummary
{
    public int TotalMachines { get; set; }
    public int RunningMachines { get; set; }
    public decimal AverageOee { get; set; }       // %
    public decimal Availability { get; set; }     // %
    public decimal Performance { get; set; }      // %
    public decimal Quality { get; set; }          // %
    public decimal TotalEnergyKwh { get; set; }
    public decimal UnitsProduced { get; set; }
    public int FinishedWorkOrders { get; set; }
}
