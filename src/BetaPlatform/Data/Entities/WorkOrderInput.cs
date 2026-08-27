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

    [Column("created_at")]
    public DateTime CreatedAt { get; set; } = TimeZoneHelper.GetKsaNow();

    [ForeignKey("WorkOrderId")]
    public virtual WorkOrder? WorkOrder { get; set; }
}
