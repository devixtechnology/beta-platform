using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi.Models;

namespace BetaPlatform.Services.Api;

/// <summary>
/// Fills in the parts of the published contract the generator cannot infer from the code (FR-032,
/// FR-033).
/// </summary>
/// <remarks>
/// <para>
/// Two gaps are closed here. First, the <strong>slice note</strong>: an integrator reading only
/// <c>/openapi/v1.json</c> must be told which responses are representative rather than real, and
/// that statement has to travel inside the document rather than in a side file nobody opens.
/// </para>
/// <para>
/// Second, the <strong>security scheme</strong>: without it the document describes the endpoints
/// and no way to authenticate against any of them. ASP.NET Core 9 does not emit one from the
/// <c>[Authorize]</c> attributes on its own.
/// </para>
/// <para>
/// XML doc comments are not read by <c>Microsoft.AspNetCore.OpenApi</c> in .NET 9, which is why the
/// per-operation text comes from <c>[EndpointSummary]</c> / <c>[EndpointDescription]</c> on the
/// actions rather than from their <c>&lt;summary&gt;</c> blocks.
/// </para>
/// </remarks>
public sealed class OpenApiDocumentTransformer : IOpenApiDocumentTransformer
{
    private const string BearerScheme = "bearerAuth";

    public Task TransformAsync(
        OpenApiDocument document,
        OpenApiDocumentTransformerContext context,
        CancellationToken cancellationToken)
    {
        document.Info = new OpenApiInfo
        {
            Title = "Beta Platform Integration API",
            Version = "1.0",
            Description =
                "Sign in for a bearer token, renew it with a refresh token, read and add products, " +
                "and raise a work order. " +
                "Products are addressed by PRODUCT CODE; internal record numbers never appear in " +
                "this contract, in either direction.\n\n" +
                "SLICE NOTE: authentication, permissions and request validation are fully " +
                "implemented. The product and work-order operations return REPRESENTATIVE data and " +
                "persist nothing — responses documented as not-yet-produced (404 for an unknown " +
                "product code, 409 for a duplicate code or work-order number, 400 for an " +
                "unresolvable product code) are specified here and will be produced unchanged by " +
                "the follow-up behaviour slice."
        };

        document.Components ??= new OpenApiComponents();
        document.Components.SecuritySchemes[BearerScheme] = new OpenApiSecurityScheme
        {
            Type = SecuritySchemeType.Http,
            Scheme = "bearer",
            BearerFormat = "JWT",
            In = ParameterLocation.Header,
            Description =
                "8-hour token from POST /api/v1/auth/login. Renew it with POST /api/v1/auth/refresh " +
                "(which rotates the refresh token) or by signing in again. Deactivating the account " +
                "revokes an outstanding token — and stops its renewals — on the next request, " +
                "without waiting for expiry; changing a password does the same."
        };

        // Applied at the document level: every operation except sign-in requires it, and that one
        // carries its own empty requirement.
        document.SecurityRequirements.Add(new OpenApiSecurityRequirement
        {
            [new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = BearerScheme }
            }] = []
        });

        return Task.CompletedTask;
    }
}
