using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using BetaPlatform.Helpers;

namespace BetaPlatform.Data.Entities;

/// <summary>
/// One product consumed by a work order. An order draws on several materials at once — steel and
/// paint and fixings — so the input side is a list, while the output stays single (006 FR-022).
/// </summary>
/// <remarks>
/// <para>
/// <b>Not</b> the same thing as <see cref="WorkOrderInput"/>, which records a manually-entered input
/// <em>weight</em> and carries no product reference at all (003 change request). The two live side
/// by side and must never be merged: this one says <em>which</em> products an order consumes, that
/// one says <em>how much</em> was actually fed in.
/// </para>
/// <para>
/// Carries no quantity and no other attribute. 005 research R13 declined a per-input quantity
/// deliberately: nothing in the platform has anywhere to put one, and a field the platform silently
/// ignores is a lie the contract tells. Adding a member later is a compatible change; removing one
/// is not.
/// </para>
/// <para>
/// <see cref="Position"/> preserves the order the caller listed its materials in — an order it
/// recognises and expects to see echoed back (005 FR-027).
/// </para>
/// </remarks>
[Table("work_order_input_products")]
public class WorkOrderInputProduct
{
    [Key]
    [Column("work_order_input_product_id")]
    public int WorkOrderInputProductId { get; set; }

    [Required]
    [Column("work_order_id")]
    public int WorkOrderId { get; set; }

    [Required]
    [Column("product_id")]
    public int ProductId { get; set; }

    /// <summary>
    /// Zero-based place in the caller's list. Position 0 is the order's <em>primary</em> input and
    /// always names the same product as <see cref="WorkOrder.InputProductId"/> (006 FR-025) — the
    /// existing column stays required and keeps feeding the screens and the running-orders view.
    /// </summary>
    [Column("position")]
    public int Position { get; set; }

    [Column("created_at")]
    public DateTime CreatedAt { get; set; } = TimeZoneHelper.GetKsaNow();

    [ForeignKey("WorkOrderId")]
    public virtual WorkOrder? WorkOrder { get; set; }

    [ForeignKey("ProductId")]
    public virtual Product? Product { get; set; }
}
