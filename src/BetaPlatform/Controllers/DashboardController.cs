using Microsoft.AspNetCore.Mvc;
using BetaPlatform.Services;

namespace BetaPlatform.Controllers;

public class DashboardController : Controller
{
    private readonly IDashboardService _dashboard;

    public DashboardController(IDashboardService dashboard) => _dashboard = dashboard;

    public async Task<IActionResult> Index()
    {
        var vm = await _dashboard.GetAsync();
        return View(vm);
    }

    /// <summary>JSON endpoint polled every ~5s by the dashboard (FR-043) and by the production
    /// display. Read-only.</summary>
    [HttpGet]
    public async Task<IActionResult> Data()
    {
        var vm = await _dashboard.GetAsync();
        return Json(vm);
    }

    /// <summary>
    /// The chromeless big screen for the production floor (004, client comment 6). It renders the
    /// same view model the dashboard does and then polls the same <see cref="Data"/> endpoint, so
    /// the wall can never disagree with the dashboard (FR-002).
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> Display()
    {
        var vm = await _dashboard.GetAsync();
        return View(vm);
    }
}
