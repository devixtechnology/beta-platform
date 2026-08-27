using Microsoft.Extensions.Options;
using BetaPlatform.Data;
using BetaPlatform.Data.Entities;
using BetaPlatform.Helpers;
using BetaPlatform.Services;
using Xunit;

namespace BetaPlatform.Tests;

public class MachineServiceTests
{
    private static Machine NewMachine(string code = "M-01", string name = "Machine 1", int typeId = 1) =>
        new() { MachineCode = code, MachineName = name, MachineTypeId = typeId };

    private static MachineService NewService(ApplicationDbContext db, int staleAfterMinutes = 5)
    {
        var options = Options.Create(new TelemetryOptions { StaleAfterMinutes = staleAfterMinutes });
        return new MachineService(db, new MachineStatusService(db, options), options);
    }

    [Fact]
    public async Task Create_Succeeds_With_Seeded_Type()
    {
        using var db = TestDb.Create();
        var svc = NewService(db);

        var result = await svc.CreateAsync(NewMachine());

        Assert.True(result.Success);
        Assert.Single(await svc.GetAllAsync());
    }

    [Fact]
    public async Task Create_Rejects_Duplicate_Code()
    {
        using var db = TestDb.Create();
        var svc = NewService(db);
        await svc.CreateAsync(NewMachine(code: "DUP", name: "A"));

        var result = await svc.CreateAsync(NewMachine(code: "DUP", name: "B"));

        Assert.False(result.Success);
        Assert.Contains("already exists", result.Error);
    }

    [Fact]
    public async Task Create_Rejects_Unknown_Type()
    {
        using var db = TestDb.Create();
        var svc = NewService(db);

        var result = await svc.CreateAsync(NewMachine(typeId: 999));

        Assert.False(result.Success);
    }

    [Fact]
    public async Task Update_Leaves_The_Inert_IsRunning_Flag_Untouched()
    {
        using var db = TestDb.Create();
        var svc = NewService(db);
        var created = (await svc.CreateAsync(NewMachine())).Value!;

        // 004: IsRunning is no longer bound by the Edit form and no longer written by the service —
        // running state comes from telemetry alone (contracts/machine-status.md).
        var edit = new Machine
        {
            MachineId = created.MachineId,
            MachineName = "Renamed",
            MachineCode = created.MachineCode,
            MachineTypeId = created.MachineTypeId,
            IsActive = true,
            IsRunning = true
        };
        var result = await svc.UpdateAsync(edit);

        Assert.True(result.Success);
        var reloaded = await svc.GetByIdAsync(created.MachineId);
        Assert.Equal("Renamed", reloaded!.MachineName);
        Assert.False(reloaded.IsRunning);
    }

    [Fact]
    public async Task Deactivate_Keeps_Record_But_Marks_Inactive()
    {
        using var db = TestDb.Create();
        var svc = NewService(db);
        var created = await svc.CreateAsync(NewMachine());

        var result = await svc.DeactivateAsync(created.Value!.MachineId);

        Assert.True(result.Success);
        var reloaded = await svc.GetByIdAsync(created.Value!.MachineId);
        Assert.NotNull(reloaded);
        Assert.False(reloaded!.IsActive);
        Assert.Empty(await svc.GetActiveAsync());
    }

    [Fact]
    public async Task GetActiveTypes_Returns_Only_Seeded_Phase1_Types()
    {
        using var db = TestDb.Create();
        var svc = NewService(db);

        var types = await svc.GetActiveTypesAsync();

        Assert.Equal(2, types.Count);
        Assert.Contains(types, t => t.Name == "Forming Machine");
        Assert.Contains(types, t => t.Name == "Flat Washer Line");
    }

    // ---- 004 US1: one status rule, same answer on every screen (FR-002) ----

    private static async Task<int> SeedMachineWithReadingAsync(
        ApplicationDbContext db, byte status, int ageMinutes)
    {
        var machine = new Machine { MachineCode = "M-S", MachineName = "M-S", MachineTypeId = 1, IsActive = true };
        db.Machines.Add(machine);
        await db.SaveChangesAsync();

        db.OeeData.Add(new OeeData
        {
            MachineId = machine.MachineId,
            Timestamp = TimeZoneHelper.GetKsaNow().AddMinutes(-ageMinutes),
            Status = status
        });
        await db.SaveChangesAsync();
        return machine.MachineId;
    }

