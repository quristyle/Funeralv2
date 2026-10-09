using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace AuthServer.Migrations
{
    /// <inheritdoc />
    public partial class AddMenuUsageLogs : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "menu_usage_logs",
                schema: "scom",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false, comment: "줄 번호")
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    occurred_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, comment: "본 때 (UTC)"),
                    user_id = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false, comment: "본 사람의 로그인 아이디 (scom.accounts.user_id)"),
                    menu_id = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true, comment: "본 메뉴의 식별자 (scom.system_menus.id). 못 찾으면 비어 있다"),
                    route_key = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true, comment: "화면이 선언한 열쇠 (예: admin.status.menu-usage)"),
                    menu_path = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: false, comment: "메뉴 경로. 권한표와 같은 열쇠이고 조회는 이것으로 묶는다"),
                    menu_title = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true, comment: "그때 화면에 적혀 있던 메뉴 제목"),
                    href = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true, comment: "실제로 열린 주소. 메뉴 경로와 다를 수 있다"),
                    from_path = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true, comment: "직전에 보던 화면의 메뉴 경로. 첫 화면이면 비어 있다")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_menu_usage_logs", x => x.id);
                },
                comment: "메뉴 열람 한 건 — 누가 어떤 화면을 언제 보았나.");

            migrationBuilder.CreateIndex(
                name: "IX_menu_usage_logs_menu_path",
                schema: "scom",
                table: "menu_usage_logs",
                column: "menu_path");

            migrationBuilder.CreateIndex(
                name: "IX_menu_usage_logs_occurred_at",
                schema: "scom",
                table: "menu_usage_logs",
                column: "occurred_at");

            migrationBuilder.CreateIndex(
                name: "IX_menu_usage_logs_user_id_occurred_at",
                schema: "scom",
                table: "menu_usage_logs",
                columns: new[] { "user_id", "occurred_at" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "menu_usage_logs",
                schema: "scom");
        }
    }
}
