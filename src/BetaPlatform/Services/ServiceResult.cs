namespace BetaPlatform.Services;

/// <summary>
/// Lightweight result for service operations that can fail with a user-facing message
/// (e.g. duplicate code, invalid work-order transition, machine busy).
/// </summary>
public class ServiceResult
{
    public bool Success { get; init; }
    public string? Error { get; init; }

    public static ServiceResult Ok() => new() { Success = true };
    public static ServiceResult Fail(string error) => new() { Success = false, Error = error };
}

public class ServiceResult<T> : ServiceResult
{
    public T? Value { get; init; }

    public static ServiceResult<T> Ok(T value) => new() { Success = true, Value = value };
    public static new ServiceResult<T> Fail(string error) => new() { Success = false, Error = error };
}
