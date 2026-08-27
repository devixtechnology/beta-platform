using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace BetaPlatform.Data.Entities;

/// <summary>
/// Arbitrary per-machine properties. Ported from the reference SPackEdgeView schema. The
/// <c>id</c> is a bigint-unsigned surrogate. <c>source_identifier</c> is a foreign key into
/// <see cref="SourceAvailable"/> (its unique <c>source_identifier</c>), and <c>machine_id</c>
/// references <see cref="Machine"/>. A unique key over (machine_id, name, code) prevents
/// duplicate property definitions.
/// </summary>
[Table("machine_properties")]
public class MachineProperty
{
    [Key]
    [Column("id")]
    public ulong Id { get; set; }

    [Required]
    [Column("machine_id")]
    public int MachineId { get; set; }

    [Required]
    [MaxLength(50)]
    [Column("machine_name")]
    public string MachineName { get; set; } = string.Empty;

    [Required]
    [MaxLength(100)]
    [Column("name")]
    public string Name { get; set; } = string.Empty;

    [Column("type")]
    public int? Type { get; set; } = 0;

    [Column("source")]
    public int? Source { get; set; } = 0;

    [MaxLength(100)]
    [Column("source_identifier")]
    public string? SourceIdentifier { get; set; }

    [Column("module")]
    public int? Module { get; set; } = 0;

    [Column("data_type")]
    public int? DataType { get; set; } = 0;

    [Column("value", TypeName = "text")]
    public string? Value { get; set; }

    [MaxLength(50)]
    [Column("code")]
    public string? Code { get; set; }

    [MaxLength(255)]
    [Column("location_address")]
    public string? LocationAddress { get; set; }

    [Column("enable_log")]
    public bool? EnableLog { get; set; } = false;

    [MaxLength(100)]
    [Column("log_table_name")]
    public string? LogTableName { get; set; }

    [Column("log_per_ms")]
    public uint? LogPerMs { get; set; } = 0;

    [MaxLength(50)]
    [Column("unit")]
    public string? Unit { get; set; }

    [ForeignKey("MachineId")]
    public virtual Machine? Machine { get; set; }

    public virtual SourceAvailable? SourceAvailable { get; set; }
}
