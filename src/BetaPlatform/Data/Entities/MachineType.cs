using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using BetaPlatform.Helpers;

namespace BetaPlatform.Data.Entities;

/// <summary>
/// Flat, extensible list of machine types. Each type carries a production-line grouping label
/// (no separate production-line entity — FR-013). Phase 1 seeds only two types; the remaining
/// Beta types are added by a future migration seed with no code change.
/// </summary>
[Table("machine_types")]
public class MachineType
{
    [Key]
    [Column("machine_type_id")]
    public int MachineTypeId { get; set; }

    [Required]
    [MaxLength(80)]
    [Column("name")]
    public string Name { get; set; } = string.Empty;

    [MaxLength(80)]
    [Column("name_english")]
    public string? NameEnglish { get; set; }

    [Required]
    [MaxLength(100)]
    [Column("production_line")]
    public string ProductionLine { get; set; } = string.Empty;

    [Column("is_active")]
    public bool IsActive { get; set; } = true;

    [Column("created_at")]
    public DateTime CreatedAt { get; set; } = TimeZoneHelper.GetKsaNow();

    public virtual ICollection<Machine> Machines { get; set; } = new List<Machine>();
}
