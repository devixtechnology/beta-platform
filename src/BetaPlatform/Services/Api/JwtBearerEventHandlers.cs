using System.Text.Json;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.IdentityModel.Tokens;
using BetaPlatform.Data.Entities;

namespace BetaPlatform.Services.Api;

/// <summary>
/// The bearer handler's behaviour: per-request revocation, and error bodies in the one shape the
/// rest of the API uses (FR-006, FR-008, FR-030, contracts/errors.md).
/// </summary>
/// <remarks>
/// Without the challenge and forbidden handlers below, the framework answers 401 and 403
/// with an <em>empty</em> body, which would break the promise that every failure shares one shape.
/// The decision logic is untouched — only the body is supplied.
/// </remarks>
public static class JwtBearerEventHandlers
{
    private const string ProblemJson = "application/problem+json";

    public static JwtBearerEvents Create() => new()
    {
        OnTokenValidated = RevalidateAccountAsync,
        OnChallenge = WriteChallengeAsync,
        OnForbidden = WriteForbiddenAsync
    };

    /// <summary>
    /// Re-checks the account behind a structurally valid token (FR-008). One primary-key lookup per
    /// request, and the only database work this feature performs.
    /// </summary>
    private static async Task RevalidateAccountAsync(TokenValidatedContext context)
    {
        var userId = context.Principal?.FindFirst("sub")?.Value;
        if (string.IsNullOrEmpty(userId))
        {
            context.Fail("The token carries no subject.");
            return;
        }

        // A refresh token opens nothing but POST /auth/refresh. It already fails this handler's
        // audience check, since refresh tokens carry a different audience by construction — this
        // says so explicitly, so the rule survives someone loosening that check later (research R12).
        var tokenUse = context.Principal?.FindFirst(ApiClaimTypes.TokenUse)?.Value;
        if (tokenUse != ApiClaimTypes.AccessTokenUse)
        {
            context.Fail("This is not an access token.");
            return;
        }

        var userManager = context.HttpContext.RequestServices
            .GetRequiredService<UserManager<ApplicationUser>>();

        var user = await userManager.FindByIdAsync(userId);
        var stamp = context.Principal?.FindFirst(ApiTokenValidation.SecurityStampClaimType)?.Value;

        if (!ApiTokenValidation.IsStillValid(user, stamp))
        {
            // Deliberately vague: a caller learns its token is no longer good, not why. The reasons
            // (deleted, deactivated, password changed) are not its business.
            context.Fail("The token is no longer valid for this account.");
        }
    }

    /// <summary>
    /// 401. The <c>WWW-Authenticate</c> description is what lets an integration tell an expired
    /// token (sign in again) from a malformed one (a real fault worth surfacing).
    /// </summary>
    private static Task WriteChallengeAsync(JwtBearerChallengeContext context)
    {
        // Suppress the framework's empty-bodied default; we answer in full below.
        context.HandleResponse();

        var response = context.Response;
        if (response.HasStarted)
        {
            return Task.CompletedTask;
        }

        var expired = IsExpiry(context.AuthenticateFailure);
        var noToken = context.AuthenticateFailure is null;

        response.StatusCode = StatusCodes.Status401Unauthorized;
        response.Headers.WWWAuthenticate = noToken
            ? "Bearer"
            : $"Bearer error=\"invalid_token\", error_description=\"{(expired ? "The token is expired." : "The token is invalid.")}\"";

        return WriteProblemAsync(
            response,
            StatusCodes.Status401Unauthorized,
            "https://tools.ietf.org/html/rfc9110#section-15.5.2",
            noToken ? "Authentication is required." : "The bearer token is not valid.");
    }

    /// <summary>403 — authenticated, but the roles on the token do not allow this operation.</summary>
    private static Task WriteForbiddenAsync(ForbiddenContext context)
    {
        var response = context.Response;
        if (response.HasStarted)
        {
            return Task.CompletedTask;
        }

        response.StatusCode = StatusCodes.Status403Forbidden;

        return WriteProblemAsync(
            response,
            StatusCodes.Status403Forbidden,
            "https://tools.ietf.org/html/rfc9110#section-15.5.4",
            "This account is not permitted to perform this operation.");
    }

    /// <summary>An expiry can arrive wrapped, so the inner exception is inspected too.</summary>
    private static bool IsExpiry(Exception? failure) => failure switch
    {
        null => false,
        SecurityTokenExpiredException => true,
        _ => failure.InnerException is SecurityTokenExpiredException
    };

    /// <summary>
    /// Writes the same three fields MVC's ProblemDetails carries. Nothing else may appear here —
    /// no stack trace, no database message, no record number (FR-031).
    /// </summary>
    private static Task WriteProblemAsync(HttpResponse response, int status, string type, string title)
    {
        response.ContentType = ProblemJson;

        var payload = JsonSerializer.Serialize(new
        {
            type,
            title,
            status
        });

        return response.WriteAsync(payload);
    }
}
