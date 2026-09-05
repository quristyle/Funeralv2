using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace funeralv2Api.Migrations
{
    /// <inheritdoc />
    public partial class AddOldFuneralMigrationTables : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "account_settings",
                schema: "smfr",
                columns: table => new
                {
                    id = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    user_id = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false, comment: "설정 주인 (게이트웨이가 붙여 준 X-User-Id)"),
                    setting_code = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false, comment: "설정 코드 (옛 conf_cd)"),
                    setting_value = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true, comment: "설정 값. 켬/끔은 \"Y\"/\"N\" 으로 적는다 — 옛 표기를 그대로 쓴다."),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    is_deleted = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_account_settings", x => x.id);
                },
                comment: "계정별 장례식장 업무 설정 한 줄.");

            migrationBuilder.CreateTable(
                name: "building_music",
                schema: "smfr",
                columns: table => new
                {
                    id = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    building_id = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false, comment: "대상 건물"),
                    media_source_id = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false, comment: "배정한 음원 (smfr.media_sources 의 AUDIO 행)"),
                    sort_order = table.Column<int>(type: "integer", nullable: false, comment: "건물 안에서의 재생 순서"),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    is_deleted = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_building_music", x => x.id);
                },
                comment: "건물에 배정한 음원.");

            migrationBuilder.CreateTable(
                name: "funeral_notice_reads",
                schema: "smfr",
                columns: table => new
                {
                    id = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    notice_id = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false, comment: "읽은 알림"),
                    user_id = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false, comment: "읽은 사람"),
                    read_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, comment: "읽은 시각"),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    is_deleted = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_funeral_notice_reads", x => x.id);
                },
                comment: "알림을 누가 언제 읽었는지.");

            migrationBuilder.CreateTable(
                name: "funeral_notices",
                schema: "smfr",
                columns: table => new
                {
                    id = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    title = table.Column<string>(type: "text", nullable: false, comment: "알림 제목"),
                    content = table.Column<string>(type: "text", nullable: true, comment: "알림 본문"),
                    notice_type = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false, comment: "알림 구분 (NOTICE 공지 · ALERT 경고 · SYSTEM 시스템)"),
                    is_important = table.Column<bool>(type: "boolean", nullable: false, comment: "중요 표시 여부. 목록 맨 위에 붙는다."),
                    target_user_id = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true, comment: "받는 사람. 비어 있으면 전체 공지다 (옛 n_nofi_user)."),
                    building_id = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true, comment: "대상 건물. 비어 있으면 건물을 가리지 않는다."),
                    target_page = table.Column<string>(type: "text", nullable: true, comment: "눌렀을 때 갈 화면 경로 (옛 target_page)"),
                    target_param = table.Column<string>(type: "text", nullable: true, comment: "화면에 넘길 값 (옛 target_param)"),
                    start_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true, comment: "게시 시작 일시. 비어 있으면 만들자마자 보인다."),
                    end_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true, comment: "게시 종료 일시. 비어 있으면 계속 보인다."),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    is_deleted = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_funeral_notices", x => x.id);
                },
                comment: "장례식장 알림 정보 엔티티.");

            migrationBuilder.CreateIndex(
                name: "IX_account_settings_user_id_setting_code",
                schema: "smfr",
                table: "account_settings",
                columns: new[] { "user_id", "setting_code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_building_music_building_id_media_source_id",
                schema: "smfr",
                table: "building_music",
                columns: new[] { "building_id", "media_source_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_funeral_notice_reads_notice_id_user_id",
                schema: "smfr",
                table: "funeral_notice_reads",
                columns: new[] { "notice_id", "user_id" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "account_settings",
                schema: "smfr");

            migrationBuilder.DropTable(
                name: "building_music",
                schema: "smfr");

            migrationBuilder.DropTable(
                name: "funeral_notice_reads",
                schema: "smfr");

            migrationBuilder.DropTable(
                name: "funeral_notices",
                schema: "smfr");
        }
    }
}