    [Theory]
    [InlineData((byte)1, 1, MachineRunningState.Running)]
    [InlineData((byte)0, 1, MachineRunningState.Stopped)]
    [InlineData((byte)1, 60, MachineRunningState.Stopped)]
    public async Task List_And_Details_Resolve_The_Same_State(byte status, int ageMinutes, MachineRunningState expected)
    {
        using var db = TestDb.Create();
        var machineId = await SeedMachineWithReadingAsync(db, status, ageMinutes);
        var svc = NewService(db);

        var listed = Assert.Single(await svc.GetAllWithStatusAsync());
        var details = await svc.GetDetailsAsync(machineId);

        Assert.Equal(expected, listed.RunningState);
        Assert.Equal(expected, details!.RunningState);
    }

    [Fact]
    public async Task Machine_Without_Telemetry_Is_Stopped_On_Both_Screens()
    {
        using var db = TestDb.Create();
        var svc = NewService(db);
        var created = (await svc.CreateAsync(NewMachine())).Value!;

        var listed = Assert.Single(await svc.GetAllWithStatusAsync());
        var details = await svc.GetDetailsAsync(created.MachineId);

        Assert.Equal(MachineRunningState.Stopped, listed.RunningState);
        Assert.Equal(MachineRunningState.Stopped, details!.RunningState);
    }

    [Fact]
    public async Task Running_State_Ignores_The_Administrative_IsRunning_Flag()
    {
        using var db = TestDb.Create();
        var machineId = await SeedMachineWithReadingAsync(db, status: 0, ageMinutes: 1);
        var machine = await db.Machines.FindAsync(machineId);
        machine!.IsRunning = true; // stale hand-set flag — the defect in client comment 2
        await db.SaveChangesAsync();
        var svc = NewService(db);

        var listed = Assert.Single(await svc.GetAllWithStatusAsync());

        Assert.Equal(MachineRunningState.Stopped, listed.RunningState);
    }

    // ---- An in-progress order overrides telemetry, on every screen (decision 2026-08-27) ----

    [Fact]
    public async Task In_Progress_Order_Is_Running_On_List_Details_And_Live()
    {
        using var db = TestDb.Create();
        // A stopped reading from 49 days ago — exactly the shape of the live database.
        var machineId = await SeedMachineWithReadingAsync(db, status: 0, ageMinutes: 60 * 24 * 49);
        await SeedWorkOrderAsync(db, machineId, "WO-OPEN", WorkOrderStatus.InProgress,
            TimeZoneHelper.GetKsaNow().AddDays(-49));
        var svc = NewService(db);

        var listed = Assert.Single(await svc.GetAllWithStatusAsync());
        var details = await svc.GetDetailsAsync(machineId);
        var live = await svc.GetLiveAsync(machineId);

        Assert.Equal(MachineRunningState.Running, listed.RunningState);
        Assert.Equal(MachineRunningState.Running, details!.RunningState);
        Assert.Equal(MachineRunningState.Running, live!.Status);
    }

    [Fact]
    public async Task A_Ready_Or_Finished_Order_Does_Not_Make_The_Machine_Running()
    {
        using var db = TestDb.Create();
        var machineId = await SeedMachineWithReadingAsync(db, status: 0, ageMinutes: 1);
        await SeedWorkOrderAsync(db, machineId, "WO-READY-ONLY", WorkOrderStatus.Ready, null);
        await SeedWorkOrderAsync(db, machineId, "WO-DONE-ONLY", WorkOrderStatus.Finished,
            TimeZoneHelper.GetKsaNow().AddHours(-3));
        var svc = NewService(db);

        var listed = Assert.Single(await svc.GetAllWithStatusAsync());
        var details = await svc.GetDetailsAsync(machineId);

        Assert.Equal(MachineRunningState.Stopped, listed.RunningState);
        Assert.Equal(MachineRunningState.Stopped, details!.RunningState);
    }

    // ---- 004 US3: uptime/downtime accounted by duration, with a "no data" bucket (FR-027) ----

    /// <summary>Seeds a machine and a run of readings at the given offsets (minutes before now).</summary>
    private static async Task<int> SeedReadingsAsync(
        ApplicationDbContext db, params (int MinutesAgo, byte Status)[] readings)
    {
        var machine = new Machine { MachineCode = "M-U", MachineName = "M-U", MachineTypeId = 1, IsActive = true };
        db.Machines.Add(machine);
        await db.SaveChangesAsync();

        var now = TimeZoneHelper.GetKsaNow();
        foreach (var (minutesAgo, status) in readings)
        {
            db.OeeData.Add(new OeeData
            {
                MachineId = machine.MachineId,
                Timestamp = now.AddMinutes(-minutesAgo),
                Status = status
            });
        }
        await db.SaveChangesAsync();
        return machine.MachineId;
    }

