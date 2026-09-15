using Microsoft.EntityFrameworkCore;
using BetaPlatform.Data;
using BetaPlatform.Data.Entities;
using BetaPlatform.Helpers;

namespace BetaPlatform.Services.Erp;

/// <summary>Reads and writes the single <c>erp_settings</c> row.</summary>
public interface IErpSettingsService
{
    /// <summary>
    /// The stored settings. Never null — a site that has never opened the ERP Settings screen gets
    /// an unsaved row with no token, so every caller sees one shape.
    /// </summary>
    Task<ErpSetting> GetAsync(CancellationToken cancellationToken = default);

    /// <summary>The token to send as <c>X-API-Key</c>, or null when none is stored.</summary>
    Task<string?> GetApiTokenAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Stores the token, creating the row the first time. A blank value clears the token, which
    /// takes the integration offline deliberately rather than by accident.
    /// </summary>
    Task SaveTokenAsync(string? apiToken, string? updatedBy, CancellationToken cancellationToken = default);
}

public class ErpSettingsService : IErpSettingsService
{
    private readonly ApplicationDbContext _db;

    public ErpSettingsService(ApplicationDbContext db) => _db = db;

    public async Task<ErpSetting> GetAsync(CancellationToken cancellationToken = default) =>
        await _db.ErpSettings
            .AsNoTracking()
            .FirstOrDefaultAsync(e => e.ErpSettingId == ErpSetting.SingletonId, cancellationToken)
        ?? new ErpSetting();

    public async Task<string?> GetApiTokenAsync(CancellationToken cancellationToken = default)
    {
        var settings = await GetAsync(cancellationToken);
        return string.IsNullOrWhiteSpace(settings.ApiToken) ? null : settings.ApiToken.Trim();
    }

    public async Task SaveTokenAsync(
        string? apiToken,
        string? updatedBy,
        CancellationToken cancellationToken = default)
    {
        var stored = string.IsNullOrWhiteSpace(apiToken) ? null : apiToken.Trim();

        var existing = await _db.ErpSettings
            .FirstOrDefaultAsync(e => e.ErpSettingId == ErpSetting.SingletonId, cancellationToken);

        if (existing is null)
        {
            _db.ErpSettings.Add(new ErpSetting
            {
                ErpSettingId = ErpSetting.SingletonId,
                ApiToken = stored,
                UpdatedBy = updatedBy,
                UpdatedAt = TimeZoneHelper.GetKsaNow()
            });
        }
        else
        {
            existing.ApiToken = stored;
            existing.UpdatedBy = updatedBy;
            existing.UpdatedAt = TimeZoneHelper.GetKsaNow();
        }

        await _db.SaveChangesAsync(cancellationToken);
    }
}
