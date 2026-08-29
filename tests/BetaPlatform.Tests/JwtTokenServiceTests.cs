using System.Security.Claims;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using BetaPlatform.Data.Entities;
using BetaPlatform.Services.Api;
using Xunit;

namespace BetaPlatform.Tests;

/// <summary>
/// Token issuing (005 FR-001, FR-002, FR-007). Every claim the contract promises must be present:
/// an integration reads these, and a missing role claim silently costs the caller its permissions.
/// </summary>
public class JwtTokenServiceTests
{
    private const string TestKey = "unit-test-signing-key-0123456789abcdefghijklmnop";

    private static JwtTokenService NewService(int lifetimeHours = 8, int refreshLifetimeDays = 30) =>
        new(Options.Create(new JwtOptions
        {
            Issuer = "TestIssuer",
            Audience = "TestAudience",
            SigningKey = TestKey,
            LifetimeHours = lifetimeHours,
            RefreshLifetimeDays = refreshLifetimeDays
        }));

    private static ApplicationUser NewUser() => new()
    {
        Id = "user-1",
        Email = "admin@beta.local",
        FullName = "Beta Administrator",
        SecurityStamp = "STAMP-ABC",
        IsActive = true
    };

    private static JsonWebToken Read(string token) => new JsonWebTokenHandler().ReadJsonWebToken(token);

    [Fact]
    public void Issue_Carries_Subject_Email_And_Name()
    {
        var issued = NewService().Issue(NewUser(), ["Admin"]);

        var token = Read(issued.Access.Token);

        Assert.Equal("user-1", token.GetClaim("sub").Value);
        Assert.Equal("admin@beta.local", token.GetClaim(ApiClaimTypes.Email).Value);
        Assert.Equal("Beta Administrator", token.GetClaim(ApiClaimTypes.Name).Value);
    }

    [Fact]
    public void Issue_Carries_One_Role_Claim_Per_Role()
    {
        var issued = NewService().Issue(NewUser(), ["Admin", "Client"]);

        var roles = Read(issued.Access.Token).Claims
            .Where(c => c.Type == ApiClaimTypes.Role)
            .Select(c => c.Value)
            .ToList();

        Assert.Equal(2, roles.Count);
        Assert.Contains("Admin", roles);
        Assert.Contains("Client", roles);
    }

    [Fact]
    public void Issue_Carries_The_Security_Stamp_So_The_Token_Can_Be_Revoked()
    {
        // Without this claim, deactivation could not bite before the token's natural expiry (FR-008).
        var issued = NewService().Issue(NewUser(), []);

        Assert.Equal("STAMP-ABC", Read(issued.Access.Token).GetClaim(ApiTokenValidation.SecurityStampClaimType).Value);
    }

    [Fact]
    public void Issue_Expires_After_The_Configured_Lifetime()
    {
        var before = DateTime.UtcNow;

        var issued = NewService(lifetimeHours: 8).Issue(NewUser(), []);

        // Eight hours out, allowing a couple of seconds for the test itself.
        Assert.InRange(issued.Access.ExpiresAt, before.AddHours(8).AddSeconds(-5), before.AddHours(8).AddSeconds(5));
    }

    [Fact]
    public void Issue_Reports_The_Expiry_The_Token_Actually_Carries()
    {
        // JWT exp is whole seconds. If the reported ExpiresAt were not truncated to match, a caller
        // scheduling its own renewal against it would be wrong by up to a second.
        var issued = NewService().Issue(NewUser(), []);

        Assert.Equal(issued.Access.ExpiresAt, Read(issued.Access.Token).ValidTo, TimeSpan.FromSeconds(1));
    }

    [Fact]
    public void Issue_Sets_The_Configured_Issuer_And_Audience()
    {
        var issued = NewService().Issue(NewUser(), []);

        var token = Read(issued.Access.Token);

        Assert.Equal("TestIssuer", token.Issuer);
        Assert.Contains("TestAudience", token.Audiences);
    }

    [Fact]
    public void Issue_Gives_Every_Token_A_Distinct_Identifier()
    {
        // Two tokens minted in the same second must still be distinguishable.
        var service = NewService();
        var user = NewUser();

        var first = Read(service.Issue(user, []).Access.Token).GetClaim("jti").Value;
        var second = Read(service.Issue(user, []).Access.Token).GetClaim("jti").Value;

        Assert.NotEqual(first, second);
    }

    [Fact]
    public void Issue_Puts_No_Secret_In_The_Token()
    {
        // The token is signed, not encrypted — anyone holding it can read every claim.
        var user = NewUser();
        user.PasswordHash = "SUPER-SECRET-HASH";

        var issued = NewService().Issue(user, ["Admin"]);

        Assert.DoesNotContain("SUPER-SECRET-HASH", Read(issued.Access.Token).Claims.Select(c => c.Value));
    }

