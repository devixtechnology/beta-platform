using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using BetaPlatform.Helpers;

namespace BetaPlatform.Data.Entities;

/// <summary>
/// One produced unit of a work order, identified by a code that follows it down the line.
/// </summary>
/// <remarks>
/// <para>
/// The counterpart of <see cref="WorkOrderInput"/>: an output of one order becomes the input of the
/// next, which is what <see cref="WorkOrderInput.SourceOutputId"/> records. The table mirrors the
/// reference system's <c>work_order_outputs</c> so the reporting views port across unchanged.
/// </para>
/// <para>
/// Beta has no writer for this table yet — it is filled by the production-chain slice. Until then
/// it is empty, and every aggregate the views build on it reads zero rather than null.
/// </para>
/// </remarks>
[Table("work_order_outputs")]
public class WorkOrderOutput
{
    [Key]
    [Column("output_id")]
    public int OutputId { get; set; }

    [Required]
    [Column("work_order_id")]
    public int WorkOrderId { get; set; }

    /// <summary>The code printed on the unit; unique across outputs, and how the next order names
    /// it when consuming it.</summary>
    [Required]
    [MaxLength(50)]
    [Column("unique_code")]
    public string UniqueCode { get; set; } = string.Empty;

    [Column("weight")]
    public decimal Weight { get; set; }

    /// <summary>Position of this unit within its order, from 1.</summary>
    [Column("sequence_number")]
    public int SequenceNumber { get; set; }

    [Column("created_at")]
    public DateTime CreatedAt { get; set; } = TimeZoneHelper.GetKsaNow();

    [MaxLength(500)]
    [Column("notes")]
    public string? Notes { get; set; }

    /// <summary>False until the label has been printed. The IoT printer claims rows by this flag.</summary>
    [Column("print_status")]
    public bool PrintStatus { get; set; }

    public virtual WorkOrder? WorkOrder { get; set; }

    /// <summary>The input row that consumed this output, when a later order has taken it.</summary>
    public virtual WorkOrderInput? ConsumedBy { get; set; }

    /// <summary>True once a later order has consumed this unit.</summary>
    [NotMapped]
    public bool IsConsumed => ConsumedBy is not null;
}
