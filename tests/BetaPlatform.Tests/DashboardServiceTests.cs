using Microsoft.Extensions.Options;
using BetaPlatform.Data;
using BetaPlatform.Data.Entities;
using BetaPlatform.Helpers;
using BetaPlatform.Services;
using Xunit;

namespace BetaPlatform.Tests;

public class DashboardServiceTests
{
    private static async Task<int> SeedMachineAsync(ApplicationDbContext db, string code = "M-1")
    {
        var m = new Machine { MachineCode = code, MachineName = code, MachineTypeId = 1, IsActive = true };
        db.Machines.Add(m);
        await db.SaveChangesAsync();
        return m.MachineId;
    }

    private static DashboardService NewService(ApplicationDbContext db, int staleAfterMinutes = 5)
    {
        var options = Options.Create(new TelemetryOptions { StaleAfterMinutes = staleAfterMinutes });
        return new DashboardService(db, new MachineStatusService(db, options), options);
    }

    /// <summary>A reading the status rule will consider live — timestamps are compared against
    /// KSA-local now, the platform's basis everywhere else.</summary>
    private static DateTime Fresh(int minutesAgo = 1) => TimeZoneHelper.GetKsaNow().AddMinutes(-minutesAgo);

    [Fact]
    public async Task Machine_Without_Telemetry_Renders_Stopped()
    {
        using var db = TestDb.Create();
        await SeedMachineAsync(db);
        var svc = NewService(db);

        var vm = await svc.GetAsync();

        var card = Assert.Single(vm.Machines);
        Assert.False(card.HasTelemetry);
        Assert.Equal(MachineRunningState.Stopped, card.Status);
        Assert.Null(card.Oee);
    }

    [Fact]
    public async Task Oee_Value_Is_Product_Over_10000()
    {
        using var db = TestDb.Create();
        var machineId = await SeedMachineAsync(db);
        db.OeeData.Add(new OeeData
        {
            MachineId = machineId,
            Timestamp = Fresh(),
            Availability = 90m,
            Performance = 80m,
            Quality = 95m,
            TotalCount = 100m,
            TotalGoods = 95m,
            Status = 1
        });
        await db.SaveChangesAsync();
        var svc = NewService(db);

        var card = Assert.Single((await svc.GetAsync()).Machines);

        Assert.True(card.HasTelemetry);
        Assert.Equal(MachineRunningState.Running, card.Status);
        // (90 * 80 * 95) / 10000 = 68.4
        Assert.Equal(68.4m, card.Oee!.Value);
    }

    [Fact]
    public async Task Latest_Oee_Row_Wins()
    {
        using var db = TestDb.Create();
        var machineId = await SeedMachineAsync(db);
        db.OeeData.Add(new OeeData { MachineId = machineId, Timestamp = Fresh(120), Availability = 50m, Performance = 50m, Quality = 50m, Status = 0 });
        db.OeeData.Add(new OeeData { MachineId = machineId, Timestamp = Fresh(1), Availability = 100m, Performance = 100m, Quality = 100m, Status = 1 });
        await db.SaveChangesAsync();
        var svc = NewService(db);

        var card = Assert.Single((await svc.GetAsync()).Machines);

        Assert.Equal(100m, card.Oee!.Availability);
        Assert.Equal(MachineRunningState.Running, card.Status);
    }

    [Fact]
    public async Task Inactive_Machines_Are_Excluded()
    {
        using var db = TestDb.Create();
        var m = new Machine { MachineCode = "M-x", MachineName = "M-x", MachineTypeId = 1, IsActive = false };
        db.Machines.Add(m);
        await db.SaveChangesAsync();
        var svc = NewService(db);

        var vm = await svc.GetAsync();

        Assert.Empty(vm.Machines);
    }

    // ---- 004 US1: the card status is the shared rule, not an inline check (FR-001/FR-002) ----

