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
/// Every submitted product code is resolved against the stored catalogue and the order is stored
/// (006). Resolution happens in full before anything is written, so a refused request leaves nothing
/// behind and a request naming several bad codes is told about all of them at once.
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
    /// code. The order is stored in the Ready state and appears on the Work Orders screen.
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
    [EndpointDescription("Stores the order in status Ready. Inputs are a LIST of product codes (at least one, none blank, no repeats); the output is a SINGLE code. An output code may repeat an input - a rework order legitimately consumes and produces the same product. A code that names no product, or names a DEACTIVATED one, answers 400 naming which entry failed, as inputProductCodes[i] or outputProductCode (NOT 404, which would say the endpoint is missing); every offending entry is named in the same response. A work-order number already in use answers 409. A refused request stores nothing.")]
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
