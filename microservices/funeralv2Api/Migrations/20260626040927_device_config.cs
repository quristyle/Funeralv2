using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace funeralv2Api.Migrations
{
    /// <inheritdoc />
    public partial class device_config : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "device_configs",
                schema: "smfr",
                columns: table => new
                {
                    id = table.Column<string>(type: "text", nullable: false),
                    device_id = table.Column<string>(type: "text", nullable: false),
                    volume = table.Column<int>(type: "integer", nullable: false),
                    brightness = table.Column<int>(type: "integer", nullable: false),
                    reboot_time = table.Column<string>(type: "character varying(5)", maxLength: 5, nullable: true),
                    is_auto_power = table.Column<bool>(type: "boolean", nullable: false),
                    power_on_time = table.Column<string>(type: "character varying(5)", maxLength: 5, nullable: true),
                    power_off_time = table.Column<string>(type: "character varying(5)", maxLength: 5, nullable: true),
                    createdat = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    createdby = table.Column<string>(type: "text", nullable: true),
                    updatedat = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    updatedby = table.Column<string>(type: "text", nullable: true),
                    isdeleted = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_device_configs", x => x.id);
                    table.ForeignKey(
                        name: "FK_device_configs_devices_device_id",
                        column: x => x.device_id,
                        principalSchema: "smfr",
                        principalTable: "devices",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_device_configs_device_id",
                schema: "smfr",
                table: "device_configs",
                column: "device_id",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "device_configs",
                schema: "smfr");
        }
    }
}
