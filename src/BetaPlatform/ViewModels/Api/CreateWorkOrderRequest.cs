using System.ComponentModel.DataAnnotations;

namespace BetaPlatform.ViewModels.Api;

/// <summary>
/// A work order submitted to <c>POST /api/v1/work-orders</c>, naming its products <strong>by
/// code</strong>.
/// </summary>
/// <remarks>
/// This is the point of the feature: the caller sends the codes the plant prints and files by, and
/// the platform resolves them. The internal <c>input_product_id</c> and <c>output_product_id</c>
/// appear nowhere in this contract, in either direction (FR-022).
/// </remarks>
public class CreateWorkOrderRequest
{
    [Required]
    [MaxLength(50)]
    public string WorkOrderNumber { get; set; } = string.Empty;

    /// <summary>
    /// Product <em>codes</em> of the materials consumed — never record numbers. A list, because a
    /// real order draws on several materials at once; at least one entry is required, none may be
    /// blank, and no code may appear twice (amended 2026-09-01, FR-042).
    /// </summary>
    /// <remarks>
    /// The output side deliberately stays singular: an order produces one end product, and pluralising
    /// it would invent a shape the plant does not work in.
    /// </remarks>
    [Required]
    [ProductCodeList]
    public List<string> InputProductCodes { get; set; } = [];

    /// <summary>
    /// Product <em>code</em> of what is produced — exactly one. May repeat a code from the inputs:
    /// a rework or re-packing order legitimately consumes and produces the same product.
    /// </summary>
    [Required]
    public string OutputProductCode { get; set; } = string.Empty;

    [Required]
    public DateTime? PlannedStartTime { get; set; }

    /// <summary>Must be greater than zero — an order to manufacture nothing is a mistake worth
    /// catching at the edge, so zero is rejected as well as negative.</summary>
    [Required]
    [GreaterThanZero(ErrorMessage = "The qtyToManufacture field must be greater than zero.")]
    public decimal? QtyToManufacture { get; set; }

    /// <summary>
    /// Optional assigned machine. The one internal identifier this contract retains: FR-022
    /// constrains <em>products</em>, and machines have no external code on this surface.
    /// </summary>
    [Range(1, int.MaxValue)]
    public int? MachineId { get; set; }

    [NotNegative]
    public decimal? HourRate { get; set; }

    [Range(0, int.MaxValue)]
    public int? LineSetupTimeMinutes { get; set; }

    [NotNegative]
    public decimal? WorkstationCapabilityPerHour { get; set; }
}
