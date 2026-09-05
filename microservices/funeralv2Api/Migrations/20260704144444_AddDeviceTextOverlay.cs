using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace funeralv2Api.Migrations
{
    /// <inheritdoc />
    public partial class AddDeviceTextOverlay : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "room_id",
                schema: "smfr",
                table: "deceaseds");

            migrationBuilder.CreateTable(
                name: "device_text_overlays",
                schema: "smfr",
                columns: table => new
                {
                    id = table.Column<string>(type: "text", nullable: false),
                    device_id = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    text_content = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    font_size = table.Column<decimal>(type: "numeric(6,3)", nullable: false),
                    font_color = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    background_color = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    text_align = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    font_weight = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    position_left = table.Column<decimal>(type: "numeric(6,3)", nullable: false),
                    position_top = table.Column<decimal>(type: "numeric(6,3)", nullable: false),
                    width = table.Column<decimal>(type: "numeric(6,3)", nullable: false),
                    height = table.Column<decimal>(type: "numeric(6,3)", nullable: false),
                    sort_order = table.Column<int>(type: "integer", nullable: false),
                    remark = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    createdat = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    createdby = table.Column<string>(type: "text", nullable: true),
                    updatedat = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    updatedby = table.Column<string>(type: "text", nullable: true),
                    isdeleted = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_device_text_overlays", x => x.id);
                    table.ForeignKey(
                        name: "FK_device_text_overlays_devices_device_id",
                        column: x => x.device_id,
                        principalSchema: "smfr",
                        principalTable: "devices",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_device_text_overlays_device_id",
                schema: "smfr",
                table: "device_text_overlays",
                column: "device_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "device_text_overlays",
                schema: "smfr");

            migrationBuilder.AddColumn<string>(
                name: "room_id",
                schema: "smfr",
                table: "deceaseds",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true);
        }
    }
}
