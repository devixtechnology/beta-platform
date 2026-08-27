using BetaPlatform.Data.Entities;
using BetaPlatform.Services;
using Xunit;

namespace BetaPlatform.Tests;

public class ProductServiceTests
{
    private static Product NewProduct(string code = "P-01", string name = "Widget") =>
        new() { ProductCode = code, ProductName = name, Unit = "kg" };

    [Fact]
    public async Task Create_Rejects_Duplicate_Code()
    {
        using var db = TestDb.Create();
        var svc = new ProductService(db);
        await svc.CreateAsync(NewProduct(code: "DUP"));

        var result = await svc.CreateAsync(NewProduct(code: "DUP", name: "Other"));

        Assert.False(result.Success);
    }

    [Fact]
    public async Task Search_Filters_By_Term()
    {
        using var db = TestDb.Create();
        var svc = new ProductService(db);
        await svc.CreateAsync(NewProduct(code: "A1", name: "Copper Rod"));
        await svc.CreateAsync(NewProduct(code: "B2", name: "Steel Washer"));

        var results = await svc.SearchAsync("washer");

        Assert.Single(results);
        Assert.Equal("Steel Washer", results[0].ProductName);
    }

    [Fact]
    public async Task Deactivate_Hides_From_Active_But_Keeps_Record()
    {
        using var db = TestDb.Create();
        var svc = new ProductService(db);
        var created = await svc.CreateAsync(NewProduct());

        await svc.DeactivateAsync(created.Value!.ProductId);

        Assert.Empty(await svc.GetActiveAsync());
        Assert.NotNull(await svc.GetByIdAsync(created.Value!.ProductId));
    }
}
