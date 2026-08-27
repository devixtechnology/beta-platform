using BetaPlatform.Data;
using BetaPlatform.Data.Entities;
using BetaPlatform.Services;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace BetaPlatform.Tests;

public class WorkOrderServiceTests
{
    private static async Task<(int machineId, int inputId, int outputId)> SeedRefsAsync(ApplicationDbContext db, int machineNo = 1)
    {
        var machine = new Machine { MachineCode = $"M-{machineNo}", MachineName = $"Machine {machineNo}", MachineTypeId = 1, IsActive = true };
        var input = new Product { ProductCode = $"IN-{machineNo}", ProductName = "Input", Unit = "kg" };
        var output = new Product { ProductCode = $"OUT-{machineNo}", ProductName = "Output", Unit = "kg" };
        db.Machines.Add(machine);
        db.Products.AddRange(input, output);
        await db.SaveChangesAsync();
        return (machine.MachineId, input.ProductId, output.ProductId);
    }

    private static WorkOrder NewOrder(string number, int machineId, int inId, int outId) => new()
    {
        WorkOrderNumber = number,
        MachineId = machineId,
        InputProductId = inId,
        OutputProductId = outId,
        PlannedStartTime = new DateTime(2026, 7, 7, 8, 0, 0),
        QtyToManufacture = 100
    };

    [Theory]
    [InlineData(WorkOrderStatus.Ready, WorkOrderStatus.InProgress, true)]
    [InlineData(WorkOrderStatus.InProgress, WorkOrderStatus.OnHold, true)]
    [InlineData(WorkOrderStatus.OnHold, WorkOrderStatus.InProgress, true)]
    [InlineData(WorkOrderStatus.InProgress, WorkOrderStatus.Finished, true)]
    [InlineData(WorkOrderStatus.Ready, WorkOrderStatus.Finished, false)]
    [InlineData(WorkOrderStatus.Ready, WorkOrderStatus.OnHold, false)]
    [InlineData(WorkOrderStatus.Finished, WorkOrderStatus.InProgress, false)]
    [InlineData(WorkOrderStatus.OnHold, WorkOrderStatus.Finished, false)]
    public void IsValidTransition_Matches_StateMachine(WorkOrderStatus from, WorkOrderStatus to, bool expected)
    {
        using var db = TestDb.Create();
        var svc = new WorkOrderService(db);
        Assert.Equal(expected, svc.IsValidTransition(from, to));
    }

    [Fact]
    public async Task Start_Sets_InProgress_And_StartedAt()
    {
        using var db = TestDb.Create();
        var svc = new WorkOrderService(db);
        var (m, i, o) = await SeedRefsAsync(db);
        var created = await svc.CreateAsync(NewOrder("WO-1", m, i, o));

        var result = await svc.StartAsync(created.Value!.WorkOrderId);

        Assert.True(result.Success);
        var order = await svc.GetByIdAsync(created.Value!.WorkOrderId);
        Assert.Equal(WorkOrderStatus.InProgress, order!.Status);
        Assert.NotNull(order.StartedAt);
    }

    [Fact]
    public async Task Finish_From_Ready_Is_Rejected()
    {
        using var db = TestDb.Create();
        var svc = new WorkOrderService(db);
        var (m, i, o) = await SeedRefsAsync(db);
        var created = await svc.CreateAsync(NewOrder("WO-1", m, i, o));

        var result = await svc.FinishAsync(created.Value!.WorkOrderId);

        Assert.False(result.Success);
    }

    [Fact]
    public async Task Create_Rejects_Duplicate_Number()
    {
        using var db = TestDb.Create();
        var svc = new WorkOrderService(db);
        var (m, i, o) = await SeedRefsAsync(db);
        await svc.CreateAsync(NewOrder("WO-DUP", m, i, o));

        var result = await svc.CreateAsync(NewOrder("WO-DUP", m, i, o));

        Assert.False(result.Success);
    }

    [Fact]
    public async Task Single_Active_Machine_Rule_Blocks_Then_Frees()
    {
        using var db = TestDb.Create();
        var svc = new WorkOrderService(db);
        var (m, i, o) = await SeedRefsAsync(db);
        var first = (await svc.CreateAsync(NewOrder("WO-1", m, i, o))).Value!;
        var second = (await svc.CreateAsync(NewOrder("WO-2", m, i, o))).Value!;

        Assert.True((await svc.StartAsync(first.WorkOrderId)).Success);

        // Second start blocked while first is active.
        var blocked = await svc.StartAsync(second.WorkOrderId);
        Assert.False(blocked.Success);

        // Finish the first, machine frees.
        Assert.True((await svc.FinishAsync(first.WorkOrderId)).Success);
        Assert.True((await svc.StartAsync(second.WorkOrderId)).Success);
    }

