using BetaPlatform.Data;
using BetaPlatform.Data.Entities;
using BetaPlatform.Services;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace BetaPlatform.Tests;

/// <summary>
/// The invariant that lets the existing Work Orders screens and the running-orders reporting view
/// go on reading <c>work_orders.input_product_id</c> untouched: position 0 of an order's input-
/// product list is always the same product as that column (006 FR-025, SC-010).
/// </summary>
public class WorkOrderInputProductTests
{
    private static async Task<(int machineId, int[] productIds)> SeedAsync(ApplicationDbContext db)
    {
        var machine = new Machine { MachineCode = "M-1", MachineName = "Machine 1", MachineTypeId = 1, IsActive = true };
        var products = new[]
        {
            new Product { ProductCode = "P-1", ProductName = "One", Unit = "kg" },
            new Product { ProductCode = "P-2", ProductName = "Two", Unit = "kg" },
            new Product { ProductCode = "P-3", ProductName = "Three", Unit = "kg" },
            new Product { ProductCode = "P-OUT", ProductName = "Out", Unit = "kg" }
        };
        db.Machines.Add(machine);
        db.Products.AddRange(products);
        await db.SaveChangesAsync();
        return (machine.MachineId, products.Select(p => p.ProductId).ToArray());
    }

    private static WorkOrder NewOrder(string number, int machineId, int inputId, int outputId) => new()
    {
        WorkOrderNumber = number,
        MachineId = machineId,
        InputProductId = inputId,
        OutputProductId = outputId,
        PlannedStartTime = new DateTime(2026, 9, 1, 8, 0, 0),
        QtyToManufacture = 100
    };

    /// <summary>
    /// An order raised on the Create screen supplies no list at all, and still gets one. There are
    /// not two kinds of order (006 FR-027).
    /// </summary>
    [Fact]
    public async Task Create_From_The_Screen_Still_Gets_An_Input_Product_Row()
    {
        using var db = TestDb.Create();
        var (machineId, ids) = await SeedAsync(db);

        var result = await new WorkOrderService(db).CreateAsync(NewOrder("WO-1", machineId, ids[0], ids[3]));

        Assert.True(result.Success);
        var row = Assert.Single(db.WorkOrderInputProducts);
        Assert.Equal(ids[0], row.ProductId);
        Assert.Equal(0, row.Position);
    }

    /// <summary>
    /// The Edit screen carries one input dropdown, so it can only say what the PRIMARY input now is.
    /// Changing it REPLACES the primary — the same thing it has always meant for a single-input
    /// order — while the order's further inputs are left alone. An order raised through the API with
    /// three materials must not lose the other two because somebody changed the dropdown.
    /// </summary>
    [Fact]
    public async Task Edit_Replaces_The_Primary_And_Keeps_The_Other_Inputs()
    {
        using var db = TestDb.Create();
        var (machineId, ids) = await SeedAsync(db);
        var svc = new WorkOrderService(db);

        var order = NewOrder("WO-1", machineId, ids[0], ids[3]);
        order.InputProducts.Add(new WorkOrderInputProduct { ProductId = ids[0], Position = 0 });
        order.InputProducts.Add(new WorkOrderInputProduct { ProductId = ids[1], Position = 1 });
        order.InputProducts.Add(new WorkOrderInputProduct { ProductId = ids[2], Position = 2 });
        await svc.CreateAsync(order);

        // The screen changes only the single input selection.
        var edit = NewOrder("WO-1", machineId, ids[2], ids[3]);
        edit.WorkOrderId = order.WorkOrderId;
        var result = await svc.UpdateAsync(edit);

        Assert.True(result.Success);

        var inputs = await db.WorkOrderInputProducts.OrderBy(p => p.Position).ToListAsync();

        // P-3 is now primary, replacing P-1. P-2 — which the dropdown never spoke for — survives,
        // and P-3 is not listed twice even though it was already an input.
        Assert.Equal([ids[2], ids[1]], inputs.Select(i => i.ProductId));
        Assert.Equal([0, 1], inputs.Select(i => i.Position));
        Assert.Equal(inputs[0].ProductId, db.WorkOrders.Single().InputProductId);
    }

    /// <summary>
    /// The single-input case, which is every order raised on the screen: swapping the input product
    /// leaves exactly one input, not two. This is the behaviour the Edit screen has always had and
    /// the reason replacing beats accumulating.
    /// </summary>
    [Fact]
    public async Task Edit_To_An_Input_The_Order_Already_Had_Does_Not_Duplicate_It()
    {
        using var db = TestDb.Create();
        var (machineId, ids) = await SeedAsync(db);
        var svc = new WorkOrderService(db);

        var order = NewOrder("WO-1", machineId, ids[0], ids[3]);
        order.InputProducts.Add(new WorkOrderInputProduct { ProductId = ids[0], Position = 0 });
        order.InputProducts.Add(new WorkOrderInputProduct { ProductId = ids[1], Position = 1 });
        await svc.CreateAsync(order);

        var edit = NewOrder("WO-1", machineId, ids[1], ids[3]);
        edit.WorkOrderId = order.WorkOrderId;
        await svc.UpdateAsync(edit);

        // P-2 replaced P-1 as primary and was already present, so the order is left with P-2 alone.
        var row = Assert.Single(await db.WorkOrderInputProducts.ToListAsync());
        Assert.Equal(ids[1], row.ProductId);
        Assert.Equal(0, row.Position);
        Assert.Equal(ids[1], db.WorkOrders.Single().InputProductId);
    }

    /// <summary>The details read carries the products, so the screen can render them (006 FR-024).</summary>
    [Fact]
    public async Task GetById_Includes_The_Input_Products_In_Order()
    {
        using var db = TestDb.Create();
        var (machineId, ids) = await SeedAsync(db);
        var svc = new WorkOrderService(db);

        var order = NewOrder("WO-1", machineId, ids[0], ids[3]);
        order.InputProducts.Add(new WorkOrderInputProduct { ProductId = ids[0], Position = 0 });
        order.InputProducts.Add(new WorkOrderInputProduct { ProductId = ids[1], Position = 1 });
        await svc.CreateAsync(order);

        var loaded = await svc.GetByIdAsync(order.WorkOrderId);

        Assert.NotNull(loaded);
        Assert.Equal(
            ["P-1", "P-2"],
            loaded!.InputProducts.OrderBy(p => p.Position).Select(p => p.Product!.ProductCode));
    }

    /// <summary>
    /// The weight records from 003 and the input products from 006 are different things and must
    /// stay so: one says how much was fed in, the other says which products those were.
    /// </summary>
    [Fact]
    public async Task Input_Weights_And_Input_Products_Are_Separate()
    {
        using var db = TestDb.Create();
        var (machineId, ids) = await SeedAsync(db);
        var svc = new WorkOrderService(db);

        var order = NewOrder("WO-1", machineId, ids[0], ids[3]);
        await svc.CreateAsync(order);
        await svc.AddInputAsync(order.WorkOrderId, 250m);

        Assert.Single(db.WorkOrderInputs);         // a weight, no product
        Assert.Single(db.WorkOrderInputProducts);  // a product, no weight
        Assert.Equal(250m, db.WorkOrderInputs.Single().Weight);
    }
}
