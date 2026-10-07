using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using BetaPlatform.Helpers;

namespace BetaPlatform.Data.Entities;

/// <summary>
/// A raw-material input recorded against a work order: a weight, and which product it was.
/// </summary>
/// <remarks>
/// The product was added so the ERP's <c>mo/finish</c> call can report consumption per product
/// (<c>consumed_components</c>). It is nullable only because inputs recorded before it existed name
/// none; every new input must name one, and a product-less input is left out of that list.
/// </remarks>
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

    /// <summary>The product this weight was of. Null only on inputs recorded before it existed.</summary>
    [Column("product_id")]
    public int? ProductId { get; set; }

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

    [ForeignKey("ProductId")]
    public virtual Product? Product { get; set; }

    [ForeignKey("SourceOutputId")]
    public virtual WorkOrderOutput? SourceOutput { get; set; }
}
