using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace funeralv2Api.Migrations
{
    /// <inheritdoc />
    public partial class AddMultimediaAndDeviceLocation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // 컬럼 이름 충돌 방지를 위해 기존 컬럼이 이미 물리 DB에 존재하면 임시 드롭합니다.
            migrationBuilder.Sql(@"
                ALTER TABLE smfr.device_attributes DROP COLUMN IF EXISTS music_id;
                ALTER TABLE smfr.device_attributes DROP COLUMN IF EXISTS video_id;
            ");

            migrationBuilder.AddColumn<string>(
                name: "music_id",
                schema: "smfr",
                table: "device_attributes",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "video_id",
                schema: "smfr",
                table: "device_attributes",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true);

            // 기존 room_id를 기반으로 building_id와 floor_id를 역추적하여 채우기 (마이그레이션 데이터 이전)
            migrationBuilder.Sql(@"
                UPDATE smfr.devices d
                SET 
                    floor_id = r.floor_id,
                    building_id = f.building_id
                FROM smfr.rooms r
                JOIN smfr.floors f ON r.floor_id = f.id
                WHERE d.room_id = r.id
                  AND (d.floor_id IS NULL OR d.building_id IS NULL);
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "music_id",
                schema: "smfr",
                table: "device_attributes");

            migrationBuilder.DropColumn(
                name: "video_id",
                schema: "smfr",
                table: "device_attributes");
        }
    }
}
