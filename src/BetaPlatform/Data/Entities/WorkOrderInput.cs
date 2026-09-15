using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using BetaPlatform.Helpers;

namespace BetaPlatform.Data.Entities;

/// <summary>
/// A raw-material input recorded against a work order. Per the 003 change request each input
/// carries ONLY a weight — no unique code and no tracing.
/// </summary>
[Table("work_order_inputs")]
public class WorkOrderInput
{
    [Key]
    [Column("input_id")]
    public int InputId { get; set; }

    [Required]
    [Column("work_order_id")]
    public int WorkOrderId { get; set; }

    [Column("weight")]
    public decimal Weight { get; set; }

    /// <summary>
    /// The upstream unit this input consumed, when it came off another order's line. Null for an
    /// input an operator simply weighed in — which is every input Beta records today (003/004), so
    /// the column is nullable where the reference system has it required.
    /// </summary>
    [Column("source_output_id")]
    public int? SourceOutputId { get; set; }

    [Column("created_at")]
    public DateTime CreatedAt { get; set; } = TimeZoneHelper.GetKsaNow();

    [ForeignKey("WorkOrderId")]
    public virtual WorkOrder? WorkOrder { get; set; }

    [ForeignKey("SourceOutputId")]
    public virtual WorkOrderOutput? SourceOutput { get; set; }
}
