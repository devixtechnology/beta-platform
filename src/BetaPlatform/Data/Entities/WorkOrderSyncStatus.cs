namespace BetaPlatform.Data.Entities;

/// <summary>
/// The result of telling the upstream ERP that a work order finished, as the reporting views
/// expose it (<c>work_orders.sync_status</c>).
/// </summary>
/// <remarks>
/// Beta notifies the ERP <em>before</em> it commits a transition and refuses the transition when
/// the call did not succeed (see <c>WorkOrderService</c>), so a finished Beta order is one the ERP
/// accepted. The column is kept because the views of the reference system report it and consumers
/// read it; it is not a retry queue.
/// </remarks>
public enum WorkOrderSyncStatus
{
    /// <summary>Not finished yet, or finished while no real ERP was configured.</summary>
    Pending = 0,

    /// <summary>The ERP acknowledged the completion.</summary>
    Synced = 1,

    /// <summary>The ERP did not accept the completion.</summary>
    FailedToSync = 2
}
