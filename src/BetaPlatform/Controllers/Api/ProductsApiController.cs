using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using BetaPlatform.Data;
using BetaPlatform.Services.Api;
using BetaPlatform.ViewModels.Api;

namespace BetaPlatform.Controllers.Api;

/// <summary>
/// The product catalogue, addressed by product code.
/// </summary>
/// <remarks>
/// Answered from the platform's own catalogue (006). Reads and writes go through
/// <c>IProductService</c>, the same service the Products screen uses, so the API and the screen
/// enforce one set of rules rather than two that can drift.
/// </remarks>
[ApiController]
[Route("api/v1/products")]
[Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
public class ProductsApiController : ApiControllerBase
{
    private readonly IProductApiService _products;

    public ProductsApiController(IProductApiService products) => _products = products;

    /// <summary>
    /// Lists the product catalogue.
    /// </summary>
    /// <param name="activeOnly">Exclude deactivated products.</param>
    /// <response code="200">The catalogue. An empty catalogue is an empty list, never a 404.</response>
    /// <response code="401">No token, expired, or the account was deactivated since issue.</response>
    [EndpointSummary("List the product catalogue")]
    [EndpointDescription("The stored product catalogue. An empty catalogue is an empty list, never a 404. Set activeOnly=true to exclude deactivated products.")]
    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<ProductResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> GetAll([FromQuery] bool activeOnly = false)
    {
        var result = await _products.GetAllAsync(activeOnly);
        return FromResult(result, Ok);
    }

    /// <summary>
    /// Gets one product by its product code.
    /// </summary>
    /// <remarks>
    /// Codes are trimmed and matched case-insensitively. A deactivated product is returned with
    /// <c>isActive: false</c> rather than reported missing.
    /// </remarks>
    /// <param name="productCode">The product code — never an internal record number.</param>
    /// <response code="200">The product.</response>
    /// <response code="401">No token, expired, or the account was deactivated since issue.</response>
    /// <response code="404">No product carries that code.</response>
    [EndpointSummary("Get one product by its product code")]
    [EndpointDescription("Codes are trimmed and matched case-insensitively. A deactivated product is returned with isActive=false rather than reported missing - 'never existed' and 'no longer used' are different answers. A code carried by no product answers 404.")]
    [HttpGet("{productCode}")]
    [ProducesResponseType(typeof(ProductResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetByCode(string productCode)
    {
        var result = await _products.GetByCodeAsync(productCode);
        return FromResult(result, Ok);
    }

    /// <summary>
    /// Creates a product. Administrators only. Stored, and immediately visible to the reads above
    /// and to the platform's own Products screen.
    /// </summary>
    /// <remarks>
    /// A created product is always active — the request cannot say otherwise. A code already carried
    /// by another product answers 409: a well-formed request the stored data disagrees with, not a
    /// malformed one, so the caller retries with a new code rather than hunting its payload.
    /// </remarks>
    /// <response code="201">The created product, in the same shape the reads return.</response>
    /// <response code="400">A required field is missing, too long, or the body is unparsable.</response>
    /// <response code="401">No token, expired, or the account was deactivated since issue.</response>
    /// <response code="403">Authenticated, but not an administrator.</response>
    /// <response code="409">A product with this code already exists.</response>
    [EndpointSummary("Create a product (administrators only)")]
    [EndpointDescription("Stores the product and returns it, with a Location addressing it by CODE. A created product is always active - the request cannot say otherwise. A code already in use answers 409, compared trimmed and case-insensitively.")]
    [HttpPost]
    [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme, Roles = DbSeeder.AdminRole)]
    [ProducesResponseType(typeof(ProductResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Create([FromBody] CreateProductRequest request)
    {
        var result = await _products.CreateAsync(request);

        return FromResult(result, created => CreatedAtAction(
            nameof(GetByCode),
            new { productCode = created.ProductCode },
            created));
    }
}
