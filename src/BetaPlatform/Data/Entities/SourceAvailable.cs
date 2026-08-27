using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace BetaPlatform.Data.Entities;

/// <summary>
/// Registry of available data sources (ERP/MES/PLC/…). Ported from the reference SPackEdgeView
/// schema. Its unique <c>source_identifier</c> is the principal key referenced by
/// <see cref="MachineProperty"/>.
/// </summary>
[Table("sources_available")]
public class SourceAvailable
{
    [Key]
    [Column("id")]
    public int Id { get; set; }

    [Required]
    [Column("source")]
    public DataSourceType Source { get; set; }

    [Required]
    [MaxLength(255)]
    [Column("source_identifier")]
    public string SourceIdentifier { get; set; } = string.Empty;

    [Column("module")]
    public DataModule Module { get; set; } = DataModule.OEE;

    [Column("source_configuration", TypeName = "text")]
    public string? SourceConfiguration { get; set; }

    [Column("status")]
    public int Status { get; set; } = 0;

    [Column("enable")]
    public bool Enable { get; set; } = true;
}
