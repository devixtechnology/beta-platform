using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using BetaPlatform.Services.Api;
using BetaPlatform.ViewModels.Api;

namespace BetaPlatform.Controllers.Api;

/// <summary>
/// The machine fleet.
/// </summary>
/// <remarks>
/// <para>
/// Answered from the platform's own records, through the same services the Machines screen uses.
/// </para>
/// <para>
/// Unlike the product operations, these responses <b>do</b> carry the record's id. A machine has no
/// external code that addresses it on this surface, and <c>machineId</c> is exactly what
/// <c>POST /work-orders</c> asks for — so this is the endpoint that tells a caller which value to
/// send.
/// </para>
/// </remarks>
[ApiController]
[Route("api/v1/machines")]
[Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
public class MachinesApiController : ApiControllerBase
{
    private readonly IMachineApiService _machines;

    public MachinesApiController(IMachineApiService machines) => _machines = machines;

    /// <summary>
    /// Lists every machine with its type, production line, active flag and live running state.
    /// </summary>
    /// <param name="activeOnly">Exclude deactivated machines.</param>
    /// <response code="200">The fleet. An empty fleet is an empty list, never a 404.</response>
    /// <response code="401">No token, expired, or the account was deactivated since issue.</response>
    [EndpointSummary("List machines, with ids")]
    [EndpointDescription("Every machine with its full detail: machineId, code, name, type (id, names, production line), the stored isActive and isRunning flags, the live runningState, and when it was created. An empty fleet is an empty list, never a 404. Set activeOnly=true to exclude deactivated machines. Unlike products, this response DOES carry the record id - machineId is the value POST /work-orders expects. runningState is the live state by the same rule every screen uses: an in-progress work order on the machine forces Running; otherwise the latest reading decides, and a machine that has never reported or whose reading has gone stale is Stopped (silence means not producing). Unknown survives only for a fresh reading whose status cannot be interpreted. It is NOT the same thing as the stored isRunning flag, which is an administrator setting.")]
    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<MachineResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> GetAll([FromQuery] bool activeOnly = false)
    {
        var result = await _machines.GetAllAsync(activeOnly);
        return FromResult(result, Ok);
    }
}
