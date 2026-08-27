using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using BetaPlatform.Helpers;

namespace BetaPlatform.Data.Entities;

/// <summary>
/// An independent production task: consumes exactly one input product and produces one output
/// (end) product on an assigned machine. Independent of other orders — no cross-order chaining
/// (FR-031/FR-032).
/// </summary>
[Table("work_orders")]
public class WorkOrder
{
    [Key]
    [Column("work_order_id")]
    public int WorkOrderId { get; set; }

    [Required]
    [MaxLength(50)]
    [Column("work_order_number")]
    public string WorkOrderNumber { get; set; } = string.Empty;

    [Required]
    [Column("input_product_id")]
    public int InputProductId { get; set; }

    [Required]
    [Column("output_product_id")]
    public int OutputProductId { get; set; }

    [Column("machine_id")]
    public int? MachineId { get; set; }

    [Required]
    [Column("planned_start_time")]
    public DateTime PlannedStartTime { get; set; }

    [Column("qty_to_manufacture")]
    public decimal QtyToManufacture { get; set; }

    /// <summary>Cost/output rate per hour for this order (003 change request). Optional.</summary>
    [Column("hour_rate")]
    public decimal? HourRate { get; set; }

    /// <summary>Line setup time in whole minutes (003 change request). Optional.</summary>
    [Column("line_setup_time_minutes")]
    public int? LineSetupTimeMinutes { get; set; }

    /// <summary>Workstation capability, in units per hour (003 change request). Optional.</summary>
    [Column("workstation_capability_per_hour")]
    public decimal? WorkstationCapabilityPerHour { get; set; }

    [Required]
    [Column("status")]
    public WorkOrderStatus Status { get; set; } = WorkOrderStatus.Ready;

    /// <summary>Start of the CURRENT active segment. Set when the order first starts and reset to
    /// "now" on every resume, so a running order's in-flight segment is always
    /// (now - StartedAt). Once an order has been held, this is no longer its original start —
    /// see <see cref="FirstStartedAt"/>.</summary>
    [Column("started_at")]
    public DateTime? StartedAt { get; set; }

    /// <summary>The immutable first time this order ever started. Preserved across holds so
    /// planned-vs-actual and the audit trail survive <see cref="StartedAt"/> being reset on
    /// resume.</summary>
    [Column("first_started_at")]
    public DateTime? FirstStartedAt { get; set; }

    [Column("finished_at")]
    public DateTime? FinishedAt { get; set; }

    /// <summary>Banked active production time in minutes, excluding time spent on hold. Each
    /// completed segment is added here on hold and on finish. The in-flight segment of a running
    /// order is NOT included — see <see cref="ActiveDuration"/> for the live figure.</summary>
    [Column("total_runtime")]
    public decimal TotalRuntime { get; set; }

    /// <summary>Live actual runtime, hold excluded. Null until the order has ever started. Equals
    /// the banked <see cref="TotalRuntime"/> plus the in-flight segment while in progress; for a
    /// finished order it equals <see cref="TotalRuntime"/> exactly. Mirrored in SQL by
    /// <c>fn_work_order_effective_runtime</c> so the app and the reporting views agree.</summary>
    [NotMapped]
    public TimeSpan? ActiveDuration
    {
        get
        {
            if (FirstStartedAt is null) return null;

            var minutes = TotalRuntime;
            if (Status == WorkOrderStatus.InProgress && StartedAt is not null)
                minutes += (decimal)(TimeZoneHelper.GetKsaNow() - StartedAt.Value).TotalMinutes;

            return TimeSpan.FromMinutes((double)minutes);
        }
    }

    [Column("created_at")]
    public DateTime CreatedAt { get; set; } = TimeZoneHelper.GetKsaNow();

    // Navigation properties
    [ForeignKey("InputProductId")]
    public virtual Product? InputProduct { get; set; }

    [ForeignKey("OutputProductId")]
    public virtual Product? OutputProduct { get; set; }

    [ForeignKey("MachineId")]
    public virtual Machine? Machine { get; set; }

    /// <summary>Manually-recorded input records — each carries only a weight, no code/tracing
    /// (003 change request).</summary>
    public virtual ICollection<WorkOrderInput> Inputs { get; set; } = new List<WorkOrderInput>();

    // Derived aggregate (not stored) — total weight of the recorded inputs.
    [NotMapped]
    public decimal TotalInputWeight => Inputs?.Sum(i => i.Weight) ?? 0m;
}
