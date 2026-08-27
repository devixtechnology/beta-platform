namespace BetaPlatform.Data.Entities;

/// <summary>
/// The single machine running state used by every screen (004 — client comment 2). It is
/// <b>derived, never persisted</b>: there is no column behind it. It is resolved once from the
/// machine's latest <c>oee_data</c> row by <see cref="Services.MachineStatusRules"/> and consumed
/// by the dashboard, the machines list/card views, the machine details page, and the production
/// display. <c>machines.is_running</c> is an administrator flag and no longer drives any status
/// display.
/// </summary>
public enum MachineRunningState
{
    /// <summary>Latest reading is fresh and reports <c>status = 1</c>.</summary>
    Running,

    /// <summary>Latest reading is fresh and reports <c>status = 0</c>.</summary>
    Stopped,

    /// <summary>No reading, a reading older than the staleness threshold, or an unrecognised status.</summary>
    Unknown
}
