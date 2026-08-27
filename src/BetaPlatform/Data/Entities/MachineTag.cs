using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace BetaPlatform.Data.Entities;

/// <summary>
/// Per-machine live tag configuration. Ported from the reference SPackEdgeView schema.
/// Deleting the owning <see cref="Machine"/> cascades to its tags.
/// </summary>
[Table("machine_tags")]
public class MachineTag
{
    [Key]
    [Column("id")]
    public int Id { get; set; }

    [Required]
    [Column("machine_id")]
    public int MachineId { get; set; }

    [MaxLength(255)]
    [Column("machine_name")]
    public string? MachineName { get; set; }

    [Required]
    [MaxLength(255)]
    [Column("name")]
    public string Name { get; set; } = string.Empty;

    [Column("type")]
    public TagType Type { get; set; } = TagType.RQA;

    [Required]
    [Column("source")]
    public DataSourceType Source { get; set; }

    [MaxLength(255)]
    [Column("source_identifier")]
    public string? SourceIdentifier { get; set; }

    [Column("module")]
    public DataModule Module { get; set; } = DataModule.OEE;

    [Column("data_type")]
    public TagDataType DataType { get; set; } = TagDataType.Number;

    [Column("value", TypeName = "decimal(10,2)")]
    public decimal? Value { get; set; }

    [MaxLength(100)]
    [Column("code")]
    public string? Code { get; set; }

    [MaxLength(255)]
    [Column("location_address")]
    public string? LocationAddress { get; set; }

    [Column("enable_log")]
    public bool EnableLog { get; set; } = false;

    [MaxLength(100)]
    [Column("log_table_name")]
    public string? LogTableName { get; set; }

    [Column("log_per_ms")]
    public int LogPerMs { get; set; } = 1000;

    [MaxLength(50)]
    [Column("unit")]
    public string? Unit { get; set; }

    [ForeignKey("MachineId")]
    public virtual Machine? Machine { get; set; }
}
