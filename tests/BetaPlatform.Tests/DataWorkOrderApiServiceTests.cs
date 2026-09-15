using BetaPlatform.Data;
using BetaPlatform.Data.Entities;
using BetaPlatform.Services;
using BetaPlatform.Services.Api;
using BetaPlatform.ViewModels.Api;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Xunit;

namespace BetaPlatform.Tests;

/// <summary>
/// Work orders resolved against the real catalogue and really stored (006 US3/US4).
/// </summary>
public class DataWorkOrderApiServiceTests
{
    private static DataWorkOrderApiService Create(ApplicationDbContext db)
    {
        var telemetry = Options.Create(new TelemetryOptions());
        return new DataWorkOrderApiService(
            new WorkOrderService(db),
            new ProductService(db),
            new MachineService(db, new MachineStatusService(db, telemetry), telemetry));
    }

    private static async Task<int> SeedAsync(ApplicationDbContext db)
    {
        db.Products.AddRange(
            new Product { ProductCode = "RM-STEEL-01", ProductName = "صلب", Unit = "kg", IsActive = true },
            new Product { ProductCode = "RM-PAINT-02", ProductName = "دهان", Unit = "L", IsActive = true },
            new Product { ProductCode = "RM-BOLT-09", ProductName = "مسامير", Unit = "pcs", IsActive = true },
            new Product { ProductCode = "FG-PANEL-07", ProductName = "لوح", Unit = "pcs", IsActive = true },
            new Product { ProductCode = "RM-LEGACY-99", ProductName = "قديم", Unit = "kg", IsActive = false });

        var machine = new Machine { MachineCode = "M-1", MachineName = "Machine 1", MachineTypeId = 1, IsActive = true };
        db.Machines.Add(machine);
        await db.SaveChangesAsync();
        return machine.MachineId;
    }

    private static CreateWorkOrderRequest NewRequest(int machineId, params string[] inputs) => new()
    {
        WorkOrderNumber = "WO-1001",
        InputProductCodes = inputs.Length > 0 ? [.. inputs] : ["RM-STEEL-01"],
        OutputProductCode = "FG-PANEL-07",
        PlannedStartTime = new DateTime(2026, 9, 1, 6, 0, 0),
        QtyToManufacture = 1200.5m,
        MachineId = machineId
    };

    [Fact]
    public async Task Create_Persists_The_Order_In_Ready()
    {
        using var db = TestDb.Create();
        var machineId = await SeedAsync(db);

        var result = await Create(db).CreateAsync(NewRequest(machineId, "RM-STEEL-01", "RM-PAINT-02"));

        Assert.Equal(ApiOutcome.Success, result.Outcome);

        var stored = Assert.Single(db.WorkOrders);
        Assert.Equal("WO-1001", stored.WorkOrderNumber);
        Assert.Equal(WorkOrderStatus.Ready, stored.Status);

        // The NAME, not the enum's integer — the numbering is an internal detail (005 FR-026).
        Assert.Equal("Ready", result.Value!.Status);
    }

    /// <summary>Every input is kept, not just the first (006 FR-022, US3 scenario 8).</summary>
    [Fact]
    public async Task Create_Stores_Every_Input_Product_In_The_Submitted_Order()
    {
        using var db = TestDb.Create();
        var machineId = await SeedAsync(db);

        await Create(db).CreateAsync(NewRequest(machineId, "RM-STEEL-01", "RM-PAINT-02", "RM-BOLT-09"));

        var inputs = await db.WorkOrderInputProducts
            .Include(p => p.Product)
            .OrderBy(p => p.Position)
            .ToListAsync();

        Assert.Equal(3, inputs.Count);
        Assert.Equal(["RM-STEEL-01", "RM-PAINT-02", "RM-BOLT-09"], inputs.Select(i => i.Product!.ProductCode));
        Assert.Equal([0, 1, 2], inputs.Select(i => i.Position));
    }

    /// <summary>
    /// Position 0 is always the same product as the order's existing single reference, so the Work
    /// Orders screens and the running-orders view keep reading the column they already read
    /// (006 FR-025, SC-010).
    /// </summary>
    [Fact]
    public async Task Create_Puts_The_First_Input_In_The_Existing_Column()
    {
        using var db = TestDb.Create();
        var machineId = await SeedAsync(db);

        await Create(db).CreateAsync(NewRequest(machineId, "RM-PAINT-02", "RM-STEEL-01"));

        var order = db.WorkOrders.Single();
        var primary = db.WorkOrderInputProducts.Single(p => p.Position == 0);
        Assert.Equal(order.InputProductId, primary.ProductId);
        Assert.Equal("RM-PAINT-02", db.Products.Single(p => p.ProductId == order.InputProductId).ProductCode);
    }

    [Fact]
    public async Task Create_Echoes_The_Codes_As_Submitted_In_Order()
    {
        using var db = TestDb.Create();
        var machineId = await SeedAsync(db);

        var result = await Create(db).CreateAsync(NewRequest(machineId, "  rm-paint-02 ", "RM-STEEL-01"));

        // Resolved to the stored spelling, in the order the caller listed them.
        Assert.Equal(["RM-PAINT-02", "RM-STEEL-01"], result.Value!.InputProductCodes);
        Assert.Equal("FG-PANEL-07", result.Value.OutputProductCode);
    }

    /// <summary>
    /// The refusal names the entry at the position submitted — a caller with six materials has to be
    /// told WHICH one to fix (006 FR-013).
    /// </summary>
    [Fact]
    public async Task Create_Unresolvable_Input_Is_Invalid_Naming_Its_Position()
    {
        using var db = TestDb.Create();
        var machineId = await SeedAsync(db);

        var result = await Create(db).CreateAsync(NewRequest(machineId, "RM-STEEL-01", "NOPE-00", "RM-BOLT-09"));

        Assert.Equal(ApiOutcome.Invalid, result.Outcome);
        Assert.Contains("inputProductCodes[1]", result.Errors!.Keys);
        Assert.Empty(db.WorkOrders);
    }

