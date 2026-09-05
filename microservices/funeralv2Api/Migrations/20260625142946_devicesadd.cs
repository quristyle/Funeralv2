using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace funeralv2Api.Migrations
{
    /// <inheritdoc />
    public partial class devicesadd : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "devices",
                schema: "smfr",
                columns: table => new
                {
                    id = table.Column<string>(type: "text", nullable: false),
                    name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    device_type = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    ip_address = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    mac_address = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    room_id = table.Column<string>(type: "text", nullable: true),
                    floor_id = table.Column<string>(type: "text", nullable: true),
                    building_id = table.Column<string>(type: "text", nullable: true),
                    createdat = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    createdby = table.Column<string>(type: "text", nullable: true),
                    updatedat = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    updatedby = table.Column<string>(type: "text", nullable: true),
                    isdeleted = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_devices", x => x.id);
                    table.ForeignKey(
                        name: "FK_devices_buildings_building_id",
                        column: x => x.building_id,
                        principalSchema: "smfr",
                        principalTable: "buildings",
                        principalColumn: "id");
                    table.ForeignKey(
                        name: "FK_devices_floors_floor_id",
                        column: x => x.floor_id,
                        principalSchema: "smfr",
                        principalTable: "floors",
                        principalColumn: "id");
                    table.ForeignKey(
                        name: "FK_devices_rooms_room_id",
                        column: x => x.room_id,
                        principalSchema: "smfr",
                        principalTable: "rooms",
                        principalColumn: "id");
                });

            migrationBuilder.CreateIndex(
                name: "IX_devices_building_id",
                schema: "smfr",
                table: "devices",
                column: "building_id");

            migrationBuilder.CreateIndex(
                name: "IX_devices_floor_id",
                schema: "smfr",
                table: "devices",
                column: "floor_id");

            migrationBuilder.CreateIndex(
                name: "IX_devices_room_id",
                schema: "smfr",
                table: "devices",
                column: "room_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "devices",
                schema: "smfr");
        }
    }
}
