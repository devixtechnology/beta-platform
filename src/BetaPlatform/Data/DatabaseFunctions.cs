using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace BetaPlatform.Data;

/// <summary>
/// Single source of truth for the database's stored functions, ported from the reference project
/// (SPackEdgeView <c>Data/DatabaseFunctions.cs</c>). Applied from the migration (the formal path)
/// and again at startup, so the function exists regardless of migration history.
///
/// Functions are applied BEFORE views: a view may call a function, never the other way around.
///
/// MySQL has no CREATE OR REPLACE FUNCTION, so each function is authored as a DROP + CREATE pair.
/// </summary>
public static class DatabaseFunctions
{
    /// <summary>
    /// <c>fn_work_order_effective_runtime(work_order_id)</c> — the live, hold-excluded actual
    /// runtime of a work order, in minutes. Takes only the order id and looks the row up itself.
    /// Mirrors <c>WorkOrder.ActiveDuration</c> exactly: the banked <c>total_runtime</c>, plus the
    /// in-flight segment (now − started_at) only while the order is InProgress (status = 2).
    /// Returns 0 for an unknown id — a defensive floor; the views always pass a valid id.
    ///
    /// Timezone: <c>started_at</c> is stored as KSA wall-clock (the app writes
    /// <c>TimeZoneHelper.GetKsaNow()</c>, UTC+3), so "now" is computed as
    /// <c>UTC_TIMESTAMP() + INTERVAL 3 HOUR</c> rather than <c>NOW()</c>. <c>UTC_TIMESTAMP()</c>
    /// ignores the session <c>time_zone</c>, so the result is correct for every caller: the app,
    /// the reporting views, and a plain Workbench/CLI session whose session default is often UTC.
    /// The reference project hit NEGATIVE runtimes with <c>NOW()</c> for exactly this reason —
    /// a UTC session clock sat ~3 h behind a KSA <c>started_at</c>. KSA is a fixed UTC+3 with no
    /// DST, so the constant offset is safe.
    ///
    /// NOT DETERMINISTIC (reads the clock); READS SQL DATA (queries <c>work_orders</c>) so it can
    /// be created with binary logging on and <c>log_bin_trust_function_creators = 0</c>.
    /// </summary>
    public const string EffectiveRuntimeDrop =
        "DROP FUNCTION IF EXISTS `fn_work_order_effective_runtime`;";

    public const string EffectiveRuntimeCreate = @"
CREATE FUNCTION `fn_work_order_effective_runtime`(`p_work_order_id` INT)
RETURNS DECIMAL(10,2)
NOT DETERMINISTIC
READS SQL DATA
BEGIN
    DECLARE `v_total_runtime` DECIMAL(10,2);
    DECLARE `v_status`        INT;
    DECLARE `v_started_at`    DATETIME;

    SELECT `total_runtime`, `status`, `started_at`
      INTO `v_total_runtime`, `v_status`, `v_started_at`
      FROM `work_orders`
     WHERE `work_order_id` = `p_work_order_id`;

    RETURN COALESCE(`v_total_runtime`, 0)
         + IF(`v_status` = 2 AND `v_started_at` IS NOT NULL,
              TIMESTAMPDIFF(SECOND, `v_started_at`, UTC_TIMESTAMP() + INTERVAL 3 HOUR) / 60,
              0);
END";

    /// <summary>All statements, ordered drop-then-create. Applying repeatedly is safe and always
    /// converges to the definitions above.</summary>
    public static readonly string[] All =
    {
        EffectiveRuntimeDrop,
        EffectiveRuntimeCreate,
    };

    /// <summary>Drop statements only, for migration rollback.</summary>
    public static readonly string[] DropAll =
    {
        EffectiveRuntimeDrop,
    };

    /// <summary>Apply from within an EF Core migration.</summary>
    public static void Apply(MigrationBuilder migrationBuilder)
    {
        // suppressTransaction: CREATE/DROP FUNCTION is DDL and forces an implicit COMMIT in MySQL,
        // which would break the migration's surrounding transaction.
        foreach (var sql in All)
            migrationBuilder.Sql(sql, suppressTransaction: true);
    }

    /// <summary>Drop from within an EF Core migration.</summary>
    public static void Drop(MigrationBuilder migrationBuilder)
    {
        foreach (var sql in DropAll)
            migrationBuilder.Sql(sql, suppressTransaction: true);
    }

    /// <summary>Apply directly against the database at startup, before the views.</summary>
    public static async Task ApplyAsync(DatabaseFacade database, CancellationToken cancellationToken = default)
    {
        foreach (var sql in All)
            await database.ExecuteSqlRawAsync(sql, cancellationToken);
    }
}
