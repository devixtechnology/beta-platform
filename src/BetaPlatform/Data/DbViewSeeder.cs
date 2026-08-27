using Microsoft.EntityFrameworkCore;

namespace BetaPlatform.Data;

/// <summary>
/// Ensures the read-only reporting SQL views exist on startup (created if missing).
/// These views are not managed by EF migrations; they are (re)created idempotently here.
/// Source of truth: the reference dump in Views.sql — DEFINER stripped so they run under any
/// connection user, and CREATE OR REPLACE used so an outdated definition is refreshed.
/// Order matters: the stored functions in <see cref="DatabaseFunctions"/> are applied first (a view
/// may call a function), then vw_running_orders_summary, then vw_machine_uptime_summary.
/// </summary>
public static class DbViewSeeder
{
    private const string RunningOrdersSummary = @"CREATE OR REPLACE VIEW `vw_running_orders_summary` AS with `inputagg` as (select `work_order_inputs`.`work_order_id` AS `work_order_id`,coalesce(sum(`work_order_inputs`.`weight`),0) AS `TotalInputWeight` from `work_order_inputs` group by `work_order_inputs`.`work_order_id`) select `wo`.`work_order_id` AS `WorkOrderId`,`wo`.`work_order_number` AS `WorkOrderNumber`,`wo`.`input_product_id` AS `InputProductId`,`wo`.`output_product_id` AS `OutputProductId`,`wo`.`planned_start_time` AS `PlannedStartTime`,`wo`.`qty_to_manufacture` AS `QtyToManufacture`,`wo`.`hour_rate` AS `HourRate`,`wo`.`workstation_capability_per_hour` AS `WorkstationCapabilityPerHour`,`wo`.`line_setup_time_minutes` AS `LineSetupTime`,`wo`.`status` AS `Status`,`wo`.`machine_id` AS `MachineId`,`wo`.`created_at` AS `CreatedAt`,`wo`.`started_at` AS `StartedAt`,`wo`.`finished_at` AS `FinishedAt`,`wo`.`first_started_at` AS `FirstStartedAt`,`fn_work_order_effective_runtime`(`wo`.`work_order_id`) AS `EffectiveRuntimeMinutes`,coalesce(`ia`.`TotalInputWeight`,0) AS `TotalInputWeight` from (`work_orders` `wo` left join `inputagg` `ia` on((`wo`.`work_order_id` = `ia`.`work_order_id`))) where (`wo`.`status` = 2) order by `wo`.`work_order_id`";

    private const string MachineUptimeSummary = @"CREATE OR REPLACE VIEW `vw_machine_uptime_summary` AS with `calculatedintervals` as (select `o`.`machine_id` AS `machine_id`,`o`.`status` AS `status`,`o`.`timestamp` AS `start_time`,lead(`o`.`timestamp`,1,now()) OVER (PARTITION BY `o`.`machine_id` ORDER BY `o`.`timestamp` )  AS `end_time` from `oee_data` `o`) select `ros`.`MachineId` AS `MachineId`,(timestampdiff(SECOND,`ros`.`PlannedStartTime`,now()) / 60) AS `Total_Elapsed_Planned_Time_Minutes`,(timestampdiff(SECOND,`ros`.`StartedAt`,now()) / 60) AS `Total_Elapsed_Actual_Time_Minutes`,(sum((case when (`ci`.`status` = 1) then timestampdiff(SECOND,(case when (`ci`.`start_time` > `ros`.`StartedAt`) then `ci`.`start_time` else `ros`.`StartedAt` end),(case when (`ci`.`end_time` < now()) then `ci`.`end_time` else now() end)) else 0 end)) / 60) AS `Total_Run_Time_Minutes`,(sum((case when (`ci`.`status` = 0) then timestampdiff(SECOND,(case when (`ci`.`start_time` > `ros`.`StartedAt`) then `ci`.`start_time` else `ros`.`StartedAt` end),(case when (`ci`.`end_time` < now()) then `ci`.`end_time` else now() end)) else 0 end)) / 60) AS `Total_Down_Time_Minutes` from (`vw_running_orders_summary` `ros` join `calculatedintervals` `ci` on((`ros`.`MachineId` = `ci`.`machine_id`))) where ((`ros`.`Status` = 2) and (`ci`.`end_time` > `ros`.`StartedAt`) and (`ci`.`start_time` < now())) group by `ros`.`MachineId`,`ros`.`StartedAt`,`ros`.`PlannedStartTime`";

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
