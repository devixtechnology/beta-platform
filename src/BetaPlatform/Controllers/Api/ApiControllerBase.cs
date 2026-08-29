using Microsoft.AspNetCore.Mvc;
using BetaPlatform.Services.Api;

namespace BetaPlatform.Controllers.Api;

/// <summary>
/// Shared behaviour for the integration API controllers: the one mapping from a service outcome to
/// the status code that means it.
/// </summary>
/// <remarks>
/// <para>
/// In one place on purpose. SC-003 promises that invalid input, unauthorized, forbidden, not found
/// and conflict never share a code, so a caller can branch on the code without parsing message text.
/// A copy of this switch in each controller is a copy that can drift, and the first drift breaks
/// that promise silently.
/// </para>
/// <para>
/// Every branch exists now, including those the sample services cannot currently reach. The
/// behaviour slice makes them <em>reachable</em>; it does not add them (FR-034, SC-005).
/// </para>
/// </remarks>
public abstract class ApiControllerBase : ControllerBase
{
    /// <summary>Maps a non-success outcome to its response.</summary>
    protected IActionResult ToFailure(ApiOutcome outcome, string? error, string? fieldName) => outcome switch
    {
        // The addressed resource does not exist.
        ApiOutcome.NotFound => Problem(title: error, statusCode: StatusCodes.Status404NotFound),

        // A well-formed request the stored data disagrees with — distinct from a malformed one, so a
        // caller retries with a new code rather than fixing its payload.
        ApiOutcome.Conflict => Problem(title: error, statusCode: StatusCodes.Status409Conflict),

        // A field in the body is wrong — an unresolvable product code, say. Named in the errors
        // dictionary, because when a request carries two codes the caller must be told which one
        // failed (FR-023).
        ApiOutcome.Invalid => ValidationProblem(new ValidationProblemDetails(
            new Dictionary<string, string[]>
            {
                [fieldName ?? string.Empty] = [error ?? "The value is not valid."]
            })),

        _ => Problem(statusCode: StatusCodes.Status500InternalServerError)
    };

    /// <summary>Returns <paramref name="onSuccess"/> for a successful result, else the mapped failure.</summary>
    protected IActionResult FromResult<T>(ApiResult<T> result, Func<T, IActionResult> onSuccess) =>
        result.Outcome == ApiOutcome.Success && result.Value is not null
            ? onSuccess(result.Value)
            : ToFailure(result.Outcome, result.Error, result.FieldName);
}
