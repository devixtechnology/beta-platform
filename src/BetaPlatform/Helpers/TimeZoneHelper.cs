namespace BetaPlatform.Helpers;

/// <summary>
/// Central helper for the factory's local (KSA, Asia/Riyadh, UTC+3) time.
/// Used for <c>created_at</c>/timestamp defaults so all persisted times are consistent,
/// matching the reference SPackEdgeView project.
/// </summary>
public static class TimeZoneHelper
{
    private static readonly TimeZoneInfo KsaTimeZone = ResolveKsaTimeZone();

    private static TimeZoneInfo ResolveKsaTimeZone()
    {
        // "Arab Standard Time" (Windows) / "Asia/Riyadh" (Linux) — both are UTC+3, no DST.
        foreach (var id in new[] { "Arab Standard Time", "Asia/Riyadh" })
        {
            try { return TimeZoneInfo.FindSystemTimeZoneById(id); }
            catch (TimeZoneNotFoundException) { }
            catch (InvalidTimeZoneException) { }
        }
        return TimeZoneInfo.CreateCustomTimeZone("KSA", TimeSpan.FromHours(3), "KSA", "KSA");
    }

    public static DateTime GetKsaNow() => TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, KsaTimeZone);
}