    [Fact]
    public async Task Uptime_Downtime_And_NoData_Sum_To_The_Whole_Window()
    {
        using var db = TestDb.Create();
        // A dense run of one-minute readings, then nothing for the rest of the day.
        var readings = Enumerable.Range(0, 60).Select(i => (MinutesAgo: i, Status: (byte)1)).ToArray();
        var machineId = await SeedReadingsAsync(db, readings);
        var svc = NewService(db);

        var vm = await svc.GetDetailsAsync(machineId);

        var total = vm!.Uptime24h + vm.Downtime24h + vm.NoDataTime24h;
        Assert.Equal(24, Math.Round(total.TotalHours, 3));
        Assert.Equal(vm.WindowEnd - vm.WindowStart, total);
    }

    [Fact]
    public async Task A_Telemetry_Gap_Lands_In_NoData_Not_Uptime()
    {
        using var db = TestDb.Create();
        // Two running readings a minute apart, then a six-hour silence.
        var machineId = await SeedReadingsAsync(db, (362, 1), (361, 1), (360, 1));
        var svc = NewService(db, staleAfterMinutes: 5);

        var vm = await svc.GetDetailsAsync(machineId);

        // Each reading contributes at most the staleness threshold, so a 6 h silence cannot be
        // counted as 6 h of running.
        Assert.True(vm!.Uptime24h < TimeSpan.FromMinutes(10),
            $"uptime was {vm.Uptime24h}, which means the gap was counted as running");
        Assert.True(vm.NoDataTime24h > TimeSpan.FromHours(23),
            $"no-data was {vm.NoDataTime24h}, which means the gap was not accounted");
        Assert.Equal(TimeSpan.Zero, vm.Downtime24h);
    }

    [Fact]
    public async Task Mixed_Running_And_Stopped_Readings_Split_By_Duration()
    {
        using var db = TestDb.Create();
        // 10 minutes running, then 10 minutes stopped, one reading per minute.
        var running = Enumerable.Range(0, 10).Select(i => (MinutesAgo: 20 - i, Status: (byte)1));
        var stopped = Enumerable.Range(0, 10).Select(i => (MinutesAgo: 10 - i, Status: (byte)0));
        var machineId = await SeedReadingsAsync(db, running.Concat(stopped).ToArray());
        var svc = NewService(db);

        var vm = await svc.GetDetailsAsync(machineId);

        // The 11th reading (the first stopped one) closes the running run, so uptime is ~10 min.
        Assert.InRange(vm!.Uptime24h.TotalMinutes, 9, 11);
        Assert.InRange(vm.Downtime24h.TotalMinutes, 9, 15);
    }

    [Fact]
    public async Task No_Telemetry_At_All_Yields_Three_Zeros()
    {
        using var db = TestDb.Create();
        var svc = NewService(db);
        var created = (await svc.CreateAsync(NewMachine())).Value!;

        var vm = await svc.GetDetailsAsync(created.MachineId);

        Assert.Equal(TimeSpan.Zero, vm!.Uptime24h);
        Assert.Equal(TimeSpan.Zero, vm.Downtime24h);
        Assert.Equal(TimeSpan.Zero, vm.NoDataTime24h);
    }

    // ---- 004 US4: the work order in progress on this machine (FR-021, FR-024) ----

    private static async Task<int> SeedWorkOrderAsync(
        ApplicationDbContext db, int machineId, string number, WorkOrderStatus status, DateTime? startedAt)
    {
        var order = new WorkOrder
        {
            WorkOrderNumber = number,
            InputProductId = 1,
            OutputProductId = 2,
            MachineId = machineId,
            PlannedStartTime = TimeZoneHelper.GetKsaNow().AddHours(-8),
            QtyToManufacture = 5000m,
            Status = status,
            StartedAt = startedAt
        };
        db.WorkOrders.Add(order);
        await db.SaveChangesAsync();
        return order.WorkOrderId;
    }

    [Fact]
    public async Task Current_Work_Order_Is_The_Latest_Started_In_Progress_Order()
    {
        using var db = TestDb.Create();
        var svc = NewService(db);
        var machine = (await svc.CreateAsync(NewMachine())).Value!;
        var now = TimeZoneHelper.GetKsaNow();

        await SeedWorkOrderAsync(db, machine.MachineId, "WO-OLD", WorkOrderStatus.InProgress, now.AddHours(-9));
        var newest = await SeedWorkOrderAsync(db, machine.MachineId, "WO-NEW", WorkOrderStatus.InProgress, now.AddHours(-2));
        await SeedWorkOrderAsync(db, machine.MachineId, "WO-DONE", WorkOrderStatus.Finished, now.AddHours(-1));

        var vm = await svc.GetDetailsAsync(machine.MachineId);

        Assert.NotNull(vm!.CurrentWorkOrder);
        Assert.Equal(newest, vm.CurrentWorkOrder!.WorkOrderId);
        Assert.Equal("WO-NEW", vm.CurrentWorkOrder.WorkOrderNumber);
        Assert.True(vm.HasOtherWorkOrdersInProgress);
    }

