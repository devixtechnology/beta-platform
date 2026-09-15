using System.ComponentModel.DataAnnotations;

namespace BetaPlatform.ViewModels.ErpSettings;

/// <summary>
/// The ERP Settings screen: the one editable credential, plus read-only context so an administrator
/// can tell at a glance whether the integration is live.
/// </summary>
public class ErpSettingsViewModel
{
    /// <summary>
    /// The value sent as <c>X-API-Key</c> on every outbound ERP call. Optional — clearing it takes
    /// the integration offline, which is a legitimate thing to want.
    /// </summary>
    [MaxLength(500, ErrorMessage = "The API token cannot be longer than 500 characters.")]
    [Display(Name = "ErpApiToken")]
    public string? ApiToken { get; set; }

    /// <summary>The configured <c>Erp:BaseUrl</c>. Shown, never edited here: it belongs to the
    /// deployment, not to the administrator.</summary>
    public string? BaseUrl { get; set; }

    /// <summary>True when no base URL is configured, so calls are being mocked.</summary>
    public bool IsMocked => string.IsNullOrWhiteSpace(BaseUrl);

    public bool HasToken { get; set; }

    public DateTime? UpdatedAt { get; set; }

    public string? UpdatedBy { get; set; }
}
