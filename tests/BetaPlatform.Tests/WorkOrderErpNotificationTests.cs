using BetaPlatform.Data;
using BetaPlatform.Data.Entities;
using BetaPlatform.Services;
using BetaPlatform.Services.Erp;
using Xunit;

namespace BetaPlatform.Tests;

/// <summary>
/// The hooks that turn a shop-floor state change into an ERP notification: which event fires when,
/// what it carries, and the guarantee that a state change the ERP did not accept is never stored.
/// </summary>
public class WorkOrderErpNotificationTests
{
    private enum MoEvent { Started, Held, Finished }

    /// <summary>Records the calls instead of making them; optionally fails every one.</summary>
    private sealed class RecordingErpClient : IErpClient
    {
        private readonly bool _fail;
        public RecordingErpClient(bool fail = false) => _fail = fail;

        public List<(MoEvent Event, int MoId, string MoReference, decimal ProducedQty)> Calls { get; } = new();

        public bool IsMock => true;

        public Task<ErpCallResult> NotifyStartedAsync(WorkOrder order, CancellationToken cancellationToken = default) =>
            Record(MoEvent.Started, order, 0m);

        public Task<ErpCallResult> NotifyHeldAsync(WorkOrder order, CancellationToken cancellationToken = default) =>
            Record(MoEvent.Held, order, 0m);

        public Task<ErpCallResult> NotifyFinishedAsync(WorkOrder order, decimal actualProducedQty, IReadOnlyList<MoConsumedComponent> consumedComponents, CancellationToken cancellationToken = default)
        {
            LastComponents = consumedComponents;
            return Record(MoEvent.Finished, order, actualProducedQty);
        }

        public IReadOnlyList<MoConsumedComponent>? LastComponents { get; private set; }

        private Task<ErpCallResult> Record(MoEvent e, WorkOrder order, decimal qty)
        {
            Calls.Add((e, order.WorkOrderId, order.WorkOrderNumber, qty));
            return Task.FromResult(_fail
                ? ErpCallResult.Failed("the ERP is down")
                : ErpCallResult.MockedOk());
        }
    }

    /// <summary>Answers every notification with one canned result.</summary>
    private sealed class StubErpClient : IErpClient
    {
        private readonly ErpCallResult _result;
        public StubErpClient(ErpCallResult result) => _result = result;

        public bool IsMock => true;

        public Task<ErpCallResult> NotifyStartedAsync(WorkOrder order, CancellationToken cancellationToken = default) =>
            Task.FromResult(_result);

        public Task<ErpCallResult> NotifyHeldAsync(WorkOrder order, CancellationToken cancellationToken = default) =>
            Task.FromResult(_result);

        public Task<ErpCallResult> NotifyFinishedAsync(WorkOrder order, decimal actualProducedQty, IReadOnlyList<MoConsumedComponent> consumedComponents, CancellationToken cancellationToken = default) =>
            Task.FromResult(_result);
    }

    private static async Task<WorkOrder> SeedReadyOrderAsync(ApplicationDbContext db, string number = "MO/00123")
    {
        var machine = new Machine { MachineCode = "M-1", MachineName = "Machine 1", MachineTypeId = 1, IsActive = true };
        var input = new Product { ProductCode = "IN-1", ProductName = "Input", Unit = "kg" };
        var output = new Product { ProductCode = "OUT-1", ProductName = "Output", Unit = "kg" };
        db.Machines.Add(machine);
        db.Products.AddRange(input, output);
        await db.SaveChangesAsync();

        var order = new WorkOrder
        {
            WorkOrderNumber = number,
            MachineId = machine.MachineId,
            InputProductId = input.ProductId,
            OutputProductId = output.ProductId,
            PlannedStartTime = new DateTime(2026, 7, 7, 8, 0, 0),
            QtyToManufacture = 100,
            Status = WorkOrderStatus.Ready
        };
        db.WorkOrders.Add(order);
        await db.SaveChangesAsync();
        return order;
    }

    [Fact]
    public async Task Starting_An_Order_Notifies_The_Erp_With_Its_Id_And_Number()
    {
        using var db = TestDb.Create();
        var order = await SeedReadyOrderAsync(db);
        var erp = new RecordingErpClient();

        var result = await new WorkOrderService(db, erp).StartAsync(order.WorkOrderId);

        Assert.True(result.Success);
        var call = Assert.Single(erp.Calls);
        Assert.Equal(MoEvent.Started, call.Event);
        Assert.Equal(order.WorkOrderId, call.MoId);
        Assert.Equal("MO/00123", call.MoReference);
    }

