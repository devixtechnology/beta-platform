using Microsoft.EntityFrameworkCore;
using BetaPlatform.Data;
using BetaPlatform.Data.Entities;
using BetaPlatform.Helpers;
using BetaPlatform.Services.Erp;

namespace BetaPlatform.Services;

public interface IWorkOrderService
{
    Task<List<WorkOrder>> GetAllAsync();
    Task<WorkOrder?> GetByIdAsync(int id);
    Task<ServiceResult<WorkOrder>> CreateAsync(WorkOrder order);
    Task<ServiceResult<WorkOrder>> UpdateAsync(WorkOrder order);
    Task<ServiceResult> StartAsync(int id);
    Task<ServiceResult> HoldAsync(int id);
    Task<ServiceResult> ResumeAsync(int id);
    Task<ServiceResult> FinishAsync(int id);
    Task<ServiceResult<WorkOrderInput>> AddInputAsync(int workOrderId, decimal weight);
    Task<ServiceResult> DeleteInputAsync(int inputId);
    Task<ServiceResult> DeleteAsync(int id);

    /// <summary>Live single-output totals for an order, sourced from the latest read-only
    /// <c>oee_data</c> row for this order (003 change request — polled ~10s by the Details page).</summary>
    Task<WorkOrderLiveTotals> GetLiveTotalsAsync(int workOrderId);

    /// <summary>Pure transition-rule check (FR-034), exposed for testing/UI gating.</summary>
    bool IsValidTransition(WorkOrderStatus from, WorkOrderStatus to);
}

/// <summary>Telemetry-derived, non-stored totals shown on the Work Order screen.</summary>
public record WorkOrderLiveTotals(decimal TotalWeight, decimal TotalCount, DateTime? Timestamp);

public class WorkOrderService : IWorkOrderService
{
    private readonly ApplicationDbContext _db;
    private readonly IErpClient? _erp;

    /// <param name="db">The platform database.</param>
    /// <param name="erp">
    /// The upstream ERP, notified <em>before</em> every state change is committed and holding a
    /// veto over it: a refused or unreachable ERP cancels the transition and nothing is written.
    /// Optional so the service can still be constructed with a bare context in tests that are not
    /// about the integration; in the running application DI always supplies it.
    /// </param>
    public WorkOrderService(ApplicationDbContext db, IErpClient? erp = null)
    {
        _db = db;
        _erp = erp;
    }

    public Task<List<WorkOrder>> GetAllAsync() =>
        _db.WorkOrders
            .Include(w => w.InputProduct)
            .Include(w => w.OutputProduct)
            .Include(w => w.Machine)
            .OrderByDescending(w => w.CreatedAt)
            .ToListAsync();

    public Task<WorkOrder?> GetByIdAsync(int id) =>
        _db.WorkOrders
            .Include(w => w.InputProduct)
            .Include(w => w.OutputProduct)
            .Include(w => w.Machine)
            .Include(w => w.Inputs)
            // Every product the order consumes, for the details screen (006 FR-024).
            .Include(w => w.InputProducts.OrderBy(p => p.Position))
                .ThenInclude(p => p.Product)
            .FirstOrDefaultAsync(w => w.WorkOrderId == id);

    public async Task<ServiceResult<WorkOrder>> CreateAsync(WorkOrder order)
    {
        if (await NumberExistsAsync(order.WorkOrderNumber, null))
            return ServiceResult<WorkOrder>.Fail($"Work order number '{order.WorkOrderNumber}' already exists.");

        order.Status = WorkOrderStatus.Ready;
        order.StartedAt = null;
        order.FirstStartedAt = null;
        order.FinishedAt = null;
        order.TotalRuntime = 0m;

        // Every order carries its input-product list, however it was raised (006 FR-027). The API
        // supplies several; the Create screen supplies none and gets a single entry mirroring its
        // one selection. There are not two kinds of order.
        SyncInputProducts(order, order.InputProducts
            .OrderBy(p => p.Position)
            .Select(p => p.ProductId)
            .ToList());

        _db.WorkOrders.Add(order);
        await _db.SaveChangesAsync();
        return ServiceResult<WorkOrder>.Ok(order);
    }