    // Superseded 2026-08-27: On Hold used to keep occupying the machine (the original FR-039
    // reading). Under the reference project's model, holding is what frees a machine — covered by
    // Holding_Frees_The_Machine_For_Another_Order below.

    [Fact]
    public async Task AddInput_Aggregates_Weight_And_Sequences()
    {
        using var db = TestDb.Create();
        var svc = new WorkOrderService(db);
        var (m, i, o) = await SeedRefsAsync(db);
        var order = (await svc.CreateAsync(NewOrder("WO-1", m, i, o))).Value!;
        await svc.StartAsync(order.WorkOrderId);

        var first = await svc.AddInputAsync(order.WorkOrderId, 12.5m);
        var second = await svc.AddInputAsync(order.WorkOrderId, 7.5m);

        // Inputs carry only a weight (no code/tracing); order/identity comes from the PK.
        Assert.True(first.Success);
        Assert.True(second.Success);
        Assert.True(second.Value!.InputId > first.Value!.InputId);

        var reloaded = await svc.GetByIdAsync(order.WorkOrderId);
        Assert.Equal(2, reloaded!.Inputs.Count);
        Assert.Equal(20.0m, reloaded.TotalInputWeight);
    }

    [Fact]
    public async Task AddInput_Rejects_NonPositive_Weight()
    {
        using var db = TestDb.Create();
        var svc = new WorkOrderService(db);
        var (m, i, o) = await SeedRefsAsync(db);
        var order = (await svc.CreateAsync(NewOrder("WO-1", m, i, o))).Value!;
        await svc.StartAsync(order.WorkOrderId);

        var result = await svc.AddInputAsync(order.WorkOrderId, 0m);

        Assert.False(result.Success);
    }

    [Fact]
    public async Task Finish_With_Zero_Inputs_Is_Allowed()
    {
        using var db = TestDb.Create();
        var svc = new WorkOrderService(db);
        var (m, i, o) = await SeedRefsAsync(db);
        var order = (await svc.CreateAsync(NewOrder("WO-1", m, i, o))).Value!;
        await svc.StartAsync(order.WorkOrderId);

        var result = await svc.FinishAsync(order.WorkOrderId);

        Assert.True(result.Success);
        var reloaded = await svc.GetByIdAsync(order.WorkOrderId);
        Assert.Equal(WorkOrderStatus.Finished, reloaded!.Status);
        Assert.Empty(reloaded.Inputs);
    }

    [Fact]
    public async Task GetLiveTotals_Returns_Latest_Oee_For_Order()
    {
        using var db = TestDb.Create();
        var svc = new WorkOrderService(db);
        var (m, i, o) = await SeedRefsAsync(db);
        var order = (await svc.CreateAsync(NewOrder("WO-1", m, i, o))).Value!;

        db.OeeData.Add(new Data.Entities.OeeData { MachineId = m, OrderId = order.WorkOrderId, Timestamp = new DateTime(2026, 7, 7, 9, 0, 0), TotalWeight = 10m, TotalCount = 5m, Status = 1 });
        db.OeeData.Add(new Data.Entities.OeeData { MachineId = m, OrderId = order.WorkOrderId, Timestamp = new DateTime(2026, 7, 7, 11, 0, 0), TotalWeight = 42m, TotalCount = 21m, Status = 1 });
        await db.SaveChangesAsync();

        var totals = await svc.GetLiveTotalsAsync(order.WorkOrderId);

        Assert.Equal(42m, totals.TotalWeight);
        Assert.Equal(21m, totals.TotalCount);
    }

    // ---- Hold-aware runtime accounting, ported from the reference project ----

    /// <summary>Rewinds the current segment's start so a measurable amount of runtime has elapsed
    /// without the test having to wait for it.</summary>
    private static async Task BackdateSegmentAsync(ApplicationDbContext db, int workOrderId, double minutes)
    {
        var order = await db.WorkOrders.FirstAsync(w => w.WorkOrderId == workOrderId);
        order.StartedAt = order.StartedAt!.Value.AddMinutes(-minutes);
        await db.SaveChangesAsync();
    }

    [Fact]
    public async Task Start_Stamps_First_Started_At_And_Resets_The_Accumulator()
    {
        using var db = TestDb.Create();
        var svc = new WorkOrderService(db);
        var (m, i, o) = await SeedRefsAsync(db);
        var order = (await svc.CreateAsync(NewOrder("WO-1", m, i, o))).Value!;

        Assert.True((await svc.StartAsync(order.WorkOrderId)).Success);

        var saved = await db.WorkOrders.FirstAsync(w => w.WorkOrderId == order.WorkOrderId);
        Assert.NotNull(saved.FirstStartedAt);
        Assert.Equal(saved.StartedAt, saved.FirstStartedAt);
        Assert.Equal(0m, saved.TotalRuntime);
    }