    [Fact]
    public async Task Holding_An_Order_Sends_The_Hold_Event()
    {
        using var db = TestDb.Create();
        var order = await SeedReadyOrderAsync(db);
        var erp = new RecordingErpClient();
        var svc = new WorkOrderService(db, erp);

        await svc.StartAsync(order.WorkOrderId);
        await svc.HoldAsync(order.WorkOrderId);

        Assert.Equal(new[] { MoEvent.Started, MoEvent.Held }, erp.Calls.Select(c => c.Event));
    }

    [Fact]
    public async Task Resuming_Sends_Start_Again_Because_The_Erp_Has_No_Notion_Of_Resume()
    {
        using var db = TestDb.Create();
        var order = await SeedReadyOrderAsync(db);
        var erp = new RecordingErpClient();
        var svc = new WorkOrderService(db, erp);

        await svc.StartAsync(order.WorkOrderId);
        await svc.HoldAsync(order.WorkOrderId);
        await svc.ResumeAsync(order.WorkOrderId);

        Assert.Equal(
            new[] { MoEvent.Started, MoEvent.Held, MoEvent.Started },
            erp.Calls.Select(c => c.Event));
    }

    [Fact]
    public async Task Finishing_Sends_The_Latest_Telemetry_Weight_As_The_Produced_Quantity()
    {
        using var db = TestDb.Create();
        var order = await SeedReadyOrderAsync(db);

        // Two readings for this order — the finish event must carry the latest, as the details
        // screen shows it, not the first or the sum.
        db.OeeData.AddRange(
            new OeeData { OrderId = order.WorkOrderId, TotalWeight = 40m, TotalCount = 4, Timestamp = new DateTime(2026, 7, 7, 9, 0, 0) },
            new OeeData { OrderId = order.WorkOrderId, TotalWeight = 98.5m, TotalCount = 9, Timestamp = new DateTime(2026, 7, 7, 10, 0, 0) });
        await db.SaveChangesAsync();

        var erp = new RecordingErpClient();
        var svc = new WorkOrderService(db, erp);
        await svc.StartAsync(order.WorkOrderId);
        await svc.FinishAsync(order.WorkOrderId);

        var finish = Assert.Single(erp.Calls, c => c.Event == MoEvent.Finished);
        Assert.Equal(98.5m, finish.ProducedQty);
    }

    [Fact]
    public async Task An_Order_With_No_Telemetry_Reports_Zero_Produced()
    {
        using var db = TestDb.Create();
        var order = await SeedReadyOrderAsync(db);
        var erp = new RecordingErpClient();
        var svc = new WorkOrderService(db, erp);

        await svc.StartAsync(order.WorkOrderId);
        await svc.FinishAsync(order.WorkOrderId);

        // Zero is the truth here, not a missing value.
        Assert.Equal(0m, Assert.Single(erp.Calls, c => c.Event == MoEvent.Finished).ProducedQty);
    }

    [Fact]
    public async Task A_Failing_Erp_Refuses_The_Transition_And_Stores_Nothing()
    {
        using var db = TestDb.Create();
        var order = await SeedReadyOrderAsync(db);
        var erp = new RecordingErpClient(fail: true);
        var svc = new WorkOrderService(db, erp);

        var started = await svc.StartAsync(order.WorkOrderId);

        Assert.False(started.Success);
        Assert.Equal("The ERP could not be called, so the request was not completed.", started.Error);
        // The ERP's own words stay in the log; the operator is not shown them.
        Assert.DoesNotContain("the ERP is down", started.Error);
        // The order never moved: still Ready, still unstarted.
        var stored = await svc.GetByIdAsync(order.WorkOrderId);
        Assert.Equal(WorkOrderStatus.Ready, stored!.Status);
        Assert.Null(stored.StartedAt);
        Assert.Null(stored.FirstStartedAt);
    }

    [Fact]
    public async Task A_Failing_Erp_Leaves_A_Running_Order_Running()
    {
        using var db = TestDb.Create();
        var order = await SeedReadyOrderAsync(db);
        var erp = new RecordingErpClient();
        var svc = new WorkOrderService(db, erp);
        await svc.StartAsync(order.WorkOrderId);

        // The ERP goes down between the start and the hold.
        var broken = new WorkOrderService(db, new RecordingErpClient(fail: true));
        var held = await broken.HoldAsync(order.WorkOrderId);

        Assert.False(held.Success);
        var stored = await svc.GetByIdAsync(order.WorkOrderId);
        Assert.Equal(WorkOrderStatus.InProgress, stored!.Status);
        Assert.Equal(0m, stored.TotalRuntime);
    }

