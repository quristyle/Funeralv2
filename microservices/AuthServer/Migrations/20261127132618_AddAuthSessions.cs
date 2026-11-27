using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AuthServer.Migrations
{
    /// <inheritdoc />
    public partial class AddAuthSessions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "auth_sessions",
                schema: "scom",
                columns: table => new
                {
                    id = table.Column<string>(type: "text", nullable: false),
                    account_id = table.Column<string>(type: "text", nullable: false, comment: "세션의 주인."),
                    expires_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, comment: "이 세션이 끝나는 시각(UTC). 갱신해도 늘지 않는다 — 로그인 한 번이 사는 최대 길이다."),
                    last_refreshed_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true, comment: "마지막으로 갱신한 시각(UTC)."),
                    revoked_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true, comment: "닫힌 시각(UTC). 비어 있으면 살아 있다."),
                    revoke_reason = table.Column<string>(type: "text", nullable: true, comment: "닫힌 까닭(LOGOUT · PASSWORD_CHANGED · PASSWORD_RESET · ADMIN_PASSWORD_RESET)."),
                    ip = table.Column<string>(type: "text", nullable: true, comment: "로그인한 IP."),
                    user_agent = table.Column<string>(type: "text", nullable: true, comment: "로그인한 브라우저·기기."),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<string>(type: "text", nullable: true),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<string>(type: "text", nullable: true),
                    is_deleted = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_auth_sessions", x => x.id);
                    table.ForeignKey(
                        name: "FK_auth_sessions_accounts_account_id",
                        column: x => x.account_id,
                        principalSchema: "scom",
                        principalTable: "accounts",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                },
                comment: "로그인 한 번이 만든 세션. 접근·갱신 토큰이 모두 SessionId 클레임으로 이 줄을 가리킨다.");

            migrationBuilder.CreateIndex(
                name: "IX_auth_sessions_account_id",
                schema: "scom",
                table: "auth_sessions",
                column: "account_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "auth_sessions",
                schema: "scom");
        }
    }
}
