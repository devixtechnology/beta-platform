namespace BetaPlatform.Data.Entities;

/// <summary>
/// Work-order lifecycle statuses (FR-033). Beta adds <see cref="OnHold"/> relative to the
/// reference project. Allowed transitions (FR-034): Ready→InProgress; InProgress↔OnHold;
/// InProgress→Finished.
/// </summary>
public enum WorkOrderStatus
{
    Ready = 1,
    InProgress = 2,
    OnHold = 3,
    Finished = 4
}
