using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using BetaPlatform.Helpers;

namespace BetaPlatform.Data.Entities;

/// <summary>
/// A physical production machine — the anchor entity. Its integer PK (<c>machine_id</c>) is the
/// foreign key referenced by the IoT-written <c>oee_data</c> and <c>power_data</c> tables, so the
/// column name/PK are kept identical to the reference SPackEdgeView schema.
/// </summary>
[Table("machines")]
public class Machine
{
    [Key]
    [Column("machine_id")]
    public int MachineId { get; set; }

    [Required]
    [MaxLength(50)]
    [Column("machine_name")]
    public string MachineName { get; set; } = string.Empty;

    [Required]
    [MaxLength(20)]
    [Column("machine_code")]
    public string MachineCode { get; set; } = string.Empty;

    [Required]
    [Column("machine_type_id")]
    public int MachineTypeId { get; set; }

    [Column("is_active")]
    public bool IsActive { get; set; } = true;

    [Column("is_running")]
    public bool IsRunning { get; set; } = false;

    [Column("created_at")]
    public DateTime CreatedAt { get; set; } = TimeZoneHelper.GetKsaNow();

    // Navigation properties
    [ForeignKey("MachineTypeId")]
    public virtual MachineType? MachineType { get; set; }

    public virtual ICollection<OeeData> OeeData { get; set; } = new List<OeeData>();
    public virtual ICollection<PowerData> PowerData { get; set; } = new List<PowerData>();
}
