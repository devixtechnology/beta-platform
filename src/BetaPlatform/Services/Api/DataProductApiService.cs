using BetaPlatform.Data.Entities;
using BetaPlatform.ViewModels.Api;

namespace BetaPlatform.Services.Api;

/// <summary>
/// The product operations answered from the platform's own catalogue (006 US1/US2).
/// </summary>
/// <remarks>
/// <para>
/// Replaces <c>SampleProductApiService</c>, which returned four invented rows and stored nothing.
/// This is the swap 005 research R7 designed for: the registration in <c>Program.cs</c> changes and
/// nothing else does — no route, no DTO, no status code (005 FR-034, SC-005).
/// </para>
/// <para>
/// It delegates to <see cref="IProductService"/> rather than touching the database, so the duplicate
/// -code rule the Products screen enforces and the one the API enforces are the same rule. A second
/// copy would drift, and the first drift would let the API create a product the screen forbids.
/// </para>
/// </remarks>
public class DataProductApiService : IProductApiService
{
    private readonly IProductService _products;

    public DataProductApiService(IProductService products) => _products = products;

    public async Task<ApiResult<IReadOnlyList<ProductResponse>>> GetAllAsync(bool activeOnly)
    {
        // SearchAsync(null) is the whole catalogue, ordered by name — deactivated products included,
        // because the filter is the caller's to apply (006 FR-002).
        var products = activeOnly
            ? await _products.GetActiveAsync()
            : await _products.SearchAsync(null);

        IReadOnlyList<ProductResponse> response = products.Select(ToResponse).ToList();

        // An empty catalogue is an empty list, never a 404: "you asked wrongly" and "there is
        // nothing yet" are different answers (006 FR-001).
        return ApiResult<IReadOnlyList<ProductResponse>>.Ok(response);
    }

    public async Task<ApiResult<ProductResponse>> GetByCodeAsync(string productCode)
    {
        var product = await _products.GetByCodeAsync(productCode);

        // A deactivated product is returned, not hidden: "never existed" and "no longer used" are
        // different answers and a caller reconciling history needs to tell them apart (006 FR-004).
        return product is null
            ? ApiResult<ProductResponse>.NotFound(
                $"No product exists with code '{ProductCode.Normalise(productCode)}'.")
            : ApiResult<ProductResponse>.Ok(ToResponse(product));
    }

    public async Task<ApiResult<ProductResponse>> CreateAsync(CreateProductRequest request)
    {
        var product = new Product
        {
            ProductCode = ProductCode.Normalise(request.ProductCode),
            ProductName = request.ProductName,
            ProductNameEnglish = request.ProductNameEnglish,
            Category = request.Category,
            Unit = request.Unit,

            // Always active on creation (005 FR-017) — the request has no field to say otherwise.
            IsActive = true
        };

        var result = await _products.CreateAsync(product);

        // The only way CreateAsync fails is a code already in use. That is a well-formed request the
        // stored data disagrees with — a 409, not a 400, so the caller retries with a new code
        // rather than hunting its payload for a formatting mistake (006 FR-009).
        return result.Success && result.Value is not null
            ? ApiResult<ProductResponse>.Ok(ToResponse(result.Value))
            : ApiResult<ProductResponse>.Conflict(result.Error
                ?? $"A product already exists with code '{product.ProductCode}'.");
    }

    /// <summary>
    /// Stored product to wire shape. The one place the mapping lives, so the product id cannot leak
    /// through a path somebody forgot about (005 FR-022).
    /// </summary>
    private static ProductResponse ToResponse(Product product) => new()
    {
        ProductCode = product.ProductCode,
        ProductName = product.ProductName,
        ProductNameEnglish = product.ProductNameEnglish,
        Category = product.Category,
        Unit = product.Unit,
        IsActive = product.IsActive
    };
}