    [Fact]
    public async Task Current_Work_Order_Ties_Break_On_The_Highest_Id()
    {
        using var db = TestDb.Create();
        var svc = NewService(db);
        var machine = (await svc.CreateAsync(NewMachine())).Value!;
        var startedAt = TimeZoneHelper.GetKsaNow().AddHours(-3);

        await SeedWorkOrderAsync(db, machine.MachineId, "WO-A", WorkOrderStatus.InProgress, startedAt);
        var later = await SeedWorkOrderAsync(db, machine.MachineId, "WO-B", WorkOrderStatus.InProgress, startedAt);

        var vm = await svc.GetDetailsAsync(machine.MachineId);

        Assert.Equal(later, vm!.CurrentWorkOrder!.WorkOrderId);
    }

    [Fact]
    public async Task No_In_Progress_Order_Leaves_The_Card_Empty()
    {
        using var db = TestDb.Create();
        var svc = NewService(db);
        var machine = (await svc.CreateAsync(NewMachine())).Value!;
        await SeedWorkOrderAsync(db, machine.MachineId, "WO-READY", WorkOrderStatus.Ready, null);

        var vm = await svc.GetDetailsAsync(machine.MachineId);

        Assert.Null(vm!.CurrentWorkOrder);
        Assert.False(vm.HasOtherWorkOrdersInProgress);
    }

    [Fact]
    public async Task Current_Work_Order_Totals_Its_Recorded_Input_Weight()
    {
        using var db = TestDb.Create();
        var svc = NewService(db);
        var machine = (await svc.CreateAsync(NewMachine())).Value!;
        var orderId = await SeedWorkOrderAsync(
            db, machine.MachineId, "WO-1", WorkOrderStatus.InProgress, TimeZoneHelper.GetKsaNow().AddHours(-4));
        db.WorkOrderInputs.AddRange(
            new WorkOrderInput { WorkOrderId = orderId, Weight = 400m },
            new WorkOrderInput { WorkOrderId = orderId, Weight = 350.5m });
        await db.SaveChangesAsync();

        var vm = await svc.GetDetailsAsync(machine.MachineId);

        Assert.Equal(750.5m, vm!.CurrentWorkOrder!.TotalInputWeight);
        Assert.InRange(vm.CurrentWorkOrder.ElapsedTime.TotalHours, 3.9, 4.1);
    }

    // ---- 004 US3: the live payload the details page polls (contracts/machine-live-data.md) ----

    [Fact]
    public async Task GetLive_Returns_Nulls_Rather_Than_Failing_When_Telemetry_Is_Absent()
    {
        using var db = TestDb.Create();
        var svc = NewService(db);
        var machine = (await svc.CreateAsync(NewMachine())).Value!;

        var live = await svc.GetLiveAsync(machine.MachineId);

        Assert.NotNull(live);
        Assert.Null(live!.Latest);
        Assert.Null(live.Power);
        Assert.Null(live.CurrentWorkOrder);
        Assert.Equal(MachineRunningState.Stopped, live.Status);
        Assert.Equal(0, live.Window.UptimeSeconds);
        Assert.Equal(0, live.Window.DowntimeSeconds);
        Assert.Equal(0, live.Window.NoDataSeconds);
    }

    [Fact]
    public async Task GetLive_Returns_Null_For_An_Unknown_Machine()
    {
        using var db = TestDb.Create();
        var svc = NewService(db);

        Assert.Null(await svc.GetLiveAsync(4242));
    }

    [Fact]
    public async Task GetLive_Reports_The_Same_State_As_The_Details_View_Model()
    {
        using var db = TestDb.Create();
        var machineId = await SeedMachineWithReadingAsync(db, status: 1, ageMinutes: 1);
        var svc = NewService(db);

        var details = await svc.GetDetailsAsync(machineId);
        var live = await svc.GetLiveAsync(machineId);

        Assert.Equal(details!.RunningState, live!.Status);
        Assert.Equal(MachineRunningState.Running, live.Status);
    }
}
