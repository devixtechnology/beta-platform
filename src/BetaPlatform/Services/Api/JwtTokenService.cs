using System.Security.Claims;
using System.Text;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using BetaPlatform.Data.Entities;

namespace BetaPlatform.Services.Api;

/// <summary>A signed token and the moment it stops being valid.</summary>
public record IssuedToken(string Token, DateTime ExpiresAt);

/// <summary>
/// What a sign-in or a renewal hands back: the token that opens the API, and the longer-lived one
/// that buys the next pair (FR-036).
/// </summary>
public record IssuedTokenPair(IssuedToken Access, IssuedToken Refresh);

/// <summary>
/// The account a presented refresh token names, once the token itself has been proven genuine.
/// Carries no roles: they are re-read from the store at renewal, so a role granted or withdrawn
/// since sign-in takes effect on the next renewal rather than 30 days later (FR-039).
/// </summary>
public record RefreshTokenSubject(string UserId, string? SecurityStamp);

public interface IJwtTokenService
{
    /// <summary>Issues an access token and its refresh token for an authenticated, active account.</summary>
    IssuedTokenPair Issue(ApplicationUser user, IEnumerable<string> roles);

    /// <summary>
    /// Verifies a presented refresh token — signature, issuer, refresh audience, lifetime, and that
    /// it is a refresh token rather than an access token — and returns the account it names, or
    /// <c>null</c> when it is not genuine. Whether that account may still use it is a separate
    /// question, answered against the store by the caller.
    /// </summary>
    Task<RefreshTokenSubject?> ValidateRefreshTokenAsync(string refreshToken);
}

/// <summary>
/// Issues the bearer tokens the integration API accepts, and verifies the refresh tokens it renews
/// them with (FR-001, FR-002, FR-007, FR-036 … FR-041).
/// </summary>
/// <remarks>
/// The token is <em>signed, not encrypted</em>: anyone holding it can read every claim, so nothing
/// secret goes in one. The claims here are only what the caller could already learn about itself,
/// plus the security stamp used to revoke it.
/// </remarks>
public class JwtTokenService : IJwtTokenService
{
    private readonly JwtOptions _options;

    public JwtTokenService(IOptions<JwtOptions> options) => _options = options.Value;

    public IssuedTokenPair Issue(ApplicationUser user, IEnumerable<string> roles)
    {
        // JWT exp/iat/nbf are whole seconds. Truncating here means the expiresAt we report is the
        // expiry the token actually carries, rather than up to a second later.
        var now = DateTime.UtcNow;
        var issuedAt = new DateTime(now.Ticks - (now.Ticks % TimeSpan.TicksPerSecond), DateTimeKind.Utc);

        var stamp = user.SecurityStamp ?? string.Empty;

        var accessClaims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, user.Id),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString("N")),
            new(ApiClaimTypes.Email, user.Email ?? string.Empty),
            new(ApiClaimTypes.Name, user.FullName),
            new(ApiClaimTypes.TokenUse, ApiClaimTypes.AccessTokenUse),

            // Compared against the store on every request so deactivation revokes this token on the
            // caller's next call rather than in eight hours (FR-008).
            new(ApiTokenValidation.SecurityStampClaimType, stamp)
        };

        // One claim per role, so a later permission decision needs no second look-up (FR-007).
        accessClaims.AddRange(roles.Select(role => new Claim(ApiClaimTypes.Role, role)));

        var access = Create(accessClaims, _options.Audience, issuedAt, issuedAt.Add(_options.Lifetime));

        // The refresh token is deliberately thinner: no email, no name, and above all no roles. It
        // proves who the caller is and nothing about what it may do — the renewal re-reads that.
        var refreshClaims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, user.Id),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString("N")),
            new(ApiClaimTypes.TokenUse, ApiClaimTypes.RefreshTokenUse),
            new(ApiTokenValidation.SecurityStampClaimType, stamp)
        };

        var refresh = Create(refreshClaims, _options.RefreshAudience, issuedAt, issuedAt.Add(_options.RefreshLifetime));

        return new IssuedTokenPair(access, refresh);
    }

    public async Task<RefreshTokenSubject?> ValidateRefreshTokenAsync(string refreshToken)
    {
        if (string.IsNullOrWhiteSpace(refreshToken))
        {
            return null;
        }

        var parameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = _options.Issuer,

            // The refresh audience, never the access one. An access token presented here fails on
            // this line, before any claim is read (research R12).
            ValidateAudience = true,
            ValidAudience = _options.RefreshAudience,

            ValidateIssuerSigningKey = true,
            IssuerSigningKey = SigningKey(),
            ValidateLifetime = true,

            // Same zero skew as the bearer handler: a token expires when it says it does.
            ClockSkew = TimeSpan.Zero,
            NameClaimType = ApiClaimTypes.Name,
            RoleClaimType = ApiClaimTypes.Role
        };

        TokenValidationResult result;
        try
        {
            result = await new JsonWebTokenHandler().ValidateTokenAsync(refreshToken, parameters);
        }
        catch (Exception)
        {
            // A string that is not a token at all throws rather than returning a failed result.
            // Both mean the same thing to the caller, and neither is worth a log entry an anonymous
            // caller can fill at will.
            return null;
        }

        if (!result.IsValid)
        {
            return null;
        }

        var identity = result.ClaimsIdentity;

        // Belt and braces over the audience check above: a token that is not marked for renewal is
        // not accepted for renewal, whatever else it carries.
        if (identity?.FindFirst(ApiClaimTypes.TokenUse)?.Value != ApiClaimTypes.RefreshTokenUse)
        {
            return null;
        }

        var userId = identity.FindFirst(JwtRegisteredClaimNames.Sub)?.Value;
        if (string.IsNullOrEmpty(userId))
        {
            return null;
        }

        return new RefreshTokenSubject(
            userId,
            identity.FindFirst(ApiTokenValidation.SecurityStampClaimType)?.Value);
    }

    /// <summary>Signs one token. The only place a descriptor is built, so both kinds agree.</summary>
    private IssuedToken Create(IEnumerable<Claim> claims, string audience, DateTime issuedAt, DateTime expiresAt)
    {
        var descriptor = new SecurityTokenDescriptor
        {
            Issuer = _options.Issuer,
            Audience = audience,
            IssuedAt = issuedAt,
            NotBefore = issuedAt,
            Expires = expiresAt,
            Subject = new ClaimsIdentity(claims),
            SigningCredentials = new SigningCredentials(SigningKey(), SecurityAlgorithms.HmacSha256)
        };

        return new IssuedToken(new JsonWebTokenHandler().CreateToken(descriptor), expiresAt);
    }

    private SymmetricSecurityKey SigningKey() =>
        new(Encoding.UTF8.GetBytes(_options.SigningKey));
}