    [Fact]
    public async Task Stale_Reading_Renders_Stopped_Not_Its_Last_Known_State()
    {
        using var db = TestDb.Create();
        var machineId = await SeedMachineAsync(db);
        db.OeeData.Add(new OeeData { MachineId = machineId, Timestamp = Fresh(90), Status = 1 });
        await db.SaveChangesAsync();
        var svc = NewService(db);

        var card = Assert.Single((await svc.GetAsync()).Machines);

        Assert.Equal(MachineRunningState.Stopped, card.Status);
        // The reading itself is still shown — only its stale "running" claim is withdrawn.
        Assert.True(card.HasTelemetry);
    }

    [Fact]
    public async Task Card_Status_Matches_MachineStatusRules_For_Every_Case()
    {
        using var db = TestDb.Create();
        var running = await SeedMachineAsync(db, "M-run");
        var stopped = await SeedMachineAsync(db, "M-stop");
        var stale = await SeedMachineAsync(db, "M-stale");
        await SeedMachineAsync(db, "M-none");
        db.OeeData.AddRange(
            new OeeData { MachineId = running, Timestamp = Fresh(), Status = 1 },
            new OeeData { MachineId = stopped, Timestamp = Fresh(), Status = 0 },
            new OeeData { MachineId = stale, Timestamp = Fresh(30), Status = 1 });
        await db.SaveChangesAsync();
        var svc = NewService(db);

        var cards = (await svc.GetAsync()).Machines.ToDictionary(c => c.MachineName);

        Assert.Equal(MachineRunningState.Running, cards["M-run"].Status);
        Assert.Equal(MachineRunningState.Stopped, cards["M-stop"].Status);
        Assert.Equal(MachineRunningState.Stopped, cards["M-stale"].Status);
        Assert.Equal(MachineRunningState.Stopped, cards["M-none"].Status);
    }

    [Fact]
    public async Task Running_Count_Counts_Only_Machines_The_Rule_Calls_Running()
    {
        using var db = TestDb.Create();
        var running = await SeedMachineAsync(db, "M-run");
        var stale = await SeedMachineAsync(db, "M-stale");
        db.OeeData.AddRange(
            new OeeData { MachineId = running, Timestamp = Fresh(), Status = 1 },
            new OeeData { MachineId = stale, Timestamp = Fresh(45), Status = 1 });
        await db.SaveChangesAsync();
        var svc = NewService(db);

        var vm = await svc.GetAsync();

        Assert.Equal(2, vm.Summary.TotalMachines);
        Assert.Equal(1, vm.Summary.RunningMachines);
    }

    // ---- An in-progress order overrides telemetry on the card (decision 2026-08-27) ----

    [Fact]
    public async Task In_Progress_Order_Renders_Running_Over_A_Stale_Reading()
    {
        using var db = TestDb.Create();
        var machineId = await SeedMachineAsync(db);
        db.OeeData.Add(new OeeData { MachineId = machineId, Timestamp = Fresh(60 * 24 * 49), Status = 0 });
        await db.SaveChangesAsync();
        await SeedInProgressOrderAsync(db, machineId, "WO-RUN", 120m);
        var svc = NewService(db);

        var vm = await svc.GetAsync();

        var card = Assert.Single(vm.Machines);
        Assert.Equal(MachineRunningState.Running, card.Status);
        Assert.Equal(1, vm.Summary.RunningMachines);
    }

    [Fact]
    public async Task In_Progress_Order_Renders_Running_With_No_Telemetry_At_All()
    {
        using var db = TestDb.Create();
        var machineId = await SeedMachineAsync(db);
        await SeedInProgressOrderAsync(db, machineId, "WO-SILENT");
        var svc = NewService(db);

        var card = Assert.Single((await svc.GetAsync()).Machines);

        Assert.Equal(MachineRunningState.Running, card.Status);
        Assert.False(card.HasTelemetry);
    }

    // ---- 004 US5: Input Weight replaces Good Units on the card (client comment 7) ----

