using BetaPlatform.Data.Entities;
using BetaPlatform.Services.Erp;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace BetaPlatform.Tests;

/// <summary>
/// The ERP credential store. One row, created on first save, and blank means "stop sending" rather
/// than "leave what was there".
/// </summary>
public class ErpSettingsServiceTests
{
    [Fact]
    public async Task GetAsync_Returns_An_Empty_Row_When_Nothing_Is_Stored()
    {
        using var db = TestDb.Create();
        var settings = await new ErpSettingsService(db).GetAsync();

        Assert.NotNull(settings);
        Assert.False(settings.HasToken);
        Assert.Null(settings.ApiToken);
    }

    [Fact]
    public async Task SaveTokenAsync_Creates_The_Singleton_Row_On_First_Save()
    {
        using var db = TestDb.Create();
        var svc = new ErpSettingsService(db);

        await svc.SaveTokenAsync("token-abc", "admin@beta.local");

        var rows = await db.ErpSettings.AsNoTracking().ToListAsync();
        var row = Assert.Single(rows);
        Assert.Equal(ErpSetting.SingletonId, row.ErpSettingId);
        Assert.Equal("token-abc", row.ApiToken);
        Assert.Equal("admin@beta.local", row.UpdatedBy);
    }

    [Fact]
    public async Task SaveTokenAsync_Updates_In_Place_And_Never_Adds_A_Second_Row()
    {
        using var db = TestDb.Create();
        var svc = new ErpSettingsService(db);

        await svc.SaveTokenAsync("first", "a@beta.local");
        await svc.SaveTokenAsync("second", "b@beta.local");

        Assert.Equal(1, await db.ErpSettings.CountAsync());
        Assert.Equal("second", await svc.GetApiTokenAsync());
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public async Task A_Blank_Token_Clears_The_Credential(string? blank)
    {
        using var db = TestDb.Create();
        var svc = new ErpSettingsService(db);
        await svc.SaveTokenAsync("token-abc", "admin@beta.local");

        await svc.SaveTokenAsync(blank, "admin@beta.local");

        Assert.Null(await svc.GetApiTokenAsync());
        Assert.False((await svc.GetAsync()).HasToken);
    }

    [Fact]
    public async Task A_Pasted_Token_Is_Trimmed()
    {
        using var db = TestDb.Create();
        var svc = new ErpSettingsService(db);

        // Copying a token out of an email brings whitespace with it; sending that as the header
        // value would fail against the ERP for a reason nobody could see on the screen.
        await svc.SaveTokenAsync("  token-abc\n", "admin@beta.local");

        Assert.Equal("token-abc", await svc.GetApiTokenAsync());
    }
}
