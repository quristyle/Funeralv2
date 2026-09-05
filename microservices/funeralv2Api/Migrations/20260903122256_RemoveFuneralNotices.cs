using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace funeralv2Api.Migrations
{
    /// <inheritdoc />
    public partial class RemoveFuneralNotices : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "funeral_notice_reads",
                schema: "smfr");

            migrationBuilder.DropTable(
                name: "funeral_notices",
                schema: "smfr");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "funeral_notice_reads",
                schema: "smfr",
                columns: table => new
                {
                    id = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    is_deleted = table.Column<bool>(type: "boolean", nullable: false),
                    notice_id = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false, comment: "읽은 알림"),
                    read_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, comment: "읽은 시각"),
                    user_id = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false, comment: "읽은 사람")
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
                    building_id = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true, comment: "대상 건물. 비어 있으면 건물을 가리지 않는다."),
                    content = table.Column<string>(type: "text", nullable: true, comment: "알림 본문"),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    end_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true, comment: "게시 종료 일시. 비어 있으면 계속 보인다."),
                    is_deleted = table.Column<bool>(type: "boolean", nullable: false),
                    is_important = table.Column<bool>(type: "boolean", nullable: false, comment: "중요 표시 여부. 목록 맨 위에 붙는다."),
                    notice_type = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false, comment: "알림 구분 (NOTICE 공지 · ALERT 경고 · SYSTEM 시스템)"),
                    start_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true, comment: "게시 시작 일시. 비어 있으면 만들자마자 보인다."),
                    target_page = table.Column<string>(type: "text", nullable: true, comment: "눌렀을 때 갈 화면 경로 (옛 target_page)"),
                    target_param = table.Column<string>(type: "text", nullable: true, comment: "화면에 넘길 값 (옛 target_param)"),
                    target_user_id = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true, comment: "받는 사람. 비어 있으면 전체 공지다 (옛 n_nofi_user)."),
                    title = table.Column<string>(type: "text", nullable: false, comment: "알림 제목"),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_funeral_notices", x => x.id);
                },
                comment: "장례식장 알림 정보 엔티티.");

            migrationBuilder.CreateIndex(
                name: "IX_funeral_notice_reads_notice_id_user_id",
                schema: "smfr",
                table: "funeral_notice_reads",
                columns: new[] { "notice_id", "user_id" },
                unique: true);
        }
    }
}