    [Fact]
    public async Task Hold_Banks_The_Segment_And_Resume_Starts_A_New_One()
    {
        using var db = TestDb.Create();
        var svc = new WorkOrderService(db);
        var (m, i, o) = await SeedRefsAsync(db);
        var order = (await svc.CreateAsync(NewOrder("WO-1", m, i, o))).Value!;
        await svc.StartAsync(order.WorkOrderId);
        var firstStart = (await db.WorkOrders.FirstAsync(w => w.WorkOrderId == order.WorkOrderId)).FirstStartedAt;
        await BackdateSegmentAsync(db, order.WorkOrderId, 30);

        Assert.True((await svc.HoldAsync(order.WorkOrderId)).Success);

        var held = await db.WorkOrders.FirstAsync(w => w.WorkOrderId == order.WorkOrderId);
        Assert.Equal(WorkOrderStatus.OnHold, held.Status);
        Assert.InRange(held.TotalRuntime, 29.9m, 30.1m);

        Assert.True((await svc.ResumeAsync(order.WorkOrderId)).Success);

        var resumed = await db.WorkOrders.FirstAsync(w => w.WorkOrderId == order.WorkOrderId);
        Assert.Equal(WorkOrderStatus.InProgress, resumed.Status);
        // A new segment begins, the original start and the banked minutes both survive.
        Assert.True(resumed.StartedAt > resumed.FirstStartedAt);
        Assert.Equal(firstStart, resumed.FirstStartedAt);
        Assert.InRange(resumed.TotalRuntime, 29.9m, 30.1m);
    }

    [Fact]
    public async Task Held_Time_Is_Excluded_From_The_Finished_Runtime()
    {
        using var db = TestDb.Create();
        var svc = new WorkOrderService(db);
        var (m, i, o) = await SeedRefsAsync(db);
        var order = (await svc.CreateAsync(NewOrder("WO-1", m, i, o))).Value!;
        await svc.StartAsync(order.WorkOrderId);
        await BackdateSegmentAsync(db, order.WorkOrderId, 20);
        await svc.HoldAsync(order.WorkOrderId);

        // However long the order now sits on hold, none of it counts.
        await svc.ResumeAsync(order.WorkOrderId);
        await BackdateSegmentAsync(db, order.WorkOrderId, 10);
        Assert.True((await svc.FinishAsync(order.WorkOrderId)).Success);

        var finished = await db.WorkOrders.FirstAsync(w => w.WorkOrderId == order.WorkOrderId);
        Assert.InRange(finished.TotalRuntime, 29.9m, 30.1m);
        // For a finished order the live figure equals the banked total exactly.
        Assert.InRange(finished.ActiveDuration!.Value.TotalMinutes, 29.9, 30.1);
    }

    [Fact]
    public async Task Active_Duration_Is_Null_Before_The_Order_Ever_Starts()
    {
        using var db = TestDb.Create();
        var svc = new WorkOrderService(db);
        var (m, i, o) = await SeedRefsAsync(db);
        var order = (await svc.CreateAsync(NewOrder("WO-1", m, i, o))).Value!;

        Assert.Null((await db.WorkOrders.FirstAsync(w => w.WorkOrderId == order.WorkOrderId)).ActiveDuration);
    }

    [Fact]
    public async Task Holding_Frees_The_Machine_For_Another_Order()
    {
        using var db = TestDb.Create();
        var svc = new WorkOrderService(db);
        var (m, i, o) = await SeedRefsAsync(db);
        var first = (await svc.CreateAsync(NewOrder("WO-1", m, i, o))).Value!;
        var second = (await svc.CreateAsync(NewOrder("WO-2", m, i, o))).Value!;
        await svc.StartAsync(first.WorkOrderId);

        // Occupied while in progress...
        Assert.False((await svc.StartAsync(second.WorkOrderId)).Success);

        // ...released by holding.
        await svc.HoldAsync(first.WorkOrderId);
        Assert.True((await svc.StartAsync(second.WorkOrderId)).Success);
    }

    [Fact]
    public async Task A_Held_Order_Waits_For_Its_Own_Machine_To_Be_Free()
    {
        using var db = TestDb.Create();
        var svc = new WorkOrderService(db);
        var (m, i, o) = await SeedRefsAsync(db);
        var first = (await svc.CreateAsync(NewOrder("WO-1", m, i, o))).Value!;
        var second = (await svc.CreateAsync(NewOrder("WO-2", m, i, o))).Value!;
        await svc.StartAsync(first.WorkOrderId);
        await svc.HoldAsync(first.WorkOrderId);
        await svc.StartAsync(second.WorkOrderId);

        // The held order is pinned to its original machine, which is now busy.
        var blocked = await svc.ResumeAsync(first.WorkOrderId);
        Assert.False(blocked.Success);
        Assert.Contains("WO-2", blocked.Error);

        await svc.FinishAsync(second.WorkOrderId);
        Assert.True((await svc.ResumeAsync(first.WorkOrderId)).Success);
    }
}
