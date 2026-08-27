using System.Globalization;
using BetaPlatform;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging;

namespace BetaPlatform.Tests;

/// <summary>
/// Guards the shared-resource localization wiring end-to-end through the real DI stack. This is the
/// regression net for the Arabic bug: the resx satellite name must line up with the path the
/// <see cref="IStringLocalizer{SharedResource}"/> searches, otherwise lookups silently fall back to
/// the (English-looking) resource keys.
/// </summary>
public class LocalizationTests
{
    private static IStringLocalizer<SharedResource> BuildLocalizer()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddLocalization(options => options.ResourcesPath = "Resources");
        return services.BuildServiceProvider().GetRequiredService<IStringLocalizer<SharedResource>>();
    }

    [Fact]
    public void Arabic_Culture_Returns_Translated_Value_Not_The_Key()
    {
        var localizer = BuildLocalizer();
        var original = CultureInfo.CurrentUICulture;
        try
        {
            CultureInfo.CurrentUICulture = new CultureInfo("ar");
            var value = localizer["Dashboard"];

            Assert.False(value.ResourceNotFound, "The 'Dashboard' resource was not found — the resx satellite name does not match the localizer search path.");
            Assert.Equal("لوحة التحكم", value.Value);
            Assert.NotEqual("Dashboard", value.Value); // must not fall back to the key
        }
        finally
        {
            CultureInfo.CurrentUICulture = original;
        }
    }

    [Fact]
    public void English_Culture_Returns_Translated_Value()
    {
        var localizer = BuildLocalizer();
        var original = CultureInfo.CurrentUICulture;
        try
        {
            CultureInfo.CurrentUICulture = new CultureInfo("en");
            var value = localizer["Dashboard"];

            Assert.False(value.ResourceNotFound);
            Assert.Equal("Dashboard", value.Value);
        }
        finally
        {
            CultureInfo.CurrentUICulture = original;
        }
    }
}
