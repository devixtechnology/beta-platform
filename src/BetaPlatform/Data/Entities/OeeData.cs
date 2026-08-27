using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using BetaPlatform.Helpers;

namespace BetaPlatform.Data.Entities;

/// <summary>
/// Time-stamped machine effectiveness record, written directly by the IoT team and READ-ONLY to
/// this application (FR-042/FR-050). Schema reproduces the reference SPackEdgeView <c>oee_data</c>
/// table EXACTLY so existing IoT writers work unchanged. See
/// specs/001-phase-1-core/contracts/telemetry-db-contract.md.
/// </summary>
[Table("oee_data")]
public class OeeData
{
    [Key]
    [Column("id")]
    public int Id { get; set; }

    [Required]
    [Column("machine_id")]
    public int MachineId { get; set; }

    [Required]
    [Column("timestamp")]
    public DateTime Timestamp { get; set; }

    [Column("availability")]
    public decimal Availability { get; set; } = 0;

    [Column("quality")]
    public decimal Quality { get; set; } = 0;

    [Column("performance")]
    public decimal Performance { get; set; } = 0;

    /// <summary>Accumulated produced weight for the machine/order (was historically stored in the
    /// <c>total_count</c> column; renamed to <c>total_weight</c> per the 003 change request).</summary>
    [Column("total_weight")]
    public decimal TotalWeight { get; set; } = 0;

    /// <summary>Accumulated produced unit count (new real counter added by the 003 change request).</summary>
    [Column("total_count")]
    public decimal TotalCount { get; set; } = 0;

    [Column("total_goods")]
    public decimal TotalGoods { get; set; } = 0;

    [Required]
    [Column("status")]
    public byte Status { get; set; } = 0; // 0=Stopped, 1=Running

    [Column("created_at")]
    public DateTime CreatedAt { get; set; } = TimeZoneHelper.GetKsaNow();

    /// <summary>Work order associated with this reading; set by the IoT system.</summary>
    [Column("order_id")]
    public int? OrderId { get; set; }

    [ForeignKey("MachineId")]
    public virtual Machine? Machine { get; set; }

    [ForeignKey("OrderId")]
    public virtual WorkOrder? WorkOrder { get; set; }

    /// <summary>OEE % = (Availability × Performance × Quality) / 10000 (components are percentages).</summary>
    [NotMapped]
    public decimal OEE => (Availability * Quality * Performance) / 10000;
}
