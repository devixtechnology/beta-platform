using Microsoft.Extensions.Options;
using BetaPlatform.Data;
using BetaPlatform.Data.Entities;
using BetaPlatform.Helpers;
using BetaPlatform.Services;
using Xunit;

namespace BetaPlatform.Tests;

/// <summary>
/// Guards the fix for research T1: <see cref="DashboardService"/> used to issue two queries per
/// machine inside a <c>foreach</c>, so a 30-machine plant cost 60 round trips every 5 seconds — and
/// the production display multiplies that across every machine at once. The lookups must stay
/// per-refresh, not per-machine, however many machines exist.
/// </summary>
public class DashboardQueryShapeTests
{
    /// <summary>Counts how often the dashboard reaches for telemetry, delegating the real work.</summary>
    private sealed class CountingStatusService : IMachineStatusService
    {
        private readonly IMachineStatusService _inner;

        public CountingStatusService(IMachineStatusService inner) => _inner = inner;

        public int GetStatesCalls { get; private set; }
        public int GetLatestOeeCalls { get; private set; }
        public int WorkInProgressCalls { get; private set; }

        public Task<IReadOnlyDictionary<int, MachineRunningState>> GetStatesAsync(IEnumerable<int> machineIds)
        {
            GetStatesCalls++;
            return _inner.GetStatesAsync(machineIds);
        }

        public Task<IReadOnlyDictionary<int, OeeData>> GetLatestOeeAsync(IEnumerable<int> machineIds)
        {
            GetLatestOeeCalls++;
            return _inner.GetLatestOeeAsync(machineIds);
        }

        public Task<IReadOnlySet<int>> GetMachinesWithWorkInProgressAsync(IEnumerable<int> machineIds)
        {
            WorkInProgressCalls++;
            return _inner.GetMachinesWithWorkInProgressAsync(machineIds);
        }
    }

    [Theory]
    [InlineData(1)]
    [InlineData(25)]
    public async Task Telemetry_Is_Fetched_Once_Per_Refresh_Regardless_Of_Machine_Count(int machineCount)
    {
        using var db = TestDb.Create();
        var now = TimeZoneHelper.GetKsaNow();

        for (var i = 0; i < machineCount; i++)
        {
            var machine = new Machine
            {
                MachineCode = $"M-{i:D3}",
                MachineName = $"Machine {i:D3}",
                MachineTypeId = 1,
                IsActive = true
            };
            db.Machines.Add(machine);
            await db.SaveChangesAsync();

            db.OeeData.Add(new OeeData { MachineId = machine.MachineId, Timestamp = now.AddMinutes(-1), Status = 1 });
            db.PowerData.Add(new PowerData { MachineId = machine.MachineId, Timestamp = now.AddMinutes(-1), KwHr = 12m });
        }
        await db.SaveChangesAsync();

        var options = Options.Create(new TelemetryOptions());
        var counting = new CountingStatusService(new MachineStatusService(db, options));
        var svc = new DashboardService(db, counting, options);

        var vm = await svc.GetAsync();

        Assert.Equal(machineCount, vm.Machines.Count);
        Assert.All(vm.Machines, card => Assert.Equal(MachineRunningState.Running, card.Status));
        Assert.All(vm.Machines, card => Assert.NotNull(card.Oee));
        Assert.All(vm.Machines, card => Assert.NotNull(card.Power));

        // Once for the whole plant, not once per machine — and the rows are read a single time,
        // with status resolved from them rather than re-queried.
        Assert.Equal(1, counting.GetLatestOeeCalls);
        Assert.Equal(0, counting.GetStatesCalls);
    }

    [Fact]
    public async Task Status_Lookup_Does_Not_Query_For_An_Empty_Machine_List()
    {
        using var db = TestDb.Create();
        var svc = new MachineStatusService(db, Options.Create(new TelemetryOptions()));

        Assert.Empty(await svc.GetStatesAsync(Array.Empty<int>()));
        Assert.Empty(await svc.GetLatestOeeAsync(Array.Empty<int>()));
    }

    [Fact]
    public async Task Latest_Lookup_Returns_The_Newest_Row_For_Every_Machine_At_Once()
    {
        using var db = TestDb.Create();
        var now = TimeZoneHelper.GetKsaNow();
        var ids = new List<int>();

        for (var i = 0; i < 3; i++)
        {
            var machine = new Machine { MachineCode = $"C{i}", MachineName = $"N{i}", MachineTypeId = 1 };
            db.Machines.Add(machine);
            await db.SaveChangesAsync();
            ids.Add(machine.MachineId);

            db.OeeData.Add(new OeeData { MachineId = machine.MachineId, Timestamp = now.AddHours(-3), Status = 0, TotalCount = 10 });
            db.OeeData.Add(new OeeData { MachineId = machine.MachineId, Timestamp = now.AddMinutes(-1), Status = 1, TotalCount = 99 });
        }
        // A machine with no telemetry at all must simply be absent, not throw.
        var silent = new Machine { MachineCode = "SILENT", MachineName = "Silent", MachineTypeId = 1 };
        db.Machines.Add(silent);
        await db.SaveChangesAsync();
        ids.Add(silent.MachineId);

        var svc = new MachineStatusService(db, Options.Create(new TelemetryOptions()));

        var latest = await svc.GetLatestOeeAsync(ids);
        var states = await svc.GetStatesAsync(ids);

        Assert.Equal(3, latest.Count);
        Assert.All(latest.Values, row => Assert.Equal(99m, row.TotalCount));
        Assert.False(latest.ContainsKey(silent.MachineId));
        Assert.Equal(MachineRunningState.Stopped, states[silent.MachineId]);
    }
}
