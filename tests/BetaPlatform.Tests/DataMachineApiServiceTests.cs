using BetaPlatform.Data;
using BetaPlatform.Data.Entities;
using BetaPlatform.Helpers;
using BetaPlatform.Services;
using BetaPlatform.Services.Api;
using BetaPlatform.ViewModels.Api;
using Microsoft.Extensions.Options;
using Xunit;

namespace BetaPlatform.Tests;

/// <summary>
/// The machine list: every machine, all of its detail, and — unlike the product operations — its id.
/// </summary>
public class DataMachineApiServiceTests
{
    private static DataMachineApiService Create(ApplicationDbContext db, int staleAfterMinutes = 10)
    {
        var telemetry = Options.Create(new TelemetryOptions { StaleAfterMinutes = staleAfterMinutes });
        var status = new MachineStatusService(db, telemetry);
        return new DataMachineApiService(new MachineService(db, status, telemetry), status);
    }

    private static async Task SeedAsync(ApplicationDbContext db)
    {
        // Machine type 1 is seeded by the model; give it a known shape to assert against.
        var type = await db.MachineTypes.FindAsync(1);
        if (type is not null)
        {
            type.Name = "تعبئة";
            type.NameEnglish = "Filling";
            type.ProductionLine = "Line A";
        }

        db.Machines.AddRange(
            new Machine { MachineCode = "M-01", MachineName = "Filler 1", MachineTypeId = 1, IsActive = true, IsRunning = true },
            new Machine { MachineCode = "M-02", MachineName = "Filler 2", MachineTypeId = 1, IsActive = true, IsRunning = false },
            new Machine { MachineCode = "M-99", MachineName = "Retired", MachineTypeId = 1, IsActive = false, IsRunning = false });
        await db.SaveChangesAsync();
    }

    [Fact]
    public async Task GetAll_Returns_Every_Machine_Including_Deactivated()
    {
        using var db = TestDb.Create();
        await SeedAsync(db);

        var result = await Create(db).GetAllAsync(activeOnly: false);

        Assert.Equal(ApiOutcome.Success, result.Outcome);
        Assert.Equal(3, result.Value!.Count);
        Assert.Contains(result.Value, m => m.MachineCode == "M-99" && !m.IsActive);
    }

    [Fact]
    public async Task GetAll_ActiveOnly_Excludes_Deactivated()
    {
        using var db = TestDb.Create();
        await SeedAsync(db);

        var result = await Create(db).GetAllAsync(activeOnly: true);

        Assert.Equal(2, result.Value!.Count);
        Assert.DoesNotContain(result.Value, m => m.MachineCode == "M-99");
    }

    [Fact]
    public async Task GetAll_Empty_Fleet_Is_Success_With_No_Rows()
    {
        using var db = TestDb.Create();

        var result = await Create(db).GetAllAsync(activeOnly: false);

        Assert.Equal(ApiOutcome.Success, result.Outcome);
        Assert.Empty(result.Value!);
    }

    /// <summary>
    /// The whole point of this endpoint: the caller gets the value it has to send back as
    /// <c>machineId</c> when raising a work order.
    /// </summary>
    [Fact]
    public async Task GetAll_Carries_The_Machine_Id()
    {
        using var db = TestDb.Create();
        await SeedAsync(db);

        var result = await Create(db).GetAllAsync(activeOnly: false);

        var stored = db.Machines.Single(m => m.MachineCode == "M-01");
        var returned = result.Value!.Single(m => m.MachineCode == "M-01");

        Assert.Equal(stored.MachineId, returned.MachineId);
        Assert.NotEqual(0, returned.MachineId);
    }

    [Fact]
    public async Task GetAll_Carries_The_Type_And_Production_Line()
    {
        using var db = TestDb.Create();
        await SeedAsync(db);

        var machine = (await Create(db).GetAllAsync(activeOnly: false)).Value!
            .Single(m => m.MachineCode == "M-01");

        Assert.Equal(1, machine.MachineTypeId);
        Assert.Equal("تعبئة", machine.MachineTypeName);
        Assert.Equal("Filling", machine.MachineTypeNameEnglish);
        Assert.Equal("Line A", machine.ProductionLine);
    }

