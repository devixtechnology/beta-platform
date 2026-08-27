using System;
using BetaPlatform.Data;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BetaPlatform.Migrations
{
    /// <summary>
    /// Hold-aware runtime accounting for work orders, ported from the reference project's
    /// ReintroduceWorkOrderHoldWithRuntime migration. <c>first_started_at</c> preserves the
    /// original start across holds; <c>total_runtime</c> banks completed active segments in
    /// minutes, so held time is never counted as production. Adds
    /// <c>fn_work_order_effective_runtime</c>, the SQL mirror of <c>WorkOrder.ActiveDuration</c>.
    /// </summary>
    public partial class AddWorkOrderRuntimeAccounting : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "first_started_at",
                table: "work_orders",
                type: "datetime(6)",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "total_runtime",
                table: "work_orders",
                type: "decimal(10,2)",
                precision: 10,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            // Backfill existing rows. Orders predate the accumulator, so their whole run counts as
            // active time: nothing was ever held under the old model.
            migrationBuilder.Sql(
                "UPDATE `work_orders` SET `first_started_at` = `started_at` WHERE `started_at` IS NOT NULL;");
            migrationBuilder.Sql(
                "UPDATE `work_orders` " +
                "SET `total_runtime` = TIMESTAMPDIFF(SECOND, `started_at`, `finished_at`) / 60 " +
                "WHERE `status` = 3 AND `started_at` IS NOT NULL AND `finished_at` IS NOT NULL;");

            DatabaseFunctions.Apply(migrationBuilder);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            DatabaseFunctions.Drop(migrationBuilder);

            migrationBuilder.DropColumn(
                name: "first_started_at",
                table: "work_orders");

            migrationBuilder.DropColumn(
                name: "total_runtime",
                table: "work_orders");
        }
    }
}
