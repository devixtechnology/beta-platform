using System;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BetaPlatform.Migrations
{
    /// <inheritdoc />
    public partial class AddWorkOrderOutputsAndChainColumns : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "machine_type",
                table: "work_orders",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "order_type",
                table: "work_orders",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "sync_status",
                table: "work_orders",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "total_mixed_data",
                table: "work_orders",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "source_output_id",
                table: "work_order_inputs",
                type: "int",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "work_order_outputs",
                columns: table => new
                {
                    output_id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    work_order_id = table.Column<int>(type: "int", nullable: false),
                    unique_code = table.Column<string>(type: "varchar(50)", maxLength: 50, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    weight = table.Column<decimal>(type: "decimal(10,2)", precision: 10, scale: 2, nullable: false),
                    sequence_number = table.Column<int>(type: "int", nullable: false),
                    created_at = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    notes = table.Column<string>(type: "varchar(500)", maxLength: 500, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    print_status = table.Column<bool>(type: "tinyint(1)", nullable: false)
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
                name: "IX_work_orders_machine_type",
                table: "work_orders",
                column: "machine_type");

            migrationBuilder.CreateIndex(
                name: "IX_work_orders_order_type",
                table: "work_orders",
                column: "order_type");

            migrationBuilder.CreateIndex(
                name: "IX_work_order_inputs_source_output_id",
                table: "work_order_inputs",
                column: "source_output_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_work_order_outputs_created_at",
                table: "work_order_outputs",
                column: "created_at");

            migrationBuilder.CreateIndex(
                name: "IX_work_order_outputs_print_status",
                table: "work_order_outputs",
                column: "print_status");

            migrationBuilder.CreateIndex(
                name: "IX_work_order_outputs_unique_code",
                table: "work_order_outputs",
                column: "unique_code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_work_order_outputs_work_order_id",
                table: "work_order_outputs",
                column: "work_order_id");

            migrationBuilder.AddForeignKey(
                name: "FK_work_order_inputs_work_order_outputs_source_output_id",
                table: "work_order_inputs",
                column: "source_output_id",
                principalTable: "work_order_outputs",
                principalColumn: "output_id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_work_orders_machine_types_machine_type",
                table: "work_orders",
                column: "machine_type",
                principalTable: "machine_types",
                principalColumn: "machine_type_id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_work_orders_machine_types_order_type",
                table: "work_orders",
                column: "order_type",
                principalTable: "machine_types",
                principalColumn: "machine_type_id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_work_order_inputs_work_order_outputs_source_output_id",
                table: "work_order_inputs");

            migrationBuilder.DropForeignKey(
                name: "FK_work_orders_machine_types_machine_type",
                table: "work_orders");

            migrationBuilder.DropForeignKey(
                name: "FK_work_orders_machine_types_order_type",
                table: "work_orders");

            migrationBuilder.DropTable(
                name: "work_order_outputs");

            migrationBuilder.DropIndex(
                name: "IX_work_orders_machine_type",
                table: "work_orders");

            migrationBuilder.DropIndex(
                name: "IX_work_orders_order_type",
                table: "work_orders");

            migrationBuilder.DropIndex(
                name: "IX_work_order_inputs_source_output_id",
                table: "work_order_inputs");

            migrationBuilder.DropColumn(
                name: "machine_type",
                table: "work_orders");

            migrationBuilder.DropColumn(
                name: "order_type",
                table: "work_orders");

            migrationBuilder.DropColumn(
                name: "sync_status",
                table: "work_orders");

            migrationBuilder.DropColumn(
                name: "total_mixed_data",
                table: "work_orders");

            migrationBuilder.DropColumn(
                name: "source_output_id",
                table: "work_order_inputs");
        }
    }
}
