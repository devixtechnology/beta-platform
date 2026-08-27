using BetaPlatform.Data.Entities;

namespace BetaPlatform.ViewModels.Machines;

/// <summary>
/// A row/card on the machines list — the machine plus the running state resolved by the single
/// status rule. The view renders <see cref="RunningState"/>; it no longer reads
/// <c>Machine.IsRunning</c>, which was the source of the dashboard-vs-list contradiction the client
/// photographed (004 — comment 2).
/// </summary>
public class MachineListItemViewModel
{
    public Machine Machine { get; set; } = null!;
    public MachineRunningState RunningState { get; set; } = MachineRunningState.Unknown;
}