    public async Task<ServiceResult<WorkOrder>> UpdateAsync(WorkOrder order)
    {
        var existing = await _db.WorkOrders
            .Include(w => w.InputProducts)
            .FirstOrDefaultAsync(w => w.WorkOrderId == order.WorkOrderId);
        if (existing is null)
            return ServiceResult<WorkOrder>.Fail("Work order not found.");
        if (existing.Status == WorkOrderStatus.Finished)
            return ServiceResult<WorkOrder>.Fail("A finished work order cannot be edited.");
        if (await NumberExistsAsync(order.WorkOrderNumber, order.WorkOrderId))
            return ServiceResult<WorkOrder>.Fail($"Work order number '{order.WorkOrderNumber}' already exists.");

        existing.WorkOrderNumber = order.WorkOrderNumber;
        existing.InputProductId = order.InputProductId;
        existing.OutputProductId = order.OutputProductId;
        existing.MachineId = order.MachineId;           // assign/reassign allowed while not Finished (FR-038)
        existing.PlannedStartTime = order.PlannedStartTime;
        existing.QtyToManufacture = order.QtyToManufacture;
        existing.HourRate = order.HourRate;
        existing.LineSetupTimeMinutes = order.LineSetupTimeMinutes;
        existing.WorkstationCapabilityPerHour = order.WorkstationCapabilityPerHour;

        // The Edit screen carries one input dropdown, so it can only ever say what the PRIMARY input
        // now is. Changing it REPLACES the primary — which is what it has always meant for a
        // single-input order, and the only reading that leaves such an order with one input rather
        // than two. Any further inputs the order has are left alone: an order raised through the API
        // with three materials must not lose the other two because somebody changed the dropdown
        // (006 FR-025).
        SyncInputProducts(existing, existing.InputProducts
            .OrderBy(p => p.Position)
            .Select(p => p.ProductId)
            .Skip(1)
            .ToList());

        await _db.SaveChangesAsync();
        return ServiceResult<WorkOrder>.Ok(existing);
    }

    /// <summary>
    /// Rewrites an order's input-product list so that position 0 is always
    /// <see cref="WorkOrder.InputProductId"/>, followed by <paramref name="furtherProductIds"/> with
    /// the primary and any repeat removed. This is the single place the 006 FR-025 invariant is
    /// enforced, so no caller can leave the list and the column disagreeing.
    /// </summary>
    private static void SyncInputProducts(WorkOrder order, IEnumerable<int> furtherProductIds)
    {
        var desired = new List<int> { order.InputProductId };
        foreach (var id in furtherProductIds)
        {
            // Not a silent collapse of a caller's mistake: 005 already refuses a repeated CODE at
            // the edge. This only stops the primary appearing twice when it is re-seated.
            if (!desired.Contains(id))
            {
                desired.Add(id);
            }
        }

        var current = order.InputProducts.ToList();

        // Reuse rows rather than delete-and-insert: the unique (work_order_id, product_id) index
        // would otherwise reject an insert against a row EF has not deleted yet.
        for (var position = 0; position < desired.Count; position++)
        {
            var productId = desired[position];
            var row = current.FirstOrDefault(p => p.ProductId == productId);

            if (row is null)
            {
                order.InputProducts.Add(new WorkOrderInputProduct
                {
                    ProductId = productId,
                    Position = position
                });
            }
            else
            {
                row.Position = position;
                current.Remove(row);
            }
        }

        // Whatever is left is no longer an input of this order.
        foreach (var stale in current)
        {
            order.InputProducts.Remove(stale);
        }
    }

    /// <summary>
    /// Sends an ERP notification and turns anything other than acceptance into the failure the
    /// caller returns instead of committing its state change.
    /// </summary>
    /// <param name="notify">The notification to send.</param>
    /// <returns>Null when the transition may proceed; the failure to return otherwise.</returns>
    /// <remarks>
    /// <para>
    /// Null when no ERP client is injected at all, and null for a mocked call, so a site with no
    /// <c>Erp:BaseUrl</c> configured is untouched by the veto — the ERP can only block a
    /// transition once a real ERP is configured to block it.
    /// </para>
    /// <para>
    /// One message covers every way the call can fail. Status codes, tokens and host names are the
    /// administrator's business and stay in the log (see <see cref="HttpErpClient"/>); the operator
    /// is told the one thing that concerns them, which is that the order did not move.
    /// </para>
    /// </remarks>
    private async Task<ServiceResult?> NotifyErpAsync(Func<IErpClient, Task<ErpCallResult>> notify)
    {
        if (_erp is null) return null;

        var call = await notify(_erp);
        return call.Success
            ? null
            : ServiceResult.Fail("The ERP could not be called, so the request was not completed.");
    }

