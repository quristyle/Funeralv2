using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace funeralv2Api.Migrations
{
    /// <inheritdoc />
    public partial class device_attribute : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "device_attributes",
                schema: "smfr",
                columns: table => new
                {
                    id = table.Column<string>(type: "text", nullable: false),
                    device_id = table.Column<string>(type: "text", nullable: false),
                    display_orientation = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    content_interval_sec = table.Column<int>(type: "integer", nullable: false),
                    is_screensaver_enabled = table.Column<bool>(type: "boolean", nullable: false),
                    screensaver_timeout_sec = table.Column<int>(type: "integer", nullable: false),
                    is_memorial_photo_enabled = table.Column<bool>(type: "boolean", nullable: false),
                    memorial_photo_effect = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    is_deceased_name_visible = table.Column<bool>(type: "boolean", nullable: false),
                    is_family_contact_visible = table.Column<bool>(type: "boolean", nullable: false),
                    is_video_enabled = table.Column<bool>(type: "boolean", nullable: false),
                    is_music_enabled = table.Column<bool>(type: "boolean", nullable: false),
                    music_volume = table.Column<int>(type: "integer", nullable: true),
                    is_media_loop = table.Column<bool>(type: "boolean", nullable: false),
                    is_muted = table.Column<bool>(type: "boolean", nullable: false),
                    is_floor_guide_enabled = table.Column<bool>(type: "boolean", nullable: false),
                    is_room_assignment_visible = table.Column<bool>(type: "boolean", nullable: false),
                    is_active_rooms_only = table.Column<bool>(type: "boolean", nullable: false),
                    floor_guide_refresh_sec = table.Column<int>(type: "integer", nullable: false),
                    is_touch_enabled = table.Column<bool>(type: "boolean", nullable: false),
                    is_qr_code_visible = table.Column<bool>(type: "boolean", nullable: false),
                    is_building_map_visible = table.Column<bool>(type: "boolean", nullable: false),
                    entrance_greeting = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    is_notice_visible = table.Column<bool>(type: "boolean", nullable: false),
                    notice_scroll_speed = table.Column<int>(type: "integer", nullable: false),
                    remark = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    createdat = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    createdby = table.Column<string>(type: "text", nullable: true),
                    updatedat = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    updatedby = table.Column<string>(type: "text", nullable: true),
                    isdeleted = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_device_attributes", x => x.id);
                    table.ForeignKey(
                        name: "FK_device_attributes_devices_device_id",
                        column: x => x.device_id,
                        principalSchema: "smfr",
                        principalTable: "devices",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_device_attributes_device_id",
                schema: "smfr",
                table: "device_attributes",
                column: "device_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "device_attributes",
                schema: "smfr");
        }
    }
}
