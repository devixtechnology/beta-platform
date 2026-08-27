using System;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BetaPlatform.Migrations
{
    /// <inheritdoc />
    public partial class ChangeRequest003_OeeInputsWorkOrderFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "work_order_outputs");

            migrationBuilder.AddColumn<decimal>(
                name: "hour_rate",
                table: "work_orders",
                type: "decimal(10,2)",
                precision: 10,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "line_setup_time_minutes",
                table: "work_orders",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "workstation_capability_per_hour",
                table: "work_orders",
                type: "decimal(10,2)",
                precision: 10,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "total_weight",
                table: "oee_data",
                type: "decimal(65,30)",
                nullable: false,
                defaultValue: 0m);

            // 003 change request: the legacy `total_count` column actually held produced *weight*.
            // Move that historical data into the new `total_weight` column, then reset `total_count`
            // so it can serve as the real produced-unit counter going forward (written by IoT).
            migrationBuilder.Sql("UPDATE oee_data SET total_weight = total_count;");
            migrationBuilder.Sql("UPDATE oee_data SET total_count = 0;");

            migrationBuilder.CreateTable(
                name: "work_order_inputs",
                columns: table => new
                {
                    input_id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    work_order_id = table.Column<int>(type: "int", nullable: false),
                    weight = table.Column<decimal>(type: "decimal(10,2)", precision: 10, scale: 2, nullable: false),
                    sequence_number = table.Column<int>(type: "int", nullable: false),
                    created_at = table.Column<DateTime>(type: "datetime(6)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_work_order_inputs", x => x.input_id);
                    table.ForeignKey(
                        name: "FK_work_order_inputs_work_orders_work_order_id",
                        column: x => x.work_order_id,
                        principalTable: "work_orders",
                        principalColumn: "work_order_id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_work_order_inputs_work_order_id_sequence_number",
                table: "work_order_inputs",
                columns: new[] { "work_order_id", "sequence_number" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "work_order_inputs");

            migrationBuilder.DropColumn(
                name: "hour_rate",
                table: "work_orders");

            migrationBuilder.DropColumn(
                name: "line_setup_time_minutes",
                table: "work_orders");

            migrationBuilder.DropColumn(
                name: "workstation_capability_per_hour",
                table: "work_orders");

            migrationBuilder.DropColumn(
                name: "total_weight",
                table: "oee_data");

            migrationBuilder.CreateTable(
                name: "work_order_outputs",
                columns: table => new
                {
                    output_id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    work_order_id = table.Column<int>(type: "int", nullable: false),
                    created_at = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    notes = table.Column<string>(type: "varchar(500)", maxLength: 500, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    sequence_number = table.Column<int>(type: "int", nullable: false),
                    unique_code = table.Column<string>(type: "varchar(50)", maxLength: 50, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    weight = table.Column<decimal>(type: "decimal(10,2)", precision: 10, scale: 2, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_work_order_outputs", x => x.output_id);
                    table.ForeignKey(
                        name: "FK_work_order_outputs_work_orders_work_order_id",
                        column: x => x.work_order_id,
                        principalTable: "work_orders",
                        principalColumn: "work_order_id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_work_order_outputs_unique_code",
                table: "work_order_outputs",
                column: "unique_code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_work_order_outputs_work_order_id_sequence_number",
                table: "work_order_outputs",
                columns: new[] { "work_order_id", "sequence_number" });
        }
    }
}
