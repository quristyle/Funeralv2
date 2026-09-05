using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace funeralv2Api.Migrations
{
    /// <inheritdoc />
    public partial class AddDeviceRibbon : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "device_ribbons",
                schema: "smfr",
                columns: table => new
                {
                    id = table.Column<string>(type: "text", nullable: false),
                    device_id = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    media_source_id = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
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
                    table.PrimaryKey("PK_device_ribbons", x => x.id);
                    table.ForeignKey(
                        name: "FK_device_ribbons_devices_device_id",
                        column: x => x.device_id,
                        principalSchema: "smfr",
                        principalTable: "devices",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_device_ribbons_media_sources_media_source_id",
                        column: x => x.media_source_id,
                        principalSchema: "smfr",
                        principalTable: "media_sources",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_device_ribbons_device_id",
                schema: "smfr",
                table: "device_ribbons",
                column: "device_id");

            migrationBuilder.CreateIndex(
                name: "IX_device_ribbons_media_source_id",
                schema: "smfr",
                table: "device_ribbons",
                column: "media_source_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "device_ribbons",
                schema: "smfr");
        }
    }
}
