using System.ComponentModel.DataAnnotations;

namespace BetaPlatform.ViewModels.Api;

/// <summary>
/// A new product submitted to <c>POST /api/v1/products</c>.
/// </summary>
/// <remarks>
/// <para>
/// Lengths mirror the stored columns exactly, so a caller gets a 400 naming the field rather than a
/// truncation failure further in.
/// </para>
/// <para>
/// <c>IsActive</c> and <c>CreatedAt</c> are deliberately absent. A created product is always active
/// (FR-017) and its creation time is the server's to assign — offering fields the platform overrides
/// would invite a caller to believe otherwise.
/// </para>
/// </remarks>
public class CreateProductRequest
{
    /// <summary>The external identity of the product. Trimmed; compared case-insensitively.</summary>
    [Required]
    [MaxLength(50)]
    public string ProductCode { get; set; } = string.Empty;

    /// <summary>Primary (Arabic) name.</summary>
    [Required]
    [MaxLength(200)]
    public string ProductName { get; set; } = string.Empty;

    [MaxLength(200)]
    public string? ProductNameEnglish { get; set; }

    [MaxLength(100)]
    public string? Category { get; set; }

    /// <summary>Unit of measure, e.g. <c>kg</c>.</summary>
    [Required]
    [MaxLength(20)]
    public string Unit { get; set; } = string.Empty;
}
