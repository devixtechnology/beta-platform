namespace BetaPlatform.Services.Api;

/// <summary>
/// How an API operation ended. The controller maps each value to exactly one status code, so a
/// caller branches on the code and never parses message text (SC-003).
/// </summary>
public enum ApiOutcome
{
    /// <summary>200 / 201.</summary>
    Success,

    /// <summary>404 — the addressed resource does not exist.</summary>
    NotFound,

    /// <summary>409 — a well-formed request the stored data disagrees with (code or number in use).</summary>
    Conflict,

    /// <summary>400 — a field in the body is wrong, e.g. a product code that resolves to nothing.</summary>
    Invalid
}

/// <summary>
/// The outcome of an API operation, carrying enough for the controller to answer without inspecting
/// message text.
/// </summary>
/// <remarks>
/// <para>
/// A deliberate departure from research R7, which proposed reusing <see cref="ServiceResult{T}"/>.
/// That type carries only a <c>bool</c> and a message, which cannot express the difference between
/// 404, 409 and 400 — and encoding all three response branches <em>now</em> is precisely what
/// FR-034 and SC-005 require, since the follow-up behaviour slice must not touch a controller.
/// Reusing <c>ServiceResult</c> would have forced the controller to infer the status code from the
/// error string, which is the fragility this type exists to avoid.
/// </para>
/// <para>
/// <see cref="FieldName"/> is what lets an <see cref="ApiOutcome.Invalid"/> result name the offending
/// field: when a work order fails because one of two product codes does not resolve, the caller has
/// to be told <em>which</em> one (FR-023).
/// </para>
/// </remarks>
public class ApiResult<T>
{
    public ApiOutcome Outcome { get; init; }

    public T? Value { get; init; }

    /// <summary>Human-readable reason. Never the sole carrier of meaning — the outcome is.</summary>
    public string? Error { get; init; }

    /// <summary>For <see cref="ApiOutcome.Invalid"/>: the request field at fault, so the response
    /// can name it in the validation errors dictionary.</summary>
    public string? FieldName { get; init; }

    public static ApiResult<T> Ok(T value) => new() { Outcome = ApiOutcome.Success, Value = value };

    public static ApiResult<T> NotFound(string error) =>
        new() { Outcome = ApiOutcome.NotFound, Error = error };

    public static ApiResult<T> Conflict(string error) =>
        new() { Outcome = ApiOutcome.Conflict, Error = error };

    public static ApiResult<T> Invalid(string fieldName, string error) =>
        new() { Outcome = ApiOutcome.Invalid, FieldName = fieldName, Error = error };
}
