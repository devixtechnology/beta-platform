using BetaPlatform.Data.Entities;
using BetaPlatform.ViewModels.Api;

namespace BetaPlatform.Services.Api;

/// <summary>
/// Work-order creation answered against the real catalogue and really stored (006 US3/US4).
/// </summary>
/// <remarks>
/// <para>
/// Replaces <c>SampleWorkOrderApiService</c>, which echoed the submission back and stored nothing.
/// The registration in <c>Program.cs</c> changes; no route, DTO or status code does.
/// </para>
/// <para>
/// Resolution happens in full <em>before</em> anything is written: a request naming three bad codes
/// is told about all three at once, and a refused request leaves nothing behind — no order, no input
/// rows, no half-order (006 FR-015, FR-020).
/// </para>
/// </remarks>
public class DataWorkOrderApiService : IWorkOrderApiService
{
    private readonly IWorkOrderService _workOrders;
    private readonly IProductService _products;
    private readonly IMachineService _machines;

    public DataWorkOrderApiService(
        IWorkOrderService workOrders,
        IProductService products,
        IMachineService machines)
    {
        _workOrders = workOrders;
        _products = products;
        _machines = machines;
    }

    public async Task<ApiResult<WorkOrderResponse>> CreateAsync(CreateWorkOrderRequest request)
    {
        // Shape validation already ran: the list is non-empty, carries no blank, and repeats no code
        // (005 ProductCodeListAttribute). What is left to check is whether these codes name anything.
        var inputCodes = request.InputProductCodes.Select(ProductCode.Normalise).ToList();
        var outputCode = ProductCode.Normalise(request.OutputProductCode);

        var catalogue = await _products.GetByCodesAsync(inputCodes.Append(outputCode));

        var errors = new Dictionary<string, string[]>();

        // Positional, because a caller with a list of materials has to be told WHICH entry to fix.
        // "A product code is invalid" against a list of six is not an answer anyone can act on.
        var inputProducts = new List<Product>(inputCodes.Count);
        for (var i = 0; i < inputCodes.Count; i++)
        {
            var resolved = Resolve(catalogue, inputCodes[i], out var problem);
            if (resolved is null)
            {
                errors[$"inputProductCodes[{i}]"] = [problem!];
            }
            else
            {
                inputProducts.Add(resolved);
            }
        }

        var outputProduct = Resolve(catalogue, outputCode, out var outputProblem);
        if (outputProduct is null)
        {
            errors["outputProductCode"] = [outputProblem!];
        }

        // The one internal identifier this contract still takes as a number. A machine that does not
        // exist is the caller's mistake, not a 500 from a foreign key three layers down (006 FR-021).
        if (request.MachineId is int machineId && await _machines.GetByIdAsync(machineId) is null)
        {
            errors["machineId"] = [$"No machine exists with id {machineId}."];
        }

        if (errors.Count > 0)
        {
            // 400, not 404: the endpoint is not missing, the body is wrong (005 contracts/errors.md).
            return ApiResult<WorkOrderResponse>.Invalid(errors);
        }

        var order = new WorkOrder
        {
            WorkOrderNumber = request.WorkOrderNumber.Trim(),

            // Position 0 stays in the existing column, which the Work Orders screens and the
            // running-orders reporting view read directly (006 FR-025).
            InputProductId = inputProducts[0].ProductId,
            OutputProductId = outputProduct!.ProductId,

            // Required by validation, so these are present by the time the service is reached.
            PlannedStartTime = request.PlannedStartTime!.Value,
            QtyToManufacture = request.QtyToManufacture!.Value,

            MachineId = request.MachineId,
            HourRate = request.HourRate,
            LineSetupTimeMinutes = request.LineSetupTimeMinutes,
            WorkstationCapabilityPerHour = request.WorkstationCapabilityPerHour
        };

        // The complete list, first entry included, in the order the caller listed it. WorkOrderService
        // re-seats the positions and enforces the position-0 invariant.
        for (var i = 0; i < inputProducts.Count; i++)
        {
            order.InputProducts.Add(new WorkOrderInputProduct
            {
                ProductId = inputProducts[i].ProductId,
                Position = i
            });
        }

        var result = await _workOrders.CreateAsync(order);

        // The only way CreateAsync fails is a work-order number already in use — a well-formed
        // request the stored data disagrees with, so 409 (006 FR-016).
        if (!result.Success || result.Value is null)
        {
            return ApiResult<WorkOrderResponse>.Conflict(result.Error
                ?? $"A work order already exists with number '{order.WorkOrderNumber}'.");
        }

        var stored = result.Value;

        return ApiResult<WorkOrderResponse>.Ok(new WorkOrderResponse
        {
            WorkOrderNumber = stored.WorkOrderNumber,

            // Echoed from what was submitted, in the submitted order — not read back from storage,
            // which knows the products but not the caller's spelling of their codes (005 FR-027).
            InputProductCodes = inputProducts.Select(p => p.ProductCode).ToList(),
            OutputProductCode = outputProduct.ProductCode,

            // The name, not the enum's integer — the numbering is an internal detail (005 FR-026).
            Status = stored.Status.ToString(),

            PlannedStartTime = stored.PlannedStartTime,
            QtyToManufacture = stored.QtyToManufacture,
            MachineId = stored.MachineId,
            HourRate = stored.HourRate,
            LineSetupTimeMinutes = stored.LineSetupTimeMinutes,
            WorkstationCapabilityPerHour = stored.WorkstationCapabilityPerHour
        });
    }

    /// <summary>
    /// The product a code names, or null with the reason. A <em>deactivated</em> product is refused
    /// here and returned by the read operations: deactivation exists to keep a product out of new
    /// selections while leaving history intact, which is exactly what the Work Orders screen does by
    /// offering only active products (006 FR-028/FR-029).
    /// </summary>
    private static Product? Resolve(IEnumerable<Product> catalogue, string code, out string? problem)
    {
        var match = catalogue.FirstOrDefault(p => ProductCode.Matches(p.ProductCode, code));

        if (match is null)
        {
            problem = $"No product exists with code '{code}'.";
            return null;
        }

        if (!match.IsActive)
        {
            // Said plainly, because the two refusals need different fixes: one is a typo, the other
            // is a product somebody retired on purpose.
            problem = $"Product '{match.ProductCode}' is deactivated and cannot be used on a new work order.";
            return null;
        }

        problem = null;
        return match;
    }
}
