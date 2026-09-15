using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using BetaPlatform.Helpers;

namespace BetaPlatform.Data.Entities;

/// <summary>
/// The ERP integration credentials an administrator maintains from the ERP Settings screen.
/// </summary>
/// <remarks>
/// <para>
/// Exactly one row ever exists, keyed by <see cref="SingletonId"/>. A single-row table rather than a
/// key/value store because there is one thing to keep and a column says what it is; and a table
/// rather than <c>appsettings.json</c> because the token is rotated by an administrator, not by a
/// deployment. The base URL goes the other way — it is part of deploying to an environment, so it
/// stays in configuration (<see cref="Services.Erp.ErpOptions"/>).
/// </para>
/// </remarks>
[Table("erp_settings")]
public class ErpSetting
{
    /// <summary>The only key this table ever holds.</summary>
    public const int SingletonId = 1;

    [Key]
    [Column("erp_setting_id")]
    public int ErpSettingId { get; set; } = SingletonId;

    /// <summary>
    /// The value sent as the <c>X-API-Key</c> header on every ERP call. Null or blank means the
    /// integration is not credentialed yet; calls are refused before a request is built rather than
    /// sent unauthenticated for the ERP to reject.
    /// </summary>
    [MaxLength(500)]
    [Column("api_token")]
    public string? ApiToken { get; set; }

    [Column("updated_at")]
    public DateTime UpdatedAt { get; set; } = TimeZoneHelper.GetKsaNow();

    /// <summary>Who last saved the token, so a credential change has a name against it.</summary>
    [MaxLength(256)]
    [Column("updated_by")]
    public string? UpdatedBy { get; set; }

    /// <summary>True when a token has been stored and ERP calls can carry credentials.</summary>
    [NotMapped]
    public bool HasToken => !string.IsNullOrWhiteSpace(ApiToken);
}