    public async Task<ServiceResult> StartAsync(int id)
    {
        var order = await _db.WorkOrders.FirstOrDefaultAsync(w => w.WorkOrderId == id);
        if (order is null) return ServiceResult.Fail("Work order not found.");
        if (!IsValidTransition(order.Status, WorkOrderStatus.InProgress))
            return ServiceResult.Fail($"Cannot start a work order in status '{order.Status}'.");
        if (order.MachineId is null)
            return ServiceResult.Fail("Assign a machine before starting the work order.");

        // A machine runs one order at a time. Only an In Progress order occupies it — a held
        // order releases its machine so another order can run (see HoldAsync).
        var occupying = await GetOrderOccupyingMachineAsync(order.MachineId.Value, order.WorkOrderId);
        if (occupying is not null)
            return ServiceResult.Fail(
                $"Machine is already running work order '{occupying}'. Finish or hold it before starting another.");

        // The moment the operator pressed Start, taken before the ERP round-trip so a slow ERP
        // never lands in the recorded times.
        var now = TimeZoneHelper.GetKsaNow();

        // The ERP is told first and gets a veto: nothing below runs unless it accepted.
        var refused = await NotifyErpAsync(erp => erp.NotifyStartedAsync(order));
        if (refused is not null) return refused;

        order.Status = WorkOrderStatus.InProgress;
        order.StartedAt = now;
        // First real start of a fresh order: stamp the immutable original start and reset the
        // runtime accumulator. StartedAt above marks the first segment.
        order.FirstStartedAt = now;
        order.TotalRuntime = 0m;
        await _db.SaveChangesAsync();

        return ServiceResult.Ok();
    }

    public async Task<ServiceResult> HoldAsync(int id)
    {
        var order = await _db.WorkOrders.FirstOrDefaultAsync(w => w.WorkOrderId == id);
        if (order is null) return ServiceResult.Fail("Work order not found.");
        if (!IsValidTransition(order.Status, WorkOrderStatus.OnHold))
            return ServiceResult.Fail($"Cannot place a work order in status '{order.Status}' on hold.");

        var now = TimeZoneHelper.GetKsaNow();

        var refused = await NotifyErpAsync(erp => erp.NotifyHeldAsync(order));
        if (refused is not null) return refused;

        // Bank the segment that just ended, so held time is never counted as production. Measured
        // to the moment Hold was pressed, not to the end of the ERP call.
        if (order.StartedAt is not null)
            order.TotalRuntime += (decimal)(now - order.StartedAt.Value).TotalMinutes;

        order.Status = WorkOrderStatus.OnHold;
        await _db.SaveChangesAsync();

        return ServiceResult.Ok();
    }

    public async Task<ServiceResult> ResumeAsync(int id)
    {
        var order = await _db.WorkOrders.FirstOrDefaultAsync(w => w.WorkOrderId == id);
        if (order is null) return ServiceResult.Fail("Work order not found.");
        if (!IsValidTransition(order.Status, WorkOrderStatus.InProgress))
            return ServiceResult.Fail($"Cannot resume a work order in status '{order.Status}'.");

        // A held order is pinned to the machine it was started on — resume cannot move it.
        if (order.MachineId is null)
            return ServiceResult.Fail("The work order has no machine assigned and cannot be resumed.");

        // Holding freed the machine, so another order may have taken it meanwhile. That order must
        // finish (or be held) before this one can resume onto its original machine.
        var occupying = await GetOrderOccupyingMachineAsync(order.MachineId.Value, order.WorkOrderId);
        if (occupying is not null)
            return ServiceResult.Fail(
                $"Machine is running work order '{occupying}'. This order resumes only onto its original machine, so wait for that one to finish.");

        var now = TimeZoneHelper.GetKsaNow();

        // Resuming is a start as far as the ERP is concerned: it has no notion of hold-and-resume
        // segments, only "this order is running now".
        var refused = await NotifyErpAsync(erp => erp.NotifyStartedAsync(order));
        if (refused is not null) return refused;

        // A new active segment begins: StartedAt is reset while FirstStartedAt and the banked
        // TotalRuntime are preserved.
        order.Status = WorkOrderStatus.InProgress;
        order.StartedAt = now;
        await _db.SaveChangesAsync();

        return ServiceResult.Ok();
    }

