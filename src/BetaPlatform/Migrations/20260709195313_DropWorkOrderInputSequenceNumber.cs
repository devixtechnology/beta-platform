using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BetaPlatform.Migrations
{
    /// <inheritdoc />
    public partial class DropWorkOrderInputSequenceNumber : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Create the replacement single-column index BEFORE dropping the composite one, so the
            // FK on work_order_id always has a supporting index (MySQL rejects dropping it otherwise).
            migrationBuilder.CreateIndex(
                name: "IX_work_order_inputs_work_order_id",
                table: "work_order_inputs",
                column: "work_order_id");

            migrationBuilder.DropIndex(
                name: "IX_work_order_inputs_work_order_id_sequence_number",
                table: "work_order_inputs");

            migrationBuilder.DropColumn(
                name: "sequence_number",
                table: "work_order_inputs");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_work_order_inputs_work_order_id",
                table: "work_order_inputs");

            migrationBuilder.AddColumn<int>(
                name: "sequence_number",
                table: "work_order_inputs",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateIndex(
                name: "IX_work_order_inputs_work_order_id_sequence_number",
                table: "work_order_inputs",
                columns: new[] { "work_order_id", "sequence_number" });
        }
    }
}
