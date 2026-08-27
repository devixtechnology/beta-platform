using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using BetaPlatform.Data;
using BetaPlatform.Data.Entities;
using BetaPlatform.Services;

namespace BetaPlatform.Controllers;

public class MachinesController : Controller
{
    private readonly IMachineService _machines;

    public MachinesController(IMachineService machines) => _machines = machines;

    public async Task<IActionResult> Index()
    {
        var machines = await _machines.GetAllWithStatusAsync();
        return View(machines);
    }

    [HttpGet]
    public async Task<IActionResult> Details(int id)
    {
        var vm = await _machines.GetDetailsAsync(id);
        if (vm is null) return NotFound();
        return View(vm);
    }

    /// <summary>JSON endpoint polled every ~5 s by the machine details page (004 — FR-018).
    /// Read-only, available to both roles under the global authorization policy.</summary>
    [HttpGet]
    public async Task<IActionResult> Data(int id)
    {
        var live = await _machines.GetLiveAsync(id);
        if (live is null) return NotFound();
        return Json(live);
    }

    [Authorize(Roles = DbSeeder.AdminRole)]
    [HttpGet]
    public async Task<IActionResult> Create()
    {
        await PopulateTypesAsync(null);
        return View(new Machine());
    }

    [Authorize(Roles = DbSeeder.AdminRole)]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create([Bind("MachineName,MachineCode,MachineTypeId")] Machine machine)
    {
        if (!ModelState.IsValid)
        {
            await PopulateTypesAsync(machine.MachineTypeId);
            return View(machine);
        }

        var result = await _machines.CreateAsync(machine);
        if (!result.Success)
        {
            ModelState.AddModelError(string.Empty, result.Error!);
            await PopulateTypesAsync(machine.MachineTypeId);
            return View(machine);
        }

        TempData["Success"] = "Machine created.";
        return RedirectToAction(nameof(Index));
    }

    [Authorize(Roles = DbSeeder.AdminRole)]
    [HttpGet]
    public async Task<IActionResult> Edit(int id)
    {
        var machine = await _machines.GetByIdAsync(id);
        if (machine is null) return NotFound();
        await PopulateTypesAsync(machine.MachineTypeId);
        return View(machine);
    }

    [Authorize(Roles = DbSeeder.AdminRole)]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, [Bind("MachineId,MachineName,MachineCode,MachineTypeId,IsActive")] Machine machine)
    {
        if (id != machine.MachineId) return BadRequest();
        if (!ModelState.IsValid)
        {
            await PopulateTypesAsync(machine.MachineTypeId);
            return View(machine);
        }

        var result = await _machines.UpdateAsync(machine);
        if (!result.Success)
        {
            ModelState.AddModelError(string.Empty, result.Error!);
            await PopulateTypesAsync(machine.MachineTypeId);
            return View(machine);
        }

        TempData["Success"] = "Machine updated.";
        return RedirectToAction(nameof(Index));
    }

    [Authorize(Roles = DbSeeder.AdminRole)]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Deactivate(int id)
    {
        var result = await _machines.DeactivateAsync(id);
        TempData[result.Success ? "Success" : "Error"] = result.Success ? "Machine deactivated." : result.Error;
        return RedirectToAction(nameof(Index));
    }

    private async Task PopulateTypesAsync(int? selected)
    {
        var types = await _machines.GetActiveTypesAsync();
        ViewBag.MachineTypes = new SelectList(types, nameof(MachineType.MachineTypeId), nameof(MachineType.Name), selected);
    }
}