    public async Task<ServiceResult> FinishAsync(int id)
    {
        var order = await _db.WorkOrders.FirstOrDefaultAsync(w => w.WorkOrderId == id);
        if (order is null) return ServiceResult.Fail("Work order not found.");
        if (!IsValidTransition(order.Status, WorkOrderStatus.Finished))
            return ServiceResult.Fail($"Cannot finish a work order in status '{order.Status}'. It must be In Progress.");

        var now = TimeZoneHelper.GetKsaNow();

        // What the line actually made, from the same telemetry reading the details screen shows.
        // An order that produced no telemetry reports zero, which is the truth. Read before the
        // notification because the finish event carries it.
        var totals = await GetLiveTotalsAsync(order.WorkOrderId);

        var refused = await NotifyErpAsync(erp => erp.NotifyFinishedAsync(order, totals.TotalWeight));
        if (refused is not null) return refused;

        // Bank the final segment, so TotalRuntime holds the complete hold-excluded runtime.
        if (order.StartedAt is not null)
            order.TotalRuntime += (decimal)(now - order.StartedAt.Value).TotalMinutes;

        order.Status = WorkOrderStatus.Finished;
        order.FinishedAt = now;
        await _db.SaveChangesAsync();

        return ServiceResult.Ok();
    }

    public async Task<ServiceResult<WorkOrderInput>> AddInputAsync(int workOrderId, decimal weight)
    {
        var order = await _db.WorkOrders.FirstOrDefaultAsync(w => w.WorkOrderId == workOrderId);
        if (order is null)
            return ServiceResult<WorkOrderInput>.Fail("Work order not found.");
        if (weight <= 0)
            return ServiceResult<WorkOrderInput>.Fail("Input weight must be greater than zero.");

        var input = new WorkOrderInput
        {
            WorkOrderId = workOrderId,
            Weight = weight
        };
        _db.WorkOrderInputs.Add(input);
        await _db.SaveChangesAsync();
        return ServiceResult<WorkOrderInput>.Ok(input);
    }

    public async Task<ServiceResult> DeleteInputAsync(int inputId)
    {
        var input = await _db.WorkOrderInputs.FirstOrDefaultAsync(i => i.InputId == inputId);
        if (input is null) return ServiceResult.Fail("Input record not found.");

        _db.WorkOrderInputs.Remove(input);
        await _db.SaveChangesAsync();
        return ServiceResult.Ok();
    }

    public async Task<WorkOrderLiveTotals> GetLiveTotalsAsync(int workOrderId)
    {
        // Single output for the order = the latest read-only OEE reading tagged with this order_id.
        var latest = await _db.OeeData
            .Where(o => o.OrderId == workOrderId)
            .OrderByDescending(o => o.Timestamp)
            .Select(o => new { o.TotalWeight, o.TotalCount, o.Timestamp })
            .FirstOrDefaultAsync();

        return latest is null
            ? new WorkOrderLiveTotals(0m, 0m, null)
            : new WorkOrderLiveTotals(latest.TotalWeight, latest.TotalCount, latest.Timestamp);
    }

    public async Task<ServiceResult> DeleteAsync(int id)
    {
        var order = await _db.WorkOrders.FirstOrDefaultAsync(w => w.WorkOrderId == id);
        if (order is null) return ServiceResult.Fail("Work order not found.");

        _db.WorkOrders.Remove(order); // inputs cascade
        await _db.SaveChangesAsync();
        return ServiceResult.Ok();
    }

    public bool IsValidTransition(WorkOrderStatus from, WorkOrderStatus to) => (from, to) switch
    {
        (WorkOrderStatus.Ready, WorkOrderStatus.InProgress) => true,
        (WorkOrderStatus.InProgress, WorkOrderStatus.OnHold) => true,
        (WorkOrderStatus.OnHold, WorkOrderStatus.InProgress) => true,
        (WorkOrderStatus.InProgress, WorkOrderStatus.Finished) => true,
        _ => false
    };

    /// <summary>The number of the order currently occupying a machine, or null when it is free.
    /// Only In Progress occupies — a held order has released its machine.</summary>
    private Task<string?> GetOrderOccupyingMachineAsync(int machineId, int excludeWorkOrderId) =>
        _db.WorkOrders
            .Where(w => w.MachineId == machineId
                        && w.WorkOrderId != excludeWorkOrderId
                        && w.Status == WorkOrderStatus.InProgress)
            .Select(w => w.WorkOrderNumber)
            .FirstOrDefaultAsync();

    private Task<bool> NumberExistsAsync(string number, int? excludeId) =>
        _db.WorkOrders.AnyAsync(w => w.WorkOrderNumber == number && (excludeId == null || w.WorkOrderId != excludeId));
}