    private static async Task<int> SeedInProgressOrderAsync(
        ApplicationDbContext db, int machineId, string number, params decimal[] inputWeights)
    {
        var order = new WorkOrder
        {
            WorkOrderNumber = number,
            InputProductId = 1,
            OutputProductId = 2,
            MachineId = machineId,
            PlannedStartTime = TimeZoneHelper.GetKsaNow().AddHours(-6),
            QtyToManufacture = 1000m,
            Status = WorkOrderStatus.InProgress,
            StartedAt = TimeZoneHelper.GetKsaNow().AddHours(-5)
        };
        db.WorkOrders.Add(order);
        await db.SaveChangesAsync();

        foreach (var weight in inputWeights)
            db.WorkOrderInputs.Add(new WorkOrderInput { WorkOrderId = order.WorkOrderId, Weight = weight });
        await db.SaveChangesAsync();
        return order.WorkOrderId;
    }

    [Fact]
    public async Task Input_Weight_Is_The_Sum_Of_The_In_Progress_Orders_Inputs()
    {
        using var db = TestDb.Create();
        var machineId = await SeedMachineAsync(db);
        await SeedInProgressOrderAsync(db, machineId, "WO-1", 400m, 350.5m, 100m);
        var svc = NewService(db);

        var card = Assert.Single((await svc.GetAsync()).Machines);

        Assert.Equal(850.5m, card.InputWeight);
    }

    [Fact]
    public async Task Input_Weight_Is_Zero_When_The_Order_Has_No_Inputs()
    {
        using var db = TestDb.Create();
        var machineId = await SeedMachineAsync(db);
        await SeedInProgressOrderAsync(db, machineId, "WO-EMPTY");
        var svc = NewService(db);

        var card = Assert.Single((await svc.GetAsync()).Machines);

        Assert.Equal(0m, card.InputWeight);
    }

    [Fact]
    public async Task Input_Weight_Is_Zero_When_The_Machine_Has_No_In_Progress_Order()
    {
        using var db = TestDb.Create();
        var machineId = await SeedMachineAsync(db);
        var finished = await SeedInProgressOrderAsync(db, machineId, "WO-DONE", 900m);
        var order = await db.WorkOrders.FindAsync(finished);
        order!.Status = WorkOrderStatus.Finished;
        await db.SaveChangesAsync();
        var svc = NewService(db);

        var card = Assert.Single((await svc.GetAsync()).Machines);

        Assert.Equal(0m, card.InputWeight);
    }

    [Fact]
    public async Task Input_Weight_Uses_The_Most_Recently_Started_Order_Per_Machine()
    {
        using var db = TestDb.Create();
        var machineId = await SeedMachineAsync(db);
        var older = await SeedInProgressOrderAsync(db, machineId, "WO-OLD", 111m);
        var olderOrder = await db.WorkOrders.FindAsync(older);
        olderOrder!.StartedAt = TimeZoneHelper.GetKsaNow().AddHours(-20);
        await db.SaveChangesAsync();
        await SeedInProgressOrderAsync(db, machineId, "WO-NEW", 222m, 3m);
        var svc = NewService(db);

        var card = Assert.Single((await svc.GetAsync()).Machines);

        Assert.Equal(225m, card.InputWeight);
    }

    [Fact]
    public async Task Input_Weight_Is_Per_Machine_Not_Shared()
    {
        using var db = TestDb.Create();
        var first = await SeedMachineAsync(db, "M-a");
        var second = await SeedMachineAsync(db, "M-b");
        await SeedInProgressOrderAsync(db, first, "WO-A", 500m);
        await SeedInProgressOrderAsync(db, second, "WO-B", 25m);
        var svc = NewService(db);

        var cards = (await svc.GetAsync()).Machines.ToDictionary(c => c.MachineName);

        Assert.Equal(500m, cards["M-a"].InputWeight);
        Assert.Equal(25m, cards["M-b"].InputWeight);
    }
}
