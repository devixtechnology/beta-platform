using System.Diagnostics;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using BetaPlatform.Models;

namespace BetaPlatform.Controllers;

/// <summary>
/// Minimal host for the production error page (referenced by UseExceptionHandler("/Home/Error")).
/// Not part of the sidebar navigation.
/// </summary>
[AllowAnonymous]
public class HomeController : Controller
{
    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error()
    {
        return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
    }
}