    [Fact]
    public async Task Every_Way_Of_Failing_Gives_The_Operator_The_Same_Message()
    {
        // A refused call, a missing token and an unreachable host differ only in the log.
        ErpCallResult[] failures =
        [
            ErpCallResult.Failed("Unauthorized", 401),
            ErpCallResult.Failed("No such host is known. (erp.example.com:443)"),
            ErpCallResult.NotCredentialed()
        ];

        foreach (var failure in failures)
        {
            using var db = TestDb.Create();
            var order = await SeedReadyOrderAsync(db);

            var result = await new WorkOrderService(db, new StubErpClient(failure)).StartAsync(order.WorkOrderId);

            Assert.False(result.Success);
            Assert.Equal("The ERP could not be called, so the request was not completed.", result.Error);
            Assert.Equal(WorkOrderStatus.Ready, (await new WorkOrderService(db).GetByIdAsync(order.WorkOrderId))!.Status);
        }
    }

    [Fact]
    public async Task A_Mocked_Call_Never_Blocks_Anything()
    {
        using var db = TestDb.Create();
        var order = await SeedReadyOrderAsync(db);
        // What a site with no Erp:BaseUrl gets: the payload is logged, the answer is success.
        var erp = new StubErpClient(ErpCallResult.MockedOk());

        Assert.True((await new WorkOrderService(db, erp).StartAsync(order.WorkOrderId)).Success);
    }

    [Fact]
    public async Task A_Refused_Transition_Notifies_Nothing()
    {
        using var db = TestDb.Create();
        var order = await SeedReadyOrderAsync(db);
        var erp = new RecordingErpClient();

        // Ready cannot go straight to Finished. The ERP must not hear about a state change that
        // never happened.
        var result = await new WorkOrderService(db, erp).FinishAsync(order.WorkOrderId);

        Assert.False(result.Success);
        Assert.Empty(erp.Calls);
    }

    [Fact]
    public async Task The_Service_Still_Works_Without_An_Erp_Client_At_All()
    {
        using var db = TestDb.Create();
        var order = await SeedReadyOrderAsync(db);

        var result = await new WorkOrderService(db).StartAsync(order.WorkOrderId);

        Assert.True(result.Success);
    }

    [Fact]
    public async Task Finishing_Sends_Input_Weights_Totalled_Per_Product_Code()
    {
        using var db = TestDb.Create();
        var order = await SeedReadyOrderAsync(db, "WH/MO/00048");
        var a = new Product { ProductCode = "M10030", ProductName = "A", Unit = "kg" };
        var b = new Product { ProductCode = "M10040", ProductName = "B", Unit = "kg" };
        db.Products.AddRange(a, b);
        await db.SaveChangesAsync();
        db.WorkOrderInputProducts.AddRange(
            new WorkOrderInputProduct { WorkOrderId = order.WorkOrderId, ProductId = a.ProductId, Position = 1 },
            new WorkOrderInputProduct { WorkOrderId = order.WorkOrderId, ProductId = b.ProductId, Position = 2 });
        await db.SaveChangesAsync();

        var erp = new RecordingErpClient();
        var svc = new WorkOrderService(db, erp);
        await svc.StartAsync(order.WorkOrderId);
        Assert.True((await svc.AddInputAsync(order.WorkOrderId, 50m, a.ProductId)).Success);
        Assert.True((await svc.AddInputAsync(order.WorkOrderId, 5.5m, a.ProductId)).Success);
        Assert.True((await svc.AddInputAsync(order.WorkOrderId, 55.5m, b.ProductId)).Success);
        // An input recorded before inputs named a product: left out, not guessed.
        db.WorkOrderInputs.Add(new WorkOrderInput { WorkOrderId = order.WorkOrderId, Weight = 9m });
        await db.SaveChangesAsync();

        Assert.True((await svc.FinishAsync(order.WorkOrderId)).Success);

        // No telemetry: the produced quantity falls back to the summed input weights (stand-in).
        Assert.Equal(111m, erp.Calls.Last().ProducedQty);
        Assert.Collection(erp.LastComponents!,
            c => { Assert.Equal("M10030", c.ProductId); Assert.Equal(55.5m, c.ActualConsumedQty); },
            c => { Assert.Equal("M10040", c.ProductId); Assert.Equal(55.5m, c.ActualConsumedQty); });
    }
}
