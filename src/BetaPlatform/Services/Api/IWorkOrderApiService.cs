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
    /// Answers <see cref="ApiOutcome.Invalid"/> — naming the offending field as
    /// <c>inputProductCodes[i]</c>, at the position submitted, or as <c>outputProductCode</c> —
    /// when a code resolves to no product, and <see cref="ApiOutcome.Conflict"/> when the order
    /// number is already in use. Naming the field matters, and the index with it: a request
    /// carrying a list of input codes must tell the caller <em>which entry</em> to fix (FR-023).
    /// </remarks>
    Task<ApiResult<WorkOrderResponse>> CreateAsync(CreateWorkOrderRequest request);
}
