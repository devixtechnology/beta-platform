using BetaPlatform.ViewModels.Api;

namespace BetaPlatform.Services.Api;

/// <summary>
/// The work-order operations the API exposes.
/// </summary>
/// <remarks>
/// The second half of the seam described in research R7. Satisfied here by
/// <see cref="SampleWorkOrderApiService"/>; the behaviour slice registers an implementation that
/// resolves product codes and delegates to the existing <see cref="IWorkOrderService"/>.
/// </remarks>
public interface IWorkOrderApiService
{
    /// <summary>
    /// Creates a work order in the initial <c>Ready</c> state.
    /// </summary>
    /// <remarks>
    /// Answers <see cref="ApiOutcome.Invalid"/> — naming <c>inputProductCode</c> or
    /// <c>outputProductCode</c> — when a code resolves to no product, and
    /// <see cref="ApiOutcome.Conflict"/> when the order number is already in use. Naming the field
    /// matters: a request carrying two codes must tell the caller which one to fix (FR-023).
    /// </remarks>
    Task<ApiResult<WorkOrderResponse>> CreateAsync(CreateWorkOrderRequest request);
}
