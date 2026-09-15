namespace BetaPlatform.Services.Erp;

/// <summary>
/// Where the upstream ERP lives, bound from the <c>Erp</c> configuration section.
/// </summary>
/// <remarks>
/// <para>
/// <see cref="BaseUrl"/> is the switch between a real integration and a mocked one. Left blank —
/// which is how <c>appsettings.json</c> ships — every outbound call is answered by
/// <see cref="MockErpClient"/>: the payload is logged, nothing leaves the machine, and the work
/// order still starts, holds and finishes exactly as before. A site that has an ERP fills the
/// value in and the same calls go out over HTTP. Nothing else in the platform changes.
/// </para>
/// <para>
/// The API token deliberately does <em>not</em> live here. It is a credential an administrator
/// rotates without a deployment, so it is stored in the database behind the ERP Settings screen —
/// see <see cref="Data.Entities.ErpSetting"/>.
/// </para>
/// </remarks>
public class ErpOptions
{
    public const string SectionName = "Erp";

    /// <summary>
    /// Root of the ERP, e.g. <c>https://erp.example.com</c>. The <c>/api/upward/v1/...</c> paths are
    /// appended to it. Blank or whitespace means "no ERP configured" — see <see cref="IsConfigured"/>.
    /// </summary>
    public string? BaseUrl { get; set; }

    /// <summary>How long to wait for the ERP before giving up. Kept short: a slow ERP must never
    /// hold up an operator pressing Start on the shop floor.</summary>
    public int TimeoutSeconds { get; set; } = 15;

    /// <summary>True when a real ERP is configured and calls should go out over HTTP.</summary>
    public bool IsConfigured => !string.IsNullOrWhiteSpace(BaseUrl);

    public TimeSpan Timeout => TimeSpan.FromSeconds(TimeoutSeconds <= 0 ? 15 : TimeoutSeconds);
}
