using Microsoft.EntityFrameworkCore;
using BetaPlatform.Data;
using BetaPlatform.Data.Entities;
using BetaPlatform.Helpers;

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

    public WorkOrderService(ApplicationDbContext db) => _db = db;

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
        _db.WorkOrders.Add(order);
        await _db.SaveChangesAsync();
        return ServiceResult<WorkOrder>.Ok(order);
    }

    public async Task<ServiceResult<WorkOrder>> UpdateAsync(WorkOrder order)
    {
        var existing = await _db.WorkOrders.FirstOrDefaultAsync(w => w.WorkOrderId == order.WorkOrderId);
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
        await _db.SaveChangesAsync();
        return ServiceResult<WorkOrder>.Ok(existing);
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

        var now = TimeZoneHelper.GetKsaNow();
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

        // Bank the segment that just ended, so held time is never counted as production.
        if (order.StartedAt is not null)
            order.TotalRuntime += (decimal)(TimeZoneHelper.GetKsaNow() - order.StartedAt.Value).TotalMinutes;

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

        // A new active segment begins: StartedAt is reset while FirstStartedAt and the banked
        // TotalRuntime are preserved.
        order.Status = WorkOrderStatus.InProgress;
        order.StartedAt = TimeZoneHelper.GetKsaNow();
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
