using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AuthServer.Migrations
{
    /// <inheritdoc />
    public partial class AddPortalErrorLogs : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "portal_error_logs",
                schema: "scom",
                columns: table => new
                {
                    id = table.Column<string>(type: "text", nullable: false),
                    trace_id = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false, comment: "추적 번호의 가운데 32자리(W3C trace-id). 조회 열쇠다.\n             사용자는 번호를 통째로 읽어 주고, 받아 적는 사람은 중간만 옮겨 적기도 한다.\n             그래서 전체()와 가운데를 따로 들고, 조회는\n             가운데로 한다 — 어느 쪽을 쳐도 찾히게 하려면 기준이 하나여야 한다."),
                    traceparent = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true, comment: "화면에 적힌 번호 그대로. 사용자가 불러 준 값과 눈으로 맞춰 보는 자리다."),
                    occurred_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, comment: "오류가 난 때(UTC).  와 따로 두는 이유는\n            보고가 늦을 수 있기 때문이다 — 포털이 큐에 쌓아 두었다가 보내고,\n            게이트웨이가 죽어 있으면 되살아난 뒤에 보낸다."),
                    source = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false, comment: "어느 프론트인가 (portal · site). 지금은 포털뿐이다."),
                    path = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true, comment: "터진 화면 경로 (/funeral/room-status)."),
                    query_string = table.Column<string>(type: "character varying(1024)", maxLength: 1024, nullable: true, comment: "질의 문자열. 경로만으로는 재현이 안 되는 화면이 많다."),
                    method = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: true),
                    user_id = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true, comment: "로그인한 사람의 아이디. 로그인 전이면 비어 있다."),
                    ip = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true, comment: "접속 IP. 포털이 nginx·게이트웨이 뒤라 X-Forwarded-For 의 첫 값이다."),
                    user_agent = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true, comment: "브라우저·기기. 이 기능을 만든 까닭이 여기 있다 — 모바일에서만 나는\n            오류를 PC 로 재현하려다 놓치는 일이 있다."),
                    exception_type = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true, comment: "예외 타입 이름 (System.NullReferenceException). 거르기와 묶어 세기에 쓴다."),
                    message = table.Column<string>(type: "character varying(2048)", maxLength: 2048, nullable: true, comment: "예외 메시지 한 줄."),
                    detail = table.Column<string>(type: "text", nullable: true, comment: "스택 추적. 안쪽 예외(InnerException)까지 펼쳐서 담는다 —\n            바깥 메시지가 \"An error occurred while…\" 뿐이고 진짜 까닭은 안쪽에만\n            있는 경우가 흔하다."),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<string>(type: "text", nullable: true),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<string>(type: "text", nullable: true),
                    is_deleted = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_portal_error_logs", x => x.id);
                },
                comment: "포털 프론트가 잡은 미처리 예외 한 건. 오류 화면이 보여 준 추적 번호로 찾는다.");

            migrationBuilder.CreateIndex(
                name: "IX_portal_error_logs_occurred_at",
                schema: "scom",
                table: "portal_error_logs",
                column: "occurred_at");

            migrationBuilder.CreateIndex(
                name: "IX_portal_error_logs_trace_id",
                schema: "scom",
                table: "portal_error_logs",
                column: "trace_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "portal_error_logs",
                schema: "scom");
        }
    }
}
