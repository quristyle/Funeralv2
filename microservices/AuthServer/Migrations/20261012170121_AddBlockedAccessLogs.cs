using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace AuthServer.Migrations
{
    /// <inheritdoc />
    public partial class AddBlockedAccessLogs : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "blocked_access_logs",
                schema: "scom",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false, comment: "줄 번호")
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    occurred_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, comment: "막힌 때 (UTC)"),
                    user_id = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false, comment: "막힌 사람의 로그인 아이디 (scom.accounts.user_id)"),
                    href = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: false, comment: "막힌 주소. 사람이 실제로 연 그 값이라 매개변수가 들어 있다\n            (/admin/system/account/jupark/app) — 「누구의 것을 보려 했나」가\n            이 칸에만 남는다."),
                    route_key = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true, comment: "화면이 선언한 열쇠. 신고가 없는 화면은 비어 있다"),
                    screen_type = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true, comment: "라우터가 끼우려던 화면 클래스. 「어느 파일이냐」의 답이다"),
                    menu_path = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true, comment: "권한을 따진 메뉴 경로. 메뉴를 못 찾았으면 비어 있다"),
                    reason = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false, comment: "왜 막혔나 — NO_PERMISSION(권한 없음) · UNMANAGED(관리 밖).\n            둘을 가르는 것이 이 표의 요점이다: 앞엣것은 권한 화면에서 체크\n            하나면 끝나고, 뒤엣것은 메뉴를 만들거나 코드를 고치기 전에는 안 열린다."),
                    from_path = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true, comment: "직전에 보던 화면. 「어디서 눌러 들어왔나」다"),
                    client_ip = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true, comment: "접속 주소. 위조할 수 있는 값이라 보여 주기 전용이다 —\n            이것으로 무엇을 판단하지 않는다(ClientAddress 머리말).")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_blocked_access_logs", x => x.id);
                },
                comment: "차단 한 건 — 권한 밖의 화면을 누가 어떤 주소로 열려 했나.");

            migrationBuilder.CreateIndex(
                name: "IX_blocked_access_logs_occurred_at",
                schema: "scom",
                table: "blocked_access_logs",
                column: "occurred_at");

            migrationBuilder.CreateIndex(
                name: "IX_blocked_access_logs_route_key",
                schema: "scom",
                table: "blocked_access_logs",
                column: "route_key");

            migrationBuilder.CreateIndex(
                name: "IX_blocked_access_logs_user_id_occurred_at",
                schema: "scom",
                table: "blocked_access_logs",
                columns: new[] { "user_id", "occurred_at" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "blocked_access_logs",
                schema: "scom");
        }
    }
}
