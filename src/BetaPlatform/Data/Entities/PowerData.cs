using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using BetaPlatform.Helpers;

namespace BetaPlatform.Data.Entities;

/// <summary>
/// Time-stamped machine energy record, written directly by the IoT team and READ-ONLY to this
/// application (FR-042/FR-051). Schema reproduces the reference SPackEdgeView <c>power_data</c>
/// table EXACTLY. Note: <c>timestamp</c> is datetime(3) (millisecond) here vs datetime(6) on
/// oee_data. See specs/001-phase-1-core/contracts/telemetry-db-contract.md.
/// </summary>
[Table("power_data")]
public class PowerData
{
    [Key]
    [Column("id")]
    public int Id { get; set; }

    [Required]
    [Column("machine_id")]
    public int MachineId { get; set; }

    [Required]
    [Column("timestamp")]
    public DateTime Timestamp { get; set; }

    [Column("kw_hr")]
    public decimal? KwHr { get; set; }

    [Column("v1")] public decimal? V1 { get; set; }
    [Column("v2")] public decimal? V2 { get; set; }
    [Column("v3")] public decimal? V3 { get; set; }
    [Column("v12")] public decimal? V12 { get; set; }
    [Column("v23")] public decimal? V23 { get; set; }
    [Column("v13")] public decimal? V13 { get; set; }
    [Column("a1")] public decimal? A1 { get; set; }
    [Column("a2")] public decimal? A2 { get; set; }
    [Column("a3")] public decimal? A3 { get; set; }
    [Column("a_avg")] public decimal? AAvg { get; set; }
    [Column("frequency")] public decimal? Frequency { get; set; }

    [Column("created_at")]
    public DateTime CreatedAt { get; set; } = TimeZoneHelper.GetKsaNow();

    [ForeignKey("MachineId")]
    public virtual Machine? Machine { get; set; }
}
