namespace BetaPlatform.ViewModels.Api;

/// <summary>
/// A product as the API presents it — one shape for reads and creates alike (FR-019), so a caller
/// writes a single parser.
/// </summary>
/// <remarks>
/// There is deliberately no product id here (FR-022). It is the one field a reader might expect and
/// must not find: a caller addresses products by the code the plant prints and files by, and the
/// internal record number is never required from, nor given to, an external caller. Its absence is
/// the promise of this feature, not an oversight.
/// </remarks>
public class ProductResponse
{
    /// <summary>The external identity of the product.</summary>
    public string ProductCode { get; set; } = string.Empty;

    /// <summary>Primary (Arabic) name.</summary>
    public string ProductName { get; set; } = string.Empty;

    public string? ProductNameEnglish { get; set; }

    public string? Category { get; set; }

    /// <summary>Unit of measure, e.g. <c>kg</c>.</summary>
    public string Unit { get; set; } = string.Empty;

    /// <summary>False for a retired product. A deactivated product is still returned rather than
    /// reported missing — a caller reconciling history needs to see it.</summary>
    public bool IsActive { get; set; }
}
