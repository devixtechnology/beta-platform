using BetaPlatform.ViewModels.Api;

namespace BetaPlatform.Services.Api;

/// <summary>
/// The machine operations the API exposes.
/// </summary>
/// <remarks>
/// Same seam as <see cref="IProductApiService"/> and <see cref="IWorkOrderApiService"/>: the
/// controller depends on this, the implementation delegates to <see cref="IMachineService"/> and
/// <see cref="IMachineStatusService"/>, and the API never reaches the database itself.
/// </remarks>
public interface IMachineApiService
{
    /// <summary>
    /// Every machine, each with its type, its production line and its live running state,
    /// optionally restricted to active machines.
    /// </summary>
    Task<ApiResult<IReadOnlyList<MachineResponse>>> GetAllAsync(bool activeOnly);
}
