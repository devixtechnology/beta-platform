namespace BetaPlatform.Services;

/// <summary>
/// Telemetry reading options, bound from the <c>Telemetry</c> configuration section (research D4).
/// Configurable so a site whose IoT writer has a different cadence is corrected in
/// <c>appsettings.json</c> rather than in a release.
/// </summary>
public class TelemetryOptions
{
    public const string SectionName = "Telemetry";

    /// <summary>How old the latest reading may be before running state falls back to
    /// <see cref="Data.Entities.MachineRunningState.Unknown"/>. Default 5 minutes.</summary>
    public int StaleAfterMinutes { get; set; } = 5;

    public TimeSpan StaleAfter => TimeSpan.FromMinutes(StaleAfterMinutes);
}
