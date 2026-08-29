using BetaPlatform.ViewModels.Api;

namespace BetaPlatform.Services.Api;

/// <summary>
/// The product operations the API exposes, in API terms.
/// </summary>
/// <remarks>
/// <para>
/// This interface is the seam described in research R7. In this slice it is satisfied by
/// <see cref="SampleProductApiService"/>, which returns representative data and stores nothing; the
/// follow-up behaviour slice registers an implementation that delegates to the existing
/// <see cref="IProductService"/>. Swapping them is a DI registration — no route, DTO, or status code
/// moves (FR-034, SC-005).
/// </para>
/// <para>
/// The methods return <see cref="ApiResult{T}"/> rather than a bare value so the controller can
/// encode its full response map <em>now</em>, including the branches this slice cannot yet reach.
/// If the controller had to grow a 404 later, the contract would not have survived the wiring.
/// </para>
/// </remarks>
public interface IProductApiService
{
    /// <summary>The catalogue, optionally restricted to active products.</summary>
    Task<ApiResult<IReadOnlyList<ProductResponse>>> GetAllAsync(bool activeOnly);

    /// <summary>
    /// One product by its code, trimmed and matched case-insensitively.
    /// Answers <see cref="ApiOutcome.NotFound"/> when no product carries that code.
    /// </summary>
    Task<ApiResult<ProductResponse>> GetByCodeAsync(string productCode);

    /// <summary>
    /// Creates a product. Answers <see cref="ApiOutcome.Conflict"/> when the code is already in
    /// use — a well-formed request the stored data disagrees with, not a malformed one.
    /// </summary>
    Task<ApiResult<ProductResponse>> CreateAsync(CreateProductRequest request);
}