    [Fact]
    public async Task Create_Unresolvable_Output_Is_Invalid_Naming_OutputProductCode()
    {
        using var db = TestDb.Create();
        var machineId = await SeedAsync(db);

        var request = NewRequest(machineId, "RM-STEEL-01");
        request.OutputProductCode = "NOPE-00";

        var result = await Create(db).CreateAsync(request);

        Assert.Equal(ApiOutcome.Invalid, result.Outcome);
        Assert.Contains("outputProductCode", result.Errors!.Keys);
        Assert.Empty(db.WorkOrders);
    }

    /// <summary>
    /// All of them in one answer. Reporting one at a time would be correct and useless: three bad
    /// codes would cost three round trips (006 FR-015, SC-007).
    /// </summary>
    [Fact]
    public async Task Create_Names_Every_Unresolvable_Code_In_One_Answer()
    {
        using var db = TestDb.Create();
        var machineId = await SeedAsync(db);

        var request = NewRequest(machineId, "NOPE-A", "RM-STEEL-01", "NOPE-C");
        request.OutputProductCode = "NOPE-OUT";

        var result = await Create(db).CreateAsync(request);

        Assert.Equal(ApiOutcome.Invalid, result.Outcome);
        Assert.Equal(
            ["inputProductCodes[0]", "inputProductCodes[2]", "outputProductCode"],
            result.Errors!.Keys.OrderBy(k => k, StringComparer.Ordinal));
    }

    /// <summary>
    /// Deactivation keeps a product out of NEW work while leaving history intact — the same rule the
    /// Work Orders screen applies by offering only active products (006 FR-028).
    /// </summary>
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Create_Deactivated_Code_Is_Refused_On_Either_Side(bool asInput)
    {
        using var db = TestDb.Create();
        var machineId = await SeedAsync(db);

        var request = NewRequest(machineId, asInput ? "RM-LEGACY-99" : "RM-STEEL-01");
        if (!asInput)
        {
            request.OutputProductCode = "RM-LEGACY-99";
        }

        var result = await Create(db).CreateAsync(request);

        Assert.Equal(ApiOutcome.Invalid, result.Outcome);
        Assert.Contains(asInput ? "inputProductCodes[0]" : "outputProductCode", result.Errors!.Keys);
        Assert.Contains("deactivated", result.Errors.Values.Single().Single());
        Assert.Empty(db.WorkOrders);
    }

    /// <summary>A rework order legitimately consumes and produces the same product (005 FR-044).</summary>
    [Fact]
    public async Task Create_Accepts_The_Same_Code_As_Input_And_Output()
    {
        using var db = TestDb.Create();
        var machineId = await SeedAsync(db);

        var request = NewRequest(machineId, "FG-PANEL-07");

        var result = await Create(db).CreateAsync(request);

        Assert.Equal(ApiOutcome.Success, result.Outcome);
        var order = db.WorkOrders.Single();
        Assert.Equal(order.InputProductId, order.OutputProductId);
    }

    [Fact]
    public async Task Create_Duplicate_Order_Number_Is_Conflict_And_Stores_Nothing()
    {
        using var db = TestDb.Create();
        var machineId = await SeedAsync(db);
        await Create(db).CreateAsync(NewRequest(machineId, "RM-STEEL-01"));

        var result = await Create(db).CreateAsync(NewRequest(machineId, "RM-PAINT-02"));

        Assert.Equal(ApiOutcome.Conflict, result.Outcome);
        Assert.Single(db.WorkOrders);
    }

    /// <summary>
    /// The one internal identifier the contract still takes as a number. An unknown machine is the
    /// caller's mistake, not a foreign-key failure surfacing as a 500 (006 FR-021).
    /// </summary>
    [Fact]
    public async Task Create_Unknown_Machine_Is_Invalid_Not_An_Unhandled_Fault()
    {
        using var db = TestDb.Create();
        await SeedAsync(db);

        var result = await Create(db).CreateAsync(NewRequest(9999, "RM-STEEL-01"));

        Assert.Equal(ApiOutcome.Invalid, result.Outcome);
        Assert.Contains("machineId", result.Errors!.Keys);
        Assert.Empty(db.WorkOrders);
    }

    [Fact]
    public async Task Create_Without_A_Machine_Is_Accepted()
    {
        using var db = TestDb.Create();
        await SeedAsync(db);

        var request = NewRequest(1, "RM-STEEL-01");
        request.MachineId = null;

        var result = await Create(db).CreateAsync(request);

        Assert.Equal(ApiOutcome.Success, result.Outcome);
        Assert.Null(db.WorkOrders.Single().MachineId);
    }

    /// <summary>A refused request leaves nothing behind — no order and no orphaned input rows (FR-020).</summary>
    [Fact]
    public async Task Create_Refused_Leaves_No_Input_Rows()
    {
        using var db = TestDb.Create();
        var machineId = await SeedAsync(db);

        await Create(db).CreateAsync(NewRequest(machineId, "RM-STEEL-01", "NOPE-00"));

        Assert.Empty(db.WorkOrders);
        Assert.Empty(db.WorkOrderInputProducts);
    }

    [Fact]
    public void WorkOrderResponse_Exposes_No_Product_Id()
    {
        var names = typeof(WorkOrderResponse).GetProperties().Select(p => p.Name.ToLowerInvariant());
        Assert.DoesNotContain("productid", names);
        Assert.DoesNotContain("inputproductid", names);
        Assert.DoesNotContain("outputproductid", names);
    }
}
