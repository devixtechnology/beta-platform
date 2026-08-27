using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BetaPlatform.Migrations
{
    /// <inheritdoc />
    public partial class AddIotConfigTables : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "machine_tags",
                columns: table => new
                {
                    id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    machine_id = table.Column<int>(type: "int", nullable: false),
                    machine_name = table.Column<string>(type: "varchar(255)", maxLength: 255, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    name = table.Column<string>(type: "varchar(255)", maxLength: 255, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    type = table.Column<int>(type: "int", nullable: false),
                    source = table.Column<int>(type: "int", nullable: false),
                    source_identifier = table.Column<string>(type: "varchar(255)", maxLength: 255, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    module = table.Column<int>(type: "int", nullable: false),
                    data_type = table.Column<int>(type: "int", nullable: false),
                    value = table.Column<decimal>(type: "decimal(10,2)", nullable: true),
                    code = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    location_address = table.Column<string>(type: "varchar(255)", maxLength: 255, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    enable_log = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    log_table_name = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    log_per_ms = table.Column<int>(type: "int", nullable: false),
                    unit = table.Column<string>(type: "varchar(50)", maxLength: 50, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_machine_tags", x => x.id);
                    table.ForeignKey(
                        name: "FK_machine_tags_machines_machine_id",
                        column: x => x.machine_id,
                        principalTable: "machines",
                        principalColumn: "machine_id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "machines_kpis",
                columns: table => new
                {
                    id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    name = table.Column<string>(type: "varchar(255)", maxLength: 255, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    type = table.Column<int>(type: "int", nullable: false),
                    source = table.Column<int>(type: "int", nullable: false),
                    source_identifier = table.Column<string>(type: "varchar(255)", maxLength: 255, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    module = table.Column<int>(type: "int", nullable: false),
                    data_type = table.Column<int>(type: "int", nullable: false),
                    value = table.Column<decimal>(type: "decimal(10,2)", nullable: true),
                    code = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    location_address = table.Column<string>(type: "varchar(255)", maxLength: 255, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    enable_log = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    log_table_name = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    log_per_ms = table.Column<int>(type: "int", nullable: false),
                    unit = table.Column<string>(type: "varchar(50)", maxLength: 50, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_machines_kpis", x => x.id);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "machines_master_data",
                columns: table => new
                {
                    id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    name = table.Column<string>(type: "varchar(255)", maxLength: 255, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    type = table.Column<int>(type: "int", nullable: false),
                    source = table.Column<int>(type: "int", nullable: false),
                    source_identifier = table.Column<string>(type: "varchar(255)", maxLength: 255, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    module = table.Column<int>(type: "int", nullable: false),
                    data_type = table.Column<int>(type: "int", nullable: false),
                    value = table.Column<decimal>(type: "decimal(10,2)", nullable: true),
                    code = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    location_address = table.Column<string>(type: "varchar(255)", maxLength: 255, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    enable_log = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    log_table_name = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    log_per_ms = table.Column<int>(type: "int", nullable: false),
                    unit = table.Column<string>(type: "varchar(50)", maxLength: 50, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_machines_master_data", x => x.id);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "sources_available",
                columns: table => new
                {
                    id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    source = table.Column<int>(type: "int", nullable: false),
                    source_identifier = table.Column<string>(type: "varchar(255)", maxLength: 255, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    module = table.Column<int>(type: "int", nullable: false),
                    source_configuration = table.Column<string>(type: "text", nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    status = table.Column<int>(type: "int", nullable: false),
                    enable = table.Column<bool>(type: "tinyint(1)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_sources_available", x => x.id);
                    table.UniqueConstraint("AK_sources_available_source_identifier", x => x.source_identifier);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "machine_properties",
                columns: table => new
                {
                    id = table.Column<ulong>(type: "bigint unsigned", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    machine_id = table.Column<int>(type: "int", nullable: false),
                    machine_name = table.Column<string>(type: "varchar(50)", maxLength: 50, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    name = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    type = table.Column<int>(type: "int", nullable: true),
                    source = table.Column<int>(type: "int", nullable: true),
                    source_identifier = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    module = table.Column<int>(type: "int", nullable: true),
                    data_type = table.Column<int>(type: "int", nullable: true),
                    value = table.Column<string>(type: "text", nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    code = table.Column<string>(type: "varchar(50)", maxLength: 50, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    location_address = table.Column<string>(type: "varchar(255)", maxLength: 255, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    enable_log = table.Column<bool>(type: "tinyint(1)", nullable: true),
                    log_table_name = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    log_per_ms = table.Column<uint>(type: "int unsigned", nullable: true),
                    unit = table.Column<string>(type: "varchar(50)", maxLength: 50, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_machine_properties", x => x.id);
                    table.ForeignKey(
                        name: "FK_machine_properties_machines_machine_id",
                        column: x => x.machine_id,
                        principalTable: "machines",
                        principalColumn: "machine_id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_machine_properties_sources_available_source_identifier",
                        column: x => x.source_identifier,
                        principalTable: "sources_available",
                        principalColumn: "source_identifier",
                        onDelete: ReferentialAction.Restrict);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_machine_properties_machine_id",
                table: "machine_properties",
                column: "machine_id");

            migrationBuilder.CreateIndex(
                name: "IX_machine_properties_name",
                table: "machine_properties",
                column: "name");

            migrationBuilder.CreateIndex(
                name: "IX_machine_properties_source_identifier",
                table: "machine_properties",
                column: "source_identifier");

            migrationBuilder.CreateIndex(
                name: "uk_machine_prop_detailed",
                table: "machine_properties",
                columns: new[] { "machine_id", "name", "code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_machine_tags_machine_id",
                table: "machine_tags",
                column: "machine_id");

            migrationBuilder.CreateIndex(
                name: "IX_machine_tags_source",
                table: "machine_tags",
                column: "source");

            migrationBuilder.CreateIndex(
                name: "IX_machine_tags_source_identifier",
                table: "machine_tags",
                column: "source_identifier");

            migrationBuilder.CreateIndex(
                name: "IX_machines_kpis_source_identifier",
                table: "machines_kpis",
                column: "source_identifier");

            migrationBuilder.CreateIndex(
                name: "IX_machines_master_data_source_identifier",
                table: "machines_master_data",
                column: "source_identifier");

            migrationBuilder.CreateIndex(
                name: "IX_sources_available_source",
                table: "sources_available",
                column: "source");

            migrationBuilder.CreateIndex(
                name: "IX_sources_available_source_identifier",
                table: "sources_available",
                column: "source_identifier",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "machine_properties");

            migrationBuilder.DropTable(
                name: "machine_tags");

            migrationBuilder.DropTable(
                name: "machines_kpis");

            migrationBuilder.DropTable(
                name: "machines_master_data");

            migrationBuilder.DropTable(
                name: "sources_available");
        }
    }
}
