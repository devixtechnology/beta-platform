using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using BetaPlatform.Data.Entities;
using BetaPlatform.Services;

namespace BetaPlatform.Controllers;

public class WorkOrdersController : Controller
{
    private readonly IWorkOrderService _orders;
    private readonly IProductService _products;
    private readonly IMachineService _machines;

    public WorkOrdersController(IWorkOrderService orders, IProductService products, IMachineService machines)
    {
        _orders = orders;
        _products = products;
        _machines = machines;
    }

    public async Task<IActionResult> Index()
    {
        var orders = await _orders.GetAllAsync();
        return View(orders);
    }

    public async Task<IActionResult> Details(int id)
    {
        var order = await _orders.GetByIdAsync(id);
        if (order is null) return NotFound();
        return View(order);
    }

    [HttpGet]
    public async Task<IActionResult> Create()
    {
        await PopulateListsAsync(null, null);
        return View(new WorkOrder { PlannedStartTime = Helpers.TimeZoneHelper.GetKsaNow() });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create([Bind("WorkOrderNumber,InputProductId,OutputProductId,MachineId,PlannedStartTime,QtyToManufacture,HourRate,LineSetupTimeMinutes,WorkstationCapabilityPerHour")] WorkOrder order)
    {
        if (!ModelState.IsValid)
        {
            await PopulateListsAsync(order.InputProductId, order.MachineId);
            return View(order);
        }

        var result = await _orders.CreateAsync(order);
        if (!result.Success)
        {
            ModelState.AddModelError(string.Empty, result.Error!);
            await PopulateListsAsync(order.InputProductId, order.MachineId);
            return View(order);
        }

        TempData["Success"] = "Work order created.";
        return RedirectToAction(nameof(Details), new { id = result.Value!.WorkOrderId });
    }

    [HttpGet]
    public async Task<IActionResult> Edit(int id)
    {
        var order = await _orders.GetByIdAsync(id);
        if (order is null) return NotFound();
        if (order.Status == WorkOrderStatus.Finished)
        {
            TempData["Error"] = "A finished work order cannot be edited.";
            return RedirectToAction(nameof(Details), new { id });
        }
        await PopulateListsAsync(order.InputProductId, order.MachineId);
        return View(order);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, [Bind("WorkOrderId,WorkOrderNumber,InputProductId,OutputProductId,MachineId,PlannedStartTime,QtyToManufacture,HourRate,LineSetupTimeMinutes,WorkstationCapabilityPerHour")] WorkOrder order)
    {
        if (id != order.WorkOrderId) return BadRequest();
        if (!ModelState.IsValid)
        {
            await PopulateListsAsync(order.InputProductId, order.MachineId);
            return View(order);
        }

        var result = await _orders.UpdateAsync(order);
        if (!result.Success)
        {
            ModelState.AddModelError(string.Empty, result.Error!);
            await PopulateListsAsync(order.InputProductId, order.MachineId);
            return View(order);
        }

        TempData["Success"] = "Work order updated.";
        return RedirectToAction(nameof(Details), new { id });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Start(int id) => await Transition(id, _orders.StartAsync, "Work order started.");

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Hold(int id) => await Transition(id, _orders.HoldAsync, "Work order placed on hold.");

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Resume(int id) => await Transition(id, _orders.ResumeAsync, "Work order resumed.");

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Finish(int id) => await Transition(id, _orders.FinishAsync, "Work order finished.");

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AddInput(int id, decimal weight)
    {
        var result = await _orders.AddInputAsync(id, weight);
        TempData[result.Success ? "Success" : "Error"] = result.Success ? "Input recorded." : result.Error;
        return RedirectToAction(nameof(Details), new { id });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteInput(int id, int inputId)
    {
        var result = await _orders.DeleteInputAsync(inputId);
        TempData[result.Success ? "Success" : "Error"] = result.Success ? "Input removed." : result.Error;
        return RedirectToAction(nameof(Details), new { id });
    }

    /// <summary>JSON endpoint polled ~10s by the Work Order Details page to refresh the single
    /// telemetry-sourced Total Weight / Total Count (003 change request).</summary>
    [HttpGet]
    public async Task<IActionResult> LiveTotals(int id)
    {
        var totals = await _orders.GetLiveTotalsAsync(id);
        return Json(new { totalWeight = totals.TotalWeight, totalCount = totals.TotalCount, timestamp = totals.Timestamp });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id)
    {
        var result = await _orders.DeleteAsync(id);
        TempData[result.Success ? "Success" : "Error"] = result.Success ? "Work order deleted." : result.Error;
        return RedirectToAction(nameof(Index));
    }

    private async Task<IActionResult> Transition(int id, Func<int, Task<ServiceResult>> action, string successMsg)
    {
        var result = await action(id);
        TempData[result.Success ? "Success" : "Error"] = result.Success ? successMsg : result.Error;
        return RedirectToAction(nameof(Details), new { id });
    }

    private async Task PopulateListsAsync(int? selectedProduct, int? selectedMachine)
    {
        var products = await _products.GetActiveAsync();
        var machines = await _machines.GetActiveAsync();
        ViewBag.Products = new SelectList(products, nameof(Product.ProductId), nameof(Product.ProductName), selectedProduct);
        ViewBag.Machines = new SelectList(machines, nameof(Machine.MachineId), nameof(Machine.MachineName), selectedMachine);
    }
}
