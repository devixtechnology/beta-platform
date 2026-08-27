namespace BetaPlatform.ViewModels.Machines;

/// <summary>
/// The work order in progress on a machine right now (004 — client comment 5). Selected among the
/// machine's in-progress orders by the latest <c>started_at</c>, ties broken by the highest id.
/// </summary>
public class CurrentWorkOrderDto
{
    public int WorkOrderId { get; set; }
    public string WorkOrderNumber { get; set; } = string.Empty;
    public string? OutputProductName { get; set; }
    public decimal QtyToManufacture { get; set; }
    public DateTime? StartedAt { get; set; }

    /// <summary>Active minutes banked before the current segment — every completed run of this
    /// order, hold periods excluded. Feeds <see cref="ElapsedTime"/>; not shown on its own.</summary>
    [System.Text.Json.Serialization.JsonIgnore]
    public decimal BankedRuntimeMinutes { get; set; }

    /// <summary>Actual runtime, hold time excluded: the banked minutes plus the current segment.
    /// Not serialized directly — <see cref="ElapsedSeconds"/> is the wire format, because a
    /// TimeSpan serializes as "04:00:00" and the client should not have to parse it.</summary>
    [System.Text.Json.Serialization.JsonIgnore]
    public TimeSpan ElapsedTime { get; set; }

    public double ElapsedSeconds => Math.Round(ElapsedTime.TotalSeconds);

    /// <summary>Total weight of the raw material recorded against the order so far.</summary>
    public decimal TotalInputWeight { get; set; }
}
