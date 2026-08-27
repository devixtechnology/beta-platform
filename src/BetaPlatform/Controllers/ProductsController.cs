using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using BetaPlatform.Data;
using BetaPlatform.Data.Entities;
using BetaPlatform.Services;

namespace BetaPlatform.Controllers;

public class ProductsController : Controller
{
    private readonly IProductService _products;

    public ProductsController(IProductService products) => _products = products;

    public async Task<IActionResult> Index(string? q)
    {
        ViewData["Query"] = q;
        var products = await _products.SearchAsync(q);
        return View(products);
    }

    [Authorize(Roles = DbSeeder.AdminRole)]
    [HttpGet]
    public IActionResult Create() => View(new Product());

    [Authorize(Roles = DbSeeder.AdminRole)]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create([Bind("ProductCode,ProductName,ProductNameEnglish,Category,Unit")] Product product)
    {
        if (!ModelState.IsValid) return View(product);

        var result = await _products.CreateAsync(product);
        if (!result.Success)
        {
            ModelState.AddModelError(string.Empty, result.Error!);
            return View(product);
        }

        TempData["Success"] = "Product created.";
        return RedirectToAction(nameof(Index));
    }

    [Authorize(Roles = DbSeeder.AdminRole)]
    [HttpGet]
    public async Task<IActionResult> Edit(int id)
    {
        var product = await _products.GetByIdAsync(id);
        if (product is null) return NotFound();
        return View(product);
    }

    [Authorize(Roles = DbSeeder.AdminRole)]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, [Bind("ProductId,ProductCode,ProductName,ProductNameEnglish,Category,Unit,IsActive")] Product product)
    {
        if (id != product.ProductId) return BadRequest();
        if (!ModelState.IsValid) return View(product);

        var result = await _products.UpdateAsync(product);
        if (!result.Success)
        {
            ModelState.AddModelError(string.Empty, result.Error!);
            return View(product);
        }

        TempData["Success"] = "Product updated.";
        return RedirectToAction(nameof(Index));
    }

    [Authorize(Roles = DbSeeder.AdminRole)]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Deactivate(int id)
    {
        var result = await _products.DeactivateAsync(id);
        TempData[result.Success ? "Success" : "Error"] = result.Success ? "Product deactivated." : result.Error;
        return RedirectToAction(nameof(Index));
    }
}
