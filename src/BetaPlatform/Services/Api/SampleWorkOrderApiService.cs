using BetaPlatform.Data.Entities;
using BetaPlatform.ViewModels.Api;

namespace BetaPlatform.Services.Api;

/// <summary>
/// Representative work-order responses for the contract-first slice (spec §Slice boundary, FR-033).
/// </summary>
/// <remarks>
/// Persists nothing, and does not resolve product codes against the real catalogue. The unresolvable
/// -code and duplicate-number outcomes this operation can return are defined on
/// <see cref="IWorkOrderApiService"/> and answered by the controller, but are unreachable until the
/// behaviour slice has data to check against.
/// </remarks>
public class SampleWorkOrderApiService : IWorkOrderApiService
{
    public Task<ApiResult<WorkOrderResponse>> CreateAsync(CreateWorkOrderRequest request)
    {
        var created = new WorkOrderResponse
        {
            WorkOrderNumber = request.WorkOrderNumber,

            // Echoed exactly as submitted (FR-027), normalised only for accidental padding. Order is
            // preserved: the caller listed its materials in an order it recognises.
            InputProductCodes = request.InputProductCodes.Select(ProductCode.Normalise).ToList(),
            OutputProductCode = ProductCode.Normalise(request.OutputProductCode),

            // The name, not the enum's integer — the numbering is an internal detail (FR-026).
            Status = WorkOrderStatus.Ready.ToString(),

            // Required by validation, so these are present by the time the service is reached.
            PlannedStartTime = request.PlannedStartTime!.Value,
            QtyToManufacture = request.QtyToManufacture!.Value,

            MachineId = request.MachineId,
            HourRate = request.HourRate,
            LineSetupTimeMinutes = request.LineSetupTimeMinutes,
            WorkstationCapabilityPerHour = request.WorkstationCapabilityPerHour
        };

        return Task.FromResult(ApiResult<WorkOrderResponse>.Ok(created));
    }
}
