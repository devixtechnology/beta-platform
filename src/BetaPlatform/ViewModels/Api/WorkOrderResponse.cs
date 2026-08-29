namespace BetaPlatform.ViewModels.Api;

/// <summary>A created work order, echoed back to the caller.</summary>
public class WorkOrderResponse
{
    public string WorkOrderNumber { get; set; } = string.Empty;

    /// <summary>Echoed exactly as submitted, so a caller confirms what was resolved without a
    /// second call (FR-027).</summary>
    public string InputProductCode { get; set; } = string.Empty;

    public string OutputProductCode { get; set; } = string.Empty;

    /// <summary>
    /// Always <c>Ready</c> on create (FR-026). Sent as the status <em>name</em>, never the
    /// underlying enum integer: a caller should not have to learn an internal numbering, and that
    /// numbering stays free to change.
    /// </summary>
    public string Status { get; set; } = string.Empty;

    public DateTime PlannedStartTime { get; set; }

    public decimal QtyToManufacture { get; set; }

    public int? MachineId { get; set; }

    public decimal? HourRate { get; set; }

    public int? LineSetupTimeMinutes { get; set; }

    public decimal? WorkstationCapabilityPerHour { get; set; }
}
