using Microsoft.EntityFrameworkCore;
using BetaPlatform.Data;
using BetaPlatform.Data.Entities;
using BetaPlatform.Services.Api;

namespace BetaPlatform.Services;

public interface IProductService
{
    Task<List<Product>> SearchAsync(string? term);
    Task<List<Product>> GetActiveAsync();
    Task<Product?> GetByIdAsync(int id);

    /// <summary>
    /// One product by the code the plant prints and files by — trimmed, matched case-insensitively.
    /// Null when no product carries it. Returns a deactivated product like any other: "never
    /// existed" and "no longer used" are different answers (006 FR-003/FR-004).
    /// </summary>
    Task<Product?> GetByCodeAsync(string? code);

    /// <summary>
    /// Every product carrying one of <paramref name="codes"/>, matched the same way as
    /// <see cref="GetByCodeAsync"/>. One round trip rather than one per code, because a work order
    /// resolves its whole list before storing anything (006 FR-012).
    /// </summary>
    Task<List<Product>> GetByCodesAsync(IEnumerable<string> codes);
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

    public Task<Product?> GetByCodeAsync(string? code)
    {
        var normalised = ProductCode.Normalise(code);

        // An empty code is not an identity and must never match — including matching another empty
        // one. Asked of the database, `ProductCode == ""` could match a row that should not exist;
        // refusing here means it cannot.
        if (normalised.Length == 0)
        {
            return Task.FromResult<Product?>(null);
        }

        // Lower-cased on both sides ON PURPOSE rather than leaning on MySQL's case-insensitive
        // default collation. Relying on the collation would make this agree with
        // ProductCode.Matches by accident of a server setting: change the collation, or run against
        // any other provider, and the API quietly starts refusing codes the screens accept
        // (005 research R9 warns about exactly this discrepancy).
        var lowered = normalised.ToLower();
        return _db.Products.FirstOrDefaultAsync(p => p.ProductCode.ToLower() == lowered);
    }

    public Task<List<Product>> GetByCodesAsync(IEnumerable<string> codes)
    {
        // Lower-cased for the same reason as GetByCodeAsync: the comparison is stated here, not
        // inherited from the server's collation.
        var lowered = codes
            .Select(c => ProductCode.Normalise(c).ToLower())
            .Where(c => c.Length > 0)
            .Distinct()
            .ToList();

        if (lowered.Count == 0)
        {
            return Task.FromResult(new List<Product>());
        }

        return _db.Products.Where(p => lowered.Contains(p.ProductCode.ToLower())).ToListAsync();
    }

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
