using BetaPlatform.Data.Entities;
using BetaPlatform.Services;
using Xunit;

namespace BetaPlatform.Tests;

/// <summary>
/// Covers the cases in specs/004-phase1-feedback/contracts/machine-status.md. This is the one
/// rule every screen consumes, so it is tested on its own — pure, no database, no web host.
/// </summary>
public class MachineStatusRulesTests
{
    private static readonly DateTime Now = new(2026, 8, 23, 14, 0, 0);
    private static readonly TimeSpan StaleAfter = TimeSpan.FromMinutes(5);

    private static OeeData Reading(byte status, DateTime timestamp) =>
        new() { MachineId = 1, Status = status, Timestamp = timestamp };

    [Fact]
    public void Null_Reading_Is_Stopped()
    {
        // A machine that has never reported is not producing — silence defaults to Stopped.
        Assert.Equal(MachineRunningState.Stopped, MachineStatusRules.Resolve(null, Now, StaleAfter, false));
    }

    [Fact]
    public void Fresh_Status_One_Is_Running()
    {
        var latest = Reading(1, Now.AddMinutes(-1));

        Assert.Equal(MachineRunningState.Running, MachineStatusRules.Resolve(latest, Now, StaleAfter, false));
    }

    [Fact]
    public void Fresh_Status_Zero_Is_Stopped()
    {
        var latest = Reading(0, Now.AddMinutes(-1));

        Assert.Equal(MachineRunningState.Stopped, MachineStatusRules.Resolve(latest, Now, StaleAfter, false));
    }

    [Fact]
    public void Running_Reading_Aged_Past_The_Threshold_Is_Stopped()
    {
        var latest = Reading(1, Now.AddMinutes(-6));

        Assert.Equal(MachineRunningState.Stopped, MachineStatusRules.Resolve(latest, Now, StaleAfter, false));
    }

    [Fact]
    public void Age_Exactly_At_The_Threshold_Is_Not_Stale()
    {
        var latest = Reading(1, Now.AddMinutes(-5));

        Assert.Equal(MachineRunningState.Running, MachineStatusRules.Resolve(latest, Now, StaleAfter, false));
    }

    [Fact]
    public void Future_Dated_Reading_Is_Treated_As_Current()
    {
        var latest = Reading(1, Now.AddMinutes(10));

        Assert.Equal(MachineRunningState.Running, MachineStatusRules.Resolve(latest, Now, StaleAfter, false));
    }

    [Fact]
    public void Unrecognised_Status_Is_Unknown()
    {
        var latest = Reading(7, Now.AddMinutes(-1));

        Assert.Equal(MachineRunningState.Unknown, MachineStatusRules.Resolve(latest, Now, StaleAfter, false));
    }

    // ---- An in-progress work order overrides telemetry outright (decision 2026-08-27) ----

    [Fact]
    public void Work_In_Progress_Makes_A_Silent_Machine_Running()
    {
        Assert.Equal(MachineRunningState.Running, MachineStatusRules.Resolve(null, Now, StaleAfter, true));
    }

    [Fact]
    public void Work_In_Progress_Overrides_A_Fresh_Stopped_Reading()
    {
        var latest = Reading(0, Now.AddMinutes(-1));

        Assert.Equal(MachineRunningState.Running, MachineStatusRules.Resolve(latest, Now, StaleAfter, true));
    }

    [Fact]
    public void Work_In_Progress_Overrides_A_Stale_Reading()
    {
        var latest = Reading(0, Now.AddDays(-49));

        Assert.Equal(MachineRunningState.Running, MachineStatusRules.Resolve(latest, Now, StaleAfter, true));
    }

    [Fact]
    public void Work_In_Progress_Overrides_An_Unrecognised_Status_Byte()
    {
        var latest = Reading(7, Now.AddMinutes(-1));

        Assert.Equal(MachineRunningState.Running, MachineStatusRules.Resolve(latest, Now, StaleAfter, true));
    }
}