    // ---- Refresh tokens (FR-036 … FR-040, research R12) ----

    [Fact]
    public void Issue_Also_Returns_A_Refresh_Token()
    {
        var issued = NewService().Issue(NewUser(), ["Admin"]);

        Assert.False(string.IsNullOrWhiteSpace(issued.Refresh.Token));
        Assert.NotEqual(issued.Access.Token, issued.Refresh.Token);
    }

    [Fact]
    public void Refresh_Token_Outlives_The_Access_Token_It_Renews()
    {
        var issued = NewService(lifetimeHours: 8, refreshLifetimeDays: 30).Issue(NewUser(), []);
        var before = DateTime.UtcNow;

        Assert.True(issued.Refresh.ExpiresAt > issued.Access.ExpiresAt);
        Assert.InRange(
            issued.Refresh.ExpiresAt,
            before.AddDays(30).AddSeconds(-5),
            before.AddDays(30).AddSeconds(5));
    }

    [Fact]
    public void Refresh_Token_Carries_No_Roles()
    {
        // Roles are re-read from the store at renewal (FR-039). A role baked into a 30-day token
        // would outlive its withdrawal by up to a month.
        var issued = NewService().Issue(NewUser(), ["Admin", "Client"]);

        Assert.DoesNotContain(Read(issued.Refresh.Token).Claims, c => c.Type == ApiClaimTypes.Role);
    }

    [Fact]
    public void Refresh_Token_Carries_The_Security_Stamp_So_Renewals_Can_Be_Revoked()
    {
        var issued = NewService().Issue(NewUser(), []);

        Assert.Equal(
            "STAMP-ABC",
            Read(issued.Refresh.Token).GetClaim(ApiTokenValidation.SecurityStampClaimType).Value);
    }

    [Fact]
    public void The_Two_Tokens_Are_Marked_And_Addressed_Differently()
    {
        // The audience is what keeps them apart mechanically; token_use is what says so in words.
        var issued = NewService().Issue(NewUser(), []);

        var access = Read(issued.Access.Token);
        var refresh = Read(issued.Refresh.Token);

        Assert.Equal(ApiClaimTypes.AccessTokenUse, access.GetClaim(ApiClaimTypes.TokenUse).Value);
        Assert.Equal(ApiClaimTypes.RefreshTokenUse, refresh.GetClaim(ApiClaimTypes.TokenUse).Value);
        Assert.Contains("TestAudience", access.Audiences);
        Assert.Contains("TestAudience.refresh", refresh.Audiences);
        Assert.DoesNotContain("TestAudience", refresh.Audiences);
    }

    [Fact]
    public async Task ValidateRefreshToken_Accepts_A_Genuine_Refresh_Token()
    {
        var service = NewService();

        var issued = service.Issue(NewUser(), ["Admin"]);
        var subject = await service.ValidateRefreshTokenAsync(issued.Refresh.Token);

        Assert.NotNull(subject);
        Assert.Equal("user-1", subject!.UserId);
        Assert.Equal("STAMP-ABC", subject.SecurityStamp);
    }

    [Fact]
    public async Task ValidateRefreshToken_Refuses_An_Access_Token()
    {
        // The whole point of the separate audience: a stolen access token cannot be laundered into
        // a fresh 8-hour one after its own expiry.
        var service = NewService();

        var issued = service.Issue(NewUser(), ["Admin"]);

        Assert.Null(await service.ValidateRefreshTokenAsync(issued.Access.Token));
    }

    [Fact]
    public async Task ValidateRefreshToken_Refuses_A_Token_Signed_With_Another_Key()
    {
        var other = new JwtTokenService(Options.Create(new JwtOptions
        {
            Issuer = "TestIssuer",
            Audience = "TestAudience",
            SigningKey = "a-completely-different-signing-key-0123456789",
            LifetimeHours = 8
        }));

        var forged = other.Issue(NewUser(), []).Refresh.Token;

        Assert.Null(await NewService().ValidateRefreshTokenAsync(forged));
    }

    [Fact]
    public async Task ValidateRefreshToken_Refuses_An_Altered_Token()
    {
        var service = NewService();
        var issued = service.Issue(NewUser(), []);

        // Flip a character in the payload segment; the signature no longer covers it.
        var parts = issued.Refresh.Token.Split('.');
        parts[1] = (parts[1][0] == 'A' ? "B" : "A") + parts[1][1..];

        Assert.Null(await service.ValidateRefreshTokenAsync(string.Join('.', parts)));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("not-a-token")]
    public async Task ValidateRefreshToken_Refuses_Rubbish(string presented)
    {
        // A caller sending nonsense gets the same null the forged token gets — and, above it, the
        // same 401 — rather than an exception escaping as a 500.
        Assert.Null(await NewService().ValidateRefreshTokenAsync(presented));
    }
}
