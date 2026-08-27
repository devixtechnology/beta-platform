using Microsoft.EntityFrameworkCore;
using BetaPlatform.Data;
using BetaPlatform.Data.Entities;

namespace BetaPlatform.Services;

public interface IProductService
{
    Task<List<Product>> SearchAsync(string? term);
    Task<List<Product>> GetActiveAsync();
    Task<Product?> GetByIdAsync(int id);
    Task<ServiceResult<Product>> CreateAsync(Product product);
    Task<ServiceResult<Product>> UpdateAsync(Product product);
    Task<ServiceResult> DeactivateAsync(int id);
}

public class ProductService : IProductService
{
    private readonly ApplicationDbContext _db;

    public ProductService(ApplicationDbContext db) => _db = db;

    public Task<List<Product>> SearchAsync(string? term)
    {
        var query = _db.Products.AsQueryable();
        if (!string.IsNullOrWhiteSpace(term))
        {
            var t = term.Trim().ToLower();
            query = query.Where(p =>
                p.ProductCode.ToLower().Contains(t) ||
                p.ProductName.ToLower().Contains(t) ||
                (p.ProductNameEnglish != null && p.ProductNameEnglish.ToLower().Contains(t)) ||
                (p.Category != null && p.Category.ToLower().Contains(t)));
        }
        return query.OrderBy(p => p.ProductName).ToListAsync();
    }

    public Task<List<Product>> GetActiveAsync() =>
        _db.Products.Where(p => p.IsActive).OrderBy(p => p.ProductName).ToListAsync();

    public Task<Product?> GetByIdAsync(int id) =>
        _db.Products.FirstOrDefaultAsync(p => p.ProductId == id);

    public async Task<ServiceResult<Product>> CreateAsync(Product product)
    {
        if (await CodeExistsAsync(product.ProductCode, null))
            return ServiceResult<Product>.Fail($"Product code '{product.ProductCode}' already exists.");

        _db.Products.Add(product);
        await _db.SaveChangesAsync();
        return ServiceResult<Product>.Ok(product);
    }

    public async Task<ServiceResult<Product>> UpdateAsync(Product product)
    {
        var existing = await _db.Products.FirstOrDefaultAsync(p => p.ProductId == product.ProductId);
        if (existing is null)
            return ServiceResult<Product>.Fail("Product not found.");
        if (await CodeExistsAsync(product.ProductCode, product.ProductId))
            return ServiceResult<Product>.Fail($"Product code '{product.ProductCode}' already exists.");

        existing.ProductCode = product.ProductCode;
        existing.ProductName = product.ProductName;
        existing.ProductNameEnglish = product.ProductNameEnglish;
        existing.Category = product.Category;
        existing.Unit = product.Unit;
        existing.IsActive = product.IsActive;
        await _db.SaveChangesAsync();
        return ServiceResult<Product>.Ok(existing);
    }

    public async Task<ServiceResult> DeactivateAsync(int id)
    {
        var product = await _db.Products.FirstOrDefaultAsync(p => p.ProductId == id);
        if (product is null)
            return ServiceResult.Fail("Product not found.");

        // Hide from new selections; existing work-order references remain valid (FR-024/FR-052).
        product.IsActive = false;
        await _db.SaveChangesAsync();
        return ServiceResult.Ok();
    }

    private Task<bool> CodeExistsAsync(string code, int? excludeId) =>
        _db.Products.AnyAsync(p => p.ProductCode == code && (excludeId == null || p.ProductId != excludeId));
}
