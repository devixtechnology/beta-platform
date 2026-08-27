using BetaPlatform.Data.Entities;

namespace BetaPlatform.Services;

/// <summary>
/// The single rule for "is this machine running?" (004 — contracts/machine-status.md). Pure and
/// static: it takes the machine's latest <c>oee_data</c> row plus whether a job is open on it, and
/// answers. Every screen consumes this; no screen re-derives running state from
/// <c>Machine.IsRunning</c> or <c>OeeData.Status</c>.
/// </summary>
public static class MachineStatusRules
{
    /// <summary>
    /// Resolves running state. An in-progress work order wins outright: while a job is open the
    /// machine is presented as <see cref="MachineRunningState.Running"/> whatever telemetry says
    /// (decision 2026-08-27). Otherwise the latest reading decides, and a machine that has never
    /// reported — or whose last reading has aged past <paramref name="staleAfter"/> — is
    /// <see cref="MachineRunningState.Stopped"/>: silence means not producing.
    /// <see cref="MachineRunningState.Unknown"/> survives only for a reading whose status byte the
    /// platform cannot interpret.
    /// </summary>
    /// <param name="latest">The machine's most recent OEE reading, or <c>null</c> when it has none.</param>
    /// <param name="now">Current time on the platform's KSA-local basis.</param>
    /// <param name="staleAfter">Maximum age of a reading still considered live.</param>
    /// <param name="hasWorkInProgress">Whether a work order on this machine is in progress.</param>
    public static MachineRunningState Resolve(
        OeeData? latest, DateTime now, TimeSpan staleAfter, bool hasWorkInProgress)
    {
        // Operator-entered, and deliberately allowed to override the sensor. The cost of the choice:
        // a job left open keeps its machine Running until someone finishes it.
        if (hasWorkInProgress) return MachineRunningState.Running;

        if (latest is null) return MachineRunningState.Stopped;

        // An age exactly equal to the threshold is NOT stale; a future-dated reading is current.
        var age = now - latest.Timestamp;
        if (age > staleAfter) return MachineRunningState.Stopped;

        return latest.Status switch
        {
            1 => MachineRunningState.Running,
            0 => MachineRunningState.Stopped,
            _ => MachineRunningState.Unknown
        };
    }
}
