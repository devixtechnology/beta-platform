using Microsoft.EntityFrameworkCore;

namespace BetaPlatform.Data;

/// <summary>
/// Ensures the read-only reporting SQL views exist on startup (created if missing).
/// These views are not managed by EF migrations; they are (re)created idempotently here.
/// Source of truth: the reference dump in Views.sql — DEFINER stripped so they run under any
/// connection user, and CREATE OR REPLACE used so an outdated definition is refreshed.
/// vw_machine_uptime_summary departs from that dump on one point: Total_Run_Time_Minutes is read
/// from <c>fn_work_order_effective_runtime</c> rather than re-derived from oee_data status
/// intervals, so the uptime view, the Machine Details screen and WorkOrder.ActiveDuration cannot
/// disagree about how long an order has actually run.
/// Order matters: the stored functions in <see cref="DatabaseFunctions"/> are applied first (a view
/// may call a function), then vw_running_orders_summary, then vw_machine_uptime_summary.
/// </summary>
public static class DbViewSeeder
{
    // Ported from regenerated_views.sql. Three departures from that file, each forced by Beta's
    // schema rather than chosen:
    //   * line_setup_time is line_setup_time_minutes here, still reported as LineSetupTime;
    //   * inputagg LEFT joins the source output and falls back to the input's own weight, because
    //     Beta inputs may be weighed in directly (003/004) — an inner join would silently drop
    //     every input recorded that way from TotalInputWeight;
    //   * FirstStartedAt and EffectiveRuntimeMinutes are kept. The reference system never had
    //     them, so its dump cannot mention them; dropping them here would be a regression.
    private const string RunningOrdersSummary = @"CREATE OR REPLACE VIEW `vw_running_orders_summary` AS
with `outputagg` as (
    select `work_order_outputs`.`work_order_id` AS `work_order_id`,
           count(`work_order_outputs`.`output_id`) AS `OutputCount`,
           coalesce(sum(`work_order_outputs`.`weight`),0) AS `TotalOutputWeight`
    from `work_order_outputs`
    group by `work_order_outputs`.`work_order_id`),
`inputagg` as (
    select `i`.`work_order_id` AS `work_order_id`,
           coalesce(sum(coalesce(`o`.`weight`,`i`.`weight`)),0) AS `TotalInputWeight`
    from (`work_order_inputs` `i`
          left join `work_order_outputs` `o` on(`i`.`source_output_id` = `o`.`output_id`))
    group by `i`.`work_order_id`),
`recentoutput` as (
    select `woo`.`work_order_id` AS `work_order_id`,
           `woo`.`unique_code` AS `UniqueCode`,
           `woo`.`weight` AS `Weight`,
           row_number() OVER (PARTITION BY `woo`.`work_order_id` ORDER BY `woo`.`created_at` DESC) AS `rn`
    from `work_order_outputs` `woo`
    where `woo`.`created_at` >= (now() - interval 1 minute))
select `wo`.`work_order_id` AS `WorkOrderId`,
       `wo`.`work_order_number` AS `WorkOrderNumber`,
       `wo`.`planned_start_time` AS `PlannedStartTime`,
       `wo`.`input_product_id` AS `InputProductId`,
       `wo`.`output_product_id` AS `OutputProductId`,
       `wo`.`hour_rate` AS `HourRate`,
       `wo`.`qty_to_manufacture` AS `QtyToManufacture`,
       `wo`.`workstation_capability_per_hour` AS `WorkstationCapabilityPerHour`,
       `wo`.`line_setup_time_minutes` AS `LineSetupTime`,
       cast(`wo`.`total_mixed_data` as signed) AS `TotalMixedData`,
       `wo`.`status` AS `Status`,
       `wo`.`order_type` AS `OrderType`,
       `wo`.`machine_type` AS `MachineType`,
       `wo`.`machine_id` AS `MachineId`,
       `m`.`machine_name` AS `MachineName`,
       `wo`.`created_at` AS `CreatedAt`,
       `wo`.`started_at` AS `StartedAt`,
       `wo`.`finished_at` AS `FinishedAt`,
       `wo`.`first_started_at` AS `FirstStartedAt`,
       `wo`.`sync_status` AS `SyncStatus`,
       `fn_work_order_effective_runtime`(`wo`.`work_order_id`) AS `EffectiveRuntimeMinutes`,
       coalesce(`oa`.`OutputCount`,0) AS `OutputCount`,
       coalesce(`oa`.`TotalOutputWeight`,0) AS `TotalOutputWeight`,
       coalesce(`ia`.`TotalInputWeight`,0) AS `TotalInputWeight`,
       `ro`.`UniqueCode` AS `UniqueCode`,
       `ro`.`Weight` AS `Weight`
from ((((`work_orders` `wo`
     left join `machines` `m` on(`wo`.`machine_id` = `m`.`machine_id`))
     left join `outputagg` `oa` on(`wo`.`work_order_id` = `oa`.`work_order_id`))
     left join `inputagg` `ia` on(`wo`.`work_order_id` = `ia`.`work_order_id`))
     left join `recentoutput` `ro` on(`wo`.`work_order_id` = `ro`.`work_order_id` and `ro`.`rn` = 1))
where (`wo`.`status` = 2)
order by `wo`.`work_order_id`";

    private const string MachineUptimeSummary = @"CREATE OR REPLACE VIEW `vw_machine_uptime_summary` AS with `calculatedintervals` as (select `o`.`machine_id` AS `machine_id`,`o`.`status` AS `status`,`o`.`timestamp` AS `start_time`,lead(`o`.`timestamp`,1,now()) OVER (PARTITION BY `o`.`machine_id` ORDER BY `o`.`timestamp` )  AS `end_time` from `oee_data` `o`) select `ros`.`MachineId` AS `MachineId`,(timestampdiff(SECOND,`ros`.`PlannedStartTime`,now()) / 60) AS `Total_Elapsed_Planned_Time_Minutes`,(timestampdiff(SECOND,`ros`.`StartedAt`,now()) / 60) AS `Total_Elapsed_Actual_Time_Minutes`,`fn_work_order_effective_runtime`(`ros`.`WorkOrderId`) AS `Total_Run_Time_Minutes`,(sum((case when (`ci`.`status` = 0) then timestampdiff(SECOND,(case when (`ci`.`start_time` > `ros`.`StartedAt`) then `ci`.`start_time` else `ros`.`StartedAt` end),(case when (`ci`.`end_time` < now()) then `ci`.`end_time` else now() end)) else 0 end)) / 60) AS `Total_Down_Time_Minutes` from (`vw_running_orders_summary` `ros` join `calculatedintervals` `ci` on((`ros`.`MachineId` = `ci`.`machine_id`))) where ((`ros`.`Status` = 2) and (`ci`.`end_time` > `ros`.`StartedAt`) and (`ci`.`start_time` < now())) group by `ros`.`MachineId`,`ros`.`StartedAt`,`ros`.`PlannedStartTime`,`ros`.`WorkOrderId`";

    public static async Task EnsureViewsAsync(ApplicationDbContext db)
    {
        // Applied unconditionally, not only when missing: CREATE OR REPLACE is idempotent, and
        // skipping when the views already exist would strand a database on an outdated definition
        // for ever — which is exactly what happened when EffectiveRuntimeMinutes was added.
        // Functions first: a view may call a function, never the other way around.
        await DatabaseFunctions.ApplyAsync(db.Database);

        await db.Database.ExecuteSqlRawAsync(RunningOrdersSummary);
        await db.Database.ExecuteSqlRawAsync(MachineUptimeSummary);
    }
}
