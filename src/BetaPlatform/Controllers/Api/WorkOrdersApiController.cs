using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using BetaPlatform.Data;
using BetaPlatform.Services.Api;
using BetaPlatform.ViewModels.Api;

namespace BetaPlatform.Controllers.Api;

/// <summary>
/// Raising work orders, naming products by code.
/// </summary>
/// <remarks>
/// <strong>Representative data in this slice.</strong> Request validation and permissions are
/// enforced; nothing is written to the work-orders table and product codes are not yet resolved
/// against the real catalogue (FR-033).
/// </remarks>
[ApiController]
[Route("api/v1/work-orders")]
[Authorize(
    AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme,
    Roles = $"{DbSeeder.AdminRole},{DbSeeder.ClientRole}")]
public class WorkOrdersApiController : ApiControllerBase
{
    private readonly IWorkOrderApiService _workOrders;

    public WorkOrdersApiController(IWorkOrderApiService workOrders) => _workOrders = workOrders;

    /// <summary>
    /// Creates a work order from a list of input <em>product codes</em> and one output product
    /// code. Representative data in this slice — the order is validated but not persisted.
    /// </summary>
    /// <remarks>
    /// The inputs are a list because an order consumes several materials; the output stays a single
    /// product. At least one input is required, none may be blank, and no code may be listed twice.
    ///
    /// An output code may repeat one of the inputs: a rework or re-packing order legitimately
    /// consumes and produces the same product, so this is accepted rather than refused as a likely
    /// typo.
    ///
    /// An unresolvable product code answers 400 naming the offending field — an input as
    /// <c>inputProductCodes[i]</c>, at the position submitted — not 404, which would tell the caller
    /// this endpoint is missing. The work-order resource was never addressed; a field in the body is
    /// wrong.
    /// </remarks>
    /// <response code="201">The created order, echoing every code, always in status "Ready".</response>
    /// <response code="400">A required field is missing, the input list is empty or repeats a code, the quantity is not positive, the body is unparsable, or a product code resolves to nothing.</response>
    /// <response code="401">No token, expired, or the account was deactivated since issue.</response>
    /// <response code="403">Authenticated, but holding neither the administrative nor the client role.</response>
    /// <response code="409">A work order with this number already exists.</response>
    [EndpointSummary("Create a work order, naming products by code")]
    [EndpointDescription("Request validation and permissions are ENFORCED; the order is NOT PERSISTED in this slice. Inputs are a LIST of product codes (at least one, none blank, no repeats); the output is a SINGLE code. An output code may repeat an input - a rework order legitimately consumes and produces the same product. An unresolvable product code answers 400 naming which entry failed, as inputProductCodes[i] (NOT 404, which would say the endpoint is missing); that and the 409 for a duplicate work-order number are specified but NOT YET PRODUCED.")]
    [HttpPost]
    [ProducesResponseType(typeof(WorkOrderResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Create([FromBody] CreateWorkOrderRequest request)
    {
        var result = await _workOrders.CreateAsync(request);

        // No GET for work orders on this surface, so a 201 with the body but no Location header:
        // pointing at an address that does not exist would be worse than omitting it.
        return FromResult(result, created => StatusCode(StatusCodes.Status201Created, created));
    }
}