    /// <summary>
    /// Silence means not producing: a machine that has never reported is <c>Stopped</c>, not
    /// <c>Unknown</c> (MachineStatusRules — the one rule every screen uses). <c>Unknown</c> survives
    /// only for a reading whose status byte the platform cannot interpret.
    /// </summary>
    [Fact]
    public async Task GetAll_Reports_Stopped_When_There_Is_No_Telemetry()
    {
        using var db = TestDb.Create();
        await SeedAsync(db);

        var result = await Create(db).GetAllAsync(activeOnly: false);

        Assert.All(result.Value!, m => Assert.Equal("Stopped", m.RunningState));
    }

    /// <summary>A fresh reading of status 1 is reported as Running.</summary>
    [Fact]
    public async Task GetAll_Reports_Running_From_A_Fresh_Reading()
    {
        using var db = TestDb.Create();
        await SeedAsync(db);
        var machineId = db.Machines.Single(m => m.MachineCode == "M-01").MachineId;

        db.OeeData.Add(new OeeData
        {
            MachineId = machineId,
            Timestamp = TimeZoneHelper.GetKsaNow(),
            Status = 1
        });
        await db.SaveChangesAsync();

        var result = await Create(db).GetAllAsync(activeOnly: false);

        var fleet = result.Value!;
        Assert.Equal("Running", fleet.Single(m => m.MachineCode == "M-01").RunningState);
        Assert.Equal("Stopped", fleet.Single(m => m.MachineCode == "M-02").RunningState);
    }

    /// <summary>A reading the platform cannot interpret is the one case that stays Unknown.</summary>
    [Fact]
    public async Task GetAll_Reports_Unknown_For_An_Uninterpretable_Status()
    {
        using var db = TestDb.Create();
        await SeedAsync(db);
        var machineId = db.Machines.Single(m => m.MachineCode == "M-01").MachineId;

        db.OeeData.Add(new OeeData
        {
            MachineId = machineId,
            Timestamp = TimeZoneHelper.GetKsaNow(),
            Status = 7
        });
        await db.SaveChangesAsync();

        var result = await Create(db).GetAllAsync(activeOnly: false);

        Assert.Equal("Unknown", result.Value!.Single(m => m.MachineCode == "M-01").RunningState);
    }

    /// <summary>
    /// The stored administrator flag and the derived live state are different answers. M-01 is
    /// flagged running by an administrator while its live state, with no reading behind it, is
    /// Stopped — exactly the confusion the two separate fields exist to prevent.
    /// </summary>
    [Fact]
    public async Task IsRunning_Flag_And_RunningState_Are_Separate()
    {
        using var db = TestDb.Create();
        await SeedAsync(db);

        var machine = (await Create(db).GetAllAsync(activeOnly: false)).Value!
            .Single(m => m.MachineCode == "M-01");

        Assert.True(machine.IsRunning);
        Assert.Equal("Stopped", machine.RunningState);
    }

    /// <summary>The state is a name, never an internal number (as with a work order's status).</summary>
    [Fact]
    public void RunningState_Is_A_String_Not_An_Enum_Integer()
    {
        Assert.Equal(typeof(string), typeof(MachineResponse).GetProperty(nameof(MachineResponse.RunningState))!.PropertyType);
    }

    /// <summary>
    /// The product rule does not leak into this one: a machine legitimately exposes its id, but it
    /// must still not expose a PRODUCT id (005 FR-022 is about products).
    /// </summary>
    [Fact]
    public void MachineResponse_Exposes_MachineId_But_No_Product_Id()
    {
        var names = typeof(MachineResponse).GetProperties().Select(p => p.Name.ToLowerInvariant()).ToList();
        Assert.Contains("machineid", names);
        Assert.DoesNotContain("productid", names);
    }
}
