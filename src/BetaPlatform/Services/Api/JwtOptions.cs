using System.Text;

namespace BetaPlatform.Services.Api;

/// <summary>
/// Bearer-token options, bound from the <c>Jwt</c> configuration section (005 research R4).
/// Follows the <see cref="TelemetryOptions"/> pattern: a site changes these in configuration
/// rather than in a release.
/// </summary>
public class JwtOptions
{
    public const string SectionName = "Jwt";

    /// <summary>The signing key shipped in <c>appsettings.Development.json</c>. Production refuses
    /// to start on this value, so committing it cannot weaken a real deployment.</summary>
    public const string DevelopmentPlaceholderKey = "dev-only-do-not-use-in-production-0123456789abcdef";

    /// <summary>HMAC-SHA256 keys shorter than the hash output are rejected by the token library at
    /// runtime with an error that reads as a configuration mystery. Catching it at startup turns
    /// that into a sentence.</summary>
    public const int MinimumSigningKeyBytes = 32;

    public string Issuer { get; set; } = "BetaPlatform";

    public string Audience { get; set; } = "BetaPlatformApi";

    public string SigningKey { get; set; } = string.Empty;

    /// <summary>One plant shift (FR-002). A caller renews with the refresh token issued alongside
    /// it, or by signing in again (FR-003, FR-036).</summary>
    public int LifetimeHours { get; set; } = 8;

    public TimeSpan Lifetime => TimeSpan.FromHours(LifetimeHours);

    /// <summary>
    /// How long a refresh token stays usable (FR-037). Long enough that an unattended integration
    /// survives a weekend without a stored password, short enough that a leaked one dies.
    /// </summary>
    public int RefreshLifetimeDays { get; set; } = 30;

    public TimeSpan RefreshLifetime => TimeSpan.FromDays(RefreshLifetimeDays);

    /// <summary>
    /// The audience refresh tokens carry, distinct from <see cref="Audience"/> by construction.
    /// This is what stops a refresh token being presented as an access token: the bearer handler
    /// validates the access audience and rejects this one before any claim is read (research R12).
    /// </summary>
    public string RefreshAudience => $"{Audience}.refresh";

    /// <summary>
    /// Returns the reason this configuration is unusable, or <c>null</c> when it is sound. Kept a
    /// pure function so startup can fail loudly (research R4) and tests can exercise every branch
    /// without a host.
    /// </summary>
    public string? Validate(bool isProduction)
    {
        if (string.IsNullOrWhiteSpace(SigningKey))
        {
            return $"{SectionName}:SigningKey is not configured. Supply it via the environment " +
                   $"({SectionName}__SigningKey) or a secret store — the application cannot issue tokens without it.";
        }

        if (Encoding.UTF8.GetByteCount(SigningKey) < MinimumSigningKeyBytes)
        {
            return $"{SectionName}:SigningKey must be at least {MinimumSigningKeyBytes} bytes; " +
                   "a shorter key cannot sign an HMAC-SHA256 token.";
        }

        // Whoever holds this key mints tokens for any account and any role. A convenience default
        // that is safe in development must be impossible in production — the same rule DbSeeder
        // applies to the administrator password.
        if (isProduction && SigningKey == DevelopmentPlaceholderKey)
        {
            return $"{SectionName}:SigningKey is still the development placeholder. Production must " +
                   "supply its own key.";
        }

        if (LifetimeHours <= 0)
        {
            return $"{SectionName}:LifetimeHours must be greater than zero.";
        }

        if (RefreshLifetimeDays <= 0)
        {
            return $"{SectionName}:RefreshLifetimeDays must be greater than zero.";
        }

        // A refresh token that dies before the access token it renews can never be used, which
        // would look like an intermittent 401 rather than a configuration mistake.
        if (RefreshLifetime <= Lifetime)
        {
            return $"{SectionName}:RefreshLifetimeDays must outlast {SectionName}:LifetimeHours; " +
                   "a refresh token that expires before the access token it renews is unusable.";
        }

        if (string.IsNullOrWhiteSpace(Issuer) || string.IsNullOrWhiteSpace(Audience))
        {
            return $"{SectionName}:Issuer and {SectionName}:Audience must both be configured.";
        }

        return null;
    }
}
