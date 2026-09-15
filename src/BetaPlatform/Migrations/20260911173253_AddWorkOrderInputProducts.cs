using System;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BetaPlatform.Migrations
{
        /// <summary>
        /// Adds <c>work_order_input_products</c> — the products a work order consumes — so an order can
        /// name several materials while <c>work_orders.output_product_id</c> stays singular (006 FR-022,
        /// 005 research R13).
        ///
        /// <para>
        /// <c>work_orders.input_product_id</c> is <b>kept, required and unchanged</b>. It holds the
        /// order's PRIMARY input, which is always the row at <c>position = 0</c> here (006 FR-025). That
        /// is what lets the Work Orders screens, <c>WorkOrderService</c> and the
        /// <c>vw_running_orders_summary</c> view keep reading the column they already read, with no
        /// edit to any of them.
        /// </para>
        ///
        /// <para>
        /// <c>Up</c> backfills one row for every work order that already exists, carrying its single
        /// input product. It is unambiguous — the column is <c>NOT NULL</c>, so every existing order has
        /// exactly one input and no human has to decide anything. After this migration, <b>no</b> order
        /// in the platform is without an input-product row, so no reader ever has to handle the empty
        /// case (006 FR-026, SC-010).
        /// </para>
        ///
        /// <para>
        /// <c>Down</c> drops the table. That loses any input beyond the first for orders raised through
        /// the API, which is unavoidable: the column it would have to fold them back into holds one
        /// product. The primary input survives, because it was never stored here alone.
        /// </para>
        /// </summary>
    public partial class AddWorkOrderInputProducts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "work_order_input_products",
                columns: table => new
                {
                    work_order_input_product_id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    work_order_id = table.Column<int>(type: "int", nullable: false),
                    product_id = table.Column<int>(type: "int", nullable: false),
                    position = table.Column<int>(type: "int", nullable: false),
                    created_at = table.Column<DateTime>(type: "datetime(6)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_work_order_input_products", x => x.work_order_input_product_id);
                    table.ForeignKey(
                        name: "FK_work_order_input_products_products_product_id",
                        column: x => x.product_id,
                        principalTable: "products",
                        principalColumn: "product_id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_work_order_input_products_work_orders_work_order_id",
                        column: x => x.work_order_id,
                        principalTable: "work_orders",
                        principalColumn: "work_order_id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_work_order_input_products_product_id",
                table: "work_order_input_products",
                column: "product_id");

            migrationBuilder.CreateIndex(
                name: "IX_work_order_input_products_work_order_id_position",
                table: "work_order_input_products",
                columns: new[] { "work_order_id", "position" });

            migrationBuilder.CreateIndex(
                name: "IX_work_order_input_products_work_order_id_product_id",
                table: "work_order_input_products",
                columns: new[] { "work_order_id", "product_id" },
                unique: true);

            // ---- Backfill: every existing order gets its one input product at position 0 ----
            // Idempotent via NOT EXISTS, so re-running against a database where the rows are already
            // present changes nothing. Set-based rather than row-by-row: this runs during a
            // deployment window and must not scale with the order count.
            migrationBuilder.Sql(@"
                INSERT INTO `work_order_input_products`
                    (`work_order_id`, `product_id`, `position`, `created_at`)
                SELECT `wo`.`work_order_id`, `wo`.`input_product_id`, 0, `wo`.`created_at`
                FROM `work_orders` `wo`
                WHERE NOT EXISTS (
                    SELECT 1 FROM `work_order_input_products` `wip`
                    WHERE `wip`.`work_order_id` = `wo`.`work_order_id`
                )");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "work_order_input_products");
        }
    }
}
