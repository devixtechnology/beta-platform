using BetaPlatform.ViewModels.Api;

namespace BetaPlatform.Services.Api;

/// <summary>
/// Representative product data for the contract-first slice (spec §Slice boundary, FR-033).
/// </summary>
/// <remarks>
/// <para>
/// Reads nothing and stores nothing. It exists so an integrator can build and test a complete client
/// against final shapes while the data behaviour is settled in a later slice. The follow-up slice
/// replaces this class with one that delegates to <see cref="IProductService"/>; nothing outside
/// this file and its DI registration changes.
/// </para>
/// <para>
/// The sample deliberately includes an Arabic primary name, a product with no English name or
/// category, and one <em>inactive</em> product — so a client exercises the nullable fields and the
/// <c>activeOnly</c> filter rather than only ever seeing the happy shape.
/// </para>
/// </remarks>
public class SampleProductApiService : IProductApiService
{
    private static readonly IReadOnlyList<ProductResponse> Catalogue =
    [
        new()
        {
            ProductCode = "RM-STEEL-01",
            ProductName = "لفائف صلب",
            ProductNameEnglish = "Steel Coil",
            Category = "Raw Material",
            Unit = "kg",
            IsActive = true
        },
        new()
        {
            ProductCode = "FG-PANEL-07",
            ProductName = "لوح معدني",
            ProductNameEnglish = "Metal Panel",
            Category = "Finished Goods",
            Unit = "pcs",
            IsActive = true
        },
        new()
        {
            // No English name, no category — the optional fields really are optional.
            ProductCode = "RM-RESIN-04",
            ProductName = "راتنج",
            Unit = "kg",
            IsActive = true
        },
        new()
        {
            // Retired, so a client meets isActive=false and the activeOnly filter has something to do.
            ProductCode = "RM-LEGACY-99",
            ProductName = "مادة قديمة",
            ProductNameEnglish = "Legacy Material",
            Category = "Raw Material",
            Unit = "kg",
            IsActive = false
        }
    ];

    public Task<ApiResult<IReadOnlyList<ProductResponse>>> GetAllAsync(bool activeOnly)
    {
        IReadOnlyList<ProductResponse> products = activeOnly
            ? Catalogue.Where(p => p.IsActive).ToList()
            : Catalogue;

        return Task.FromResult(ApiResult<IReadOnlyList<ProductResponse>>.Ok(products));
    }

    public Task<ApiResult<ProductResponse>> CreateAsync(CreateProductRequest request)
    {
        // Echoed back, not stored. The duplicate-code conflict this operation can return is defined
        // on the interface and answered by the controller, but is unreachable until the behaviour
        // slice has a catalogue to check against (spec §Slice boundary).
        var created = new ProductResponse
        {
            ProductCode = ProductCode.Normalise(request.ProductCode),
            ProductName = request.ProductName,
            ProductNameEnglish = request.ProductNameEnglish,
            Category = request.Category,
            Unit = request.Unit,

            // Always active on creation (FR-017) — the request cannot say otherwise.
            IsActive = true
        };

        return Task.FromResult(ApiResult<ProductResponse>.Ok(created));
    }

    public Task<ApiResult<ProductResponse>> GetByCodeAsync(string productCode)
    {
        // Through the shared helper, so the sample and the eventual data-backed implementation
        // agree on what "the same code" means (research R9).
        var match = Catalogue.FirstOrDefault(p => ProductCode.Matches(p.ProductCode, productCode));

        // A deactivated product is returned, not hidden: "never existed" and "no longer used" are
        // different answers and a caller reconciling history needs to tell them apart.
        return Task.FromResult(match is null
            ? ApiResult<ProductResponse>.NotFound($"No product exists with code '{ProductCode.Normalise(productCode)}'.")
            : ApiResult<ProductResponse>.Ok(match));
    }
}
