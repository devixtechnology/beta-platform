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
/// <strong>Representative data in this slice.</strong> Shapes, status codes, permissions and request
/// validation are final and enforced; the responses are drawn from sample data and nothing is read
/// from or written to the products table (FR-033).
/// </remarks>
[ApiController]
[Route("api/v1/products")]
[Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
public class ProductsApiController : ApiControllerBase
{
    private readonly IProductApiService _products;

    public ProductsApiController(IProductApiService products) => _products = products;

    /// <summary>
    /// Lists the product catalogue. Representative data in this slice.
    /// </summary>
    /// <param name="activeOnly">Exclude deactivated products.</param>
    /// <response code="200">The catalogue. An empty catalogue is an empty list, never a 404.</response>
    /// <response code="401">No token, expired, or the account was deactivated since issue.</response>
    [EndpointSummary("List the product catalogue")]
    [EndpointDescription("REPRESENTATIVE DATA in this slice - nothing is read from the products table. An empty catalogue is an empty list, never a 404.")]
    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<ProductResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> GetAll([FromQuery] bool activeOnly = false)
    {
        var result = await _products.GetAllAsync(activeOnly);
        return FromResult(result, Ok);
    }

    /// <summary>
    /// Gets one product by its product code. Representative data in this slice.
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
    [EndpointDescription("REPRESENTATIVE DATA in this slice. Codes are trimmed and matched case-insensitively. A deactivated product is returned with isActive=false rather than reported missing. An unknown code does answer 404 - the representative catalogue is finite.")]
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
    /// Creates a product. Administrators only. The submission is genuinely validated and
    /// permission-checked; it is NOT persisted in this slice.
    /// </summary>
    /// <remarks>
    /// A created product is always active — the request cannot say otherwise. The 409 for a code
    /// already in use is specified here and answered by this action, but cannot occur until the
    /// behaviour slice has a catalogue to check against.
    /// </remarks>
    /// <response code="201">The created product, in the same shape the reads return.</response>
    /// <response code="400">A required field is missing, too long, or the body is unparsable.</response>
    /// <response code="401">No token, expired, or the account was deactivated since issue.</response>
    /// <response code="403">Authenticated, but not an administrator.</response>
    /// <response code="409">A product with this code already exists.</response>
    [EndpointSummary("Create a product (administrators only)")]
    [EndpointDescription("Request validation and permissions are ENFORCED; the product is NOT PERSISTED in this slice. A created product is always active - the request cannot say otherwise. The 409 for a duplicate product code is specified but NOT YET PRODUCED.")]
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
