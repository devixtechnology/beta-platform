using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BetaPlatform.Migrations
{
    /// <inheritdoc />
    public partial class AddWorkOrderInputProduct : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "product_id",
                table: "work_order_inputs",
                type: "int",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_work_order_inputs_product_id",
                table: "work_order_inputs",
                column: "product_id");

            migrationBuilder.AddForeignKey(
                name: "FK_work_order_inputs_products_product_id",
                table: "work_order_inputs",
                column: "product_id",
                principalTable: "products",
                principalColumn: "product_id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_work_order_inputs_products_product_id",
                table: "work_order_inputs");

            migrationBuilder.DropIndex(
                name: "IX_work_order_inputs_product_id",
                table: "work_order_inputs");

            migrationBuilder.DropColumn(
                name: "product_id",
                table: "work_order_inputs");
        }
    }
}
