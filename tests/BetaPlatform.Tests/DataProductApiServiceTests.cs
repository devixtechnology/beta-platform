using BetaPlatform.Data;
using BetaPlatform.Data.Entities;
using BetaPlatform.Services;
using BetaPlatform.Services.Api;
using BetaPlatform.ViewModels.Api;
using Xunit;

namespace BetaPlatform.Tests;

/// <summary>
/// The product operations answered from stored data (006 US1/US2) — the behaviour that replaced the
/// representative responses 005 shipped.
/// </summary>
public class DataProductApiServiceTests
{
    private static DataProductApiService Create(ApplicationDbContext db) =>
        new(new ProductService(db));

    private static async Task SeedCatalogueAsync(ApplicationDbContext db)
    {
        db.Products.AddRange(
            new Product { ProductCode = "RM-STEEL-01", ProductName = "لفائف صلب", ProductNameEnglish = "Steel Coil", Category = "Raw Material", Unit = "kg", IsActive = true },
            new Product { ProductCode = "FG-PANEL-07", ProductName = "لوح معدني", ProductNameEnglish = "Metal Panel", Category = "Finished Goods", Unit = "pcs", IsActive = true },
            // No English name, no category — the optional fields really are optional.
            new Product { ProductCode = "RM-RESIN-04", ProductName = "راتنج", Unit = "kg", IsActive = true },
            new Product { ProductCode = "RM-LEGACY-99", ProductName = "مادة قديمة", ProductNameEnglish = "Legacy", Category = "Raw Material", Unit = "kg", IsActive = false });
        await db.SaveChangesAsync();
    }

    [Fact]
    public async Task GetAll_Returns_Stored_Catalogue_Including_Deactivated()
    {
        using var db = TestDb.Create();
        await SeedCatalogueAsync(db);

        var result = await Create(db).GetAllAsync(activeOnly: false);

        Assert.Equal(ApiOutcome.Success, result.Outcome);
        Assert.Equal(4, result.Value!.Count);
        Assert.Contains(result.Value, p => p.ProductCode == "RM-LEGACY-99" && !p.IsActive);
    }

    [Fact]
    public async Task GetAll_ActiveOnly_Excludes_Deactivated()
    {
        using var db = TestDb.Create();
        await SeedCatalogueAsync(db);

        var result = await Create(db).GetAllAsync(activeOnly: true);

        Assert.Equal(3, result.Value!.Count);
        Assert.DoesNotContain(result.Value, p => p.ProductCode == "RM-LEGACY-99");
    }

    /// <summary>An empty catalogue is an empty list, never a 404 (006 FR-001).</summary>
    [Fact]
    public async Task GetAll_Empty_Catalogue_Is_Success_With_No_Rows()
    {
        using var db = TestDb.Create();

        var result = await Create(db).GetAllAsync(activeOnly: false);

        Assert.Equal(ApiOutcome.Success, result.Outcome);
        Assert.Empty(result.Value!);
    }

    [Fact]
    public async Task GetAll_Preserves_Absent_Optional_Fields()
    {
        using var db = TestDb.Create();
        await SeedCatalogueAsync(db);

        var resin = (await Create(db).GetAllAsync(activeOnly: false)).Value!
            .Single(p => p.ProductCode == "RM-RESIN-04");

        // Absent, not blanked into an empty string a client would render as a name.
        Assert.Null(resin.ProductNameEnglish);
        Assert.Null(resin.Category);
    }

    [Theory]
    [InlineData("RM-STEEL-01")]
    [InlineData("rm-steel-01")]
    [InlineData("  RM-Steel-01  ")]
    public async Task GetByCode_Matches_Trimmed_And_Case_Insensitively(string code)
    {
        using var db = TestDb.Create();
        await SeedCatalogueAsync(db);

        var result = await Create(db).GetByCodeAsync(code);

        Assert.Equal(ApiOutcome.Success, result.Outcome);
        Assert.Equal("RM-STEEL-01", result.Value!.ProductCode);
    }

    /// <summary>
    /// "Never existed" and "no longer used" are different answers, so a deactivated product is
    /// returned rather than hidden (006 FR-004).
    /// </summary>
    [Fact]
    public async Task GetByCode_Returns_Deactivated_Product_Rather_Than_NotFound()
    {
        using var db = TestDb.Create();
        await SeedCatalogueAsync(db);

        var result = await Create(db).GetByCodeAsync("RM-LEGACY-99");

        Assert.Equal(ApiOutcome.Success, result.Outcome);
        Assert.False(result.Value!.IsActive);
    }

    [Theory]
    [InlineData("NOPE-00")]
    [InlineData("")]
    [InlineData("   ")]
    public async Task GetByCode_Unknown_Or_Blank_Is_NotFound(string code)
    {
        using var db = TestDb.Create();
        await SeedCatalogueAsync(db);

        var result = await Create(db).GetByCodeAsync(code);

        Assert.Equal(ApiOutcome.NotFound, result.Outcome);
    }

    [Fact]
    public async Task Create_Persists_The_Product()
    {
        using var db = TestDb.Create();
        var request = new CreateProductRequest
        {
            ProductCode = "  RM-NEW-01  ",
            ProductName = "منتج جديد",
            ProductNameEnglish = "New Product",
            Category = "Raw Material",
            Unit = "kg"
        };

        var result = await Create(db).CreateAsync(request);

        Assert.Equal(ApiOutcome.Success, result.Outcome);

        // Really stored, and stored trimmed — the plant treats a code as a printed label, so
        // accidental padding is not part of its identity.
        var stored = Assert.Single(db.Products);
        Assert.Equal("RM-NEW-01", stored.ProductCode);
        Assert.Equal("RM-NEW-01", result.Value!.ProductCode);
    }

    /// <summary>Always active on creation — the request has no field to say otherwise (005 FR-017).</summary>
    [Fact]
    public async Task Create_Always_Produces_An_Active_Product()
    {
        using var db = TestDb.Create();

        var result = await Create(db).CreateAsync(new CreateProductRequest
        {
            ProductCode = "RM-NEW-02",
            ProductName = "اسم",
            Unit = "kg"
        });

        Assert.True(result.Value!.IsActive);
        Assert.True(db.Products.Single().IsActive);
    }

    /// <summary>
    /// A duplicate code is a 409, not a 400: the request is well formed, the stored data disagrees
    /// with it (006 FR-009).
    /// </summary>
    [Fact]
    public async Task Create_Duplicate_Code_Is_Conflict_And_Stores_Nothing()
    {
        using var db = TestDb.Create();
        await SeedCatalogueAsync(db);
        var before = db.Products.Count();

        var result = await Create(db).CreateAsync(new CreateProductRequest
        {
            ProductCode = "RM-STEEL-01",
            ProductName = "تكرار",
            Unit = "kg"
        });

        Assert.Equal(ApiOutcome.Conflict, result.Outcome);
        Assert.Equal(before, db.Products.Count());
    }

    /// <summary>
    /// No response carries the internal record number, in any casing — the promise 005 made and the
    /// one most easily broken now that responses are built from records that have one (FR-006).
    /// </summary>
    [Fact]
    public void ProductResponse_Exposes_No_Product_Id()
    {
        var names = typeof(ProductResponse).GetProperties().Select(p => p.Name.ToLowerInvariant());
        Assert.DoesNotContain("productid", names);
    }
}
