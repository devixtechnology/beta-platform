using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using BetaPlatform.Data;
using BetaPlatform.Services.Erp;
using BetaPlatform.ViewModels.ErpSettings;

namespace BetaPlatform.Controllers;

/// <summary>
/// Maintains the credential the platform uses to call the upstream ERP. Administrator-only: the
/// token is a shared secret, and anyone holding it can post manufacturing-order events upstream.
/// </summary>
/// <remarks>
/// Thin by the constitution's Principle II — it maps and delegates; <see cref="IErpSettingsService"/>
/// owns the storage rule. The base URL is displayed but never editable here: it belongs to the
/// deployment (<c>Erp:BaseUrl</c>), not to an administrator.
/// </remarks>
[Authorize(Roles = DbSeeder.AdminRole)]
public class ErpSettingsController : Controller
{
    private readonly IErpSettingsService _settings;
    private readonly IOptionsMonitor<ErpOptions> _options;

    public ErpSettingsController(IErpSettingsService settings, IOptionsMonitor<ErpOptions> options)
    {
        _settings = settings;
        _options = options;
    }

    [HttpGet]
    public async Task<IActionResult> Index()
    {
        var stored = await _settings.GetAsync();

        return View(new ErpSettingsViewModel
        {
            ApiToken = stored.ApiToken,
            BaseUrl = _options.CurrentValue.BaseUrl,
            HasToken = stored.HasToken,
            UpdatedAt = stored.UpdatedAt == default ? null : stored.UpdatedAt,
            UpdatedBy = stored.UpdatedBy
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Index(ErpSettingsViewModel model)
    {
        if (!ModelState.IsValid)
        {
            model.BaseUrl = _options.CurrentValue.BaseUrl;
            return View(model);
        }

        await _settings.SaveTokenAsync(model.ApiToken, User.Identity?.Name);

        TempData["Success"] = string.IsNullOrWhiteSpace(model.ApiToken)
            ? "ERP API token cleared. Outbound calls will not be sent until a token is stored."
            : "ERP API token saved.";

        return RedirectToAction(nameof(Index));
    }
}
