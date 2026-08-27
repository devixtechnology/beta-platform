using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using BetaPlatform.Helpers;

namespace BetaPlatform.Data.Entities;

/// <summary>
/// A material or finished good used as a work-order input or output. Bilingual name support:
/// <c>ProductName</c> (Arabic/primary) + optional <c>ProductNameEnglish</c> (FR-021/FR-062).
/// </summary>
[Table("products")]
public class Product
{
    [Key]
    [Column("product_id")]
    public int ProductId { get; set; }

    [Required]
    [MaxLength(50)]
    [Column("product_code")]
    public string ProductCode { get; set; } = string.Empty;

    [Required]
    [MaxLength(200)]
    [Column("product_name")]
    public string ProductName { get; set; } = string.Empty;

    [MaxLength(200)]
    [Column("product_name_english")]
    public string? ProductNameEnglish { get; set; }

    [MaxLength(100)]
    [Column("category")]
    public string? Category { get; set; }

    [Required]
    [MaxLength(20)]
    [Column("unit")]
    public string Unit { get; set; } = "kg";

    [Column("is_active")]
    public bool IsActive { get; set; } = true;

    [Column("created_at")]
    public DateTime CreatedAt { get; set; } = TimeZoneHelper.GetKsaNow();
}
