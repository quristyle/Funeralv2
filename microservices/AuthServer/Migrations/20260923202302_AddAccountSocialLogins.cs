using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AuthServer.Migrations
{
    /// <inheritdoc />
    public partial class AddAccountSocialLogins : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "account_social_logins",
                schema: "scom",
                columns: table => new
                {
                    id = table.Column<string>(type: "text", nullable: false),
                    account_id = table.Column<string>(type: "text", nullable: false, comment: "이 소셜 연결의 주인."),
                    provider = table.Column<string>(type: "text", nullable: false, comment: "공급자 열쇠 — google · naver · kakao.\n            설정(Auth:Social:Providers)의 칸 이름과 같아야 한다."),
                    provider_user_id = table.Column<string>(type: "text", nullable: false, comment: "공급자가 지어 준 사용자 번호. 로그인은 이 값으로 주인을 찾는다\n            (머리말 참고). 공급자 안에서만 고유하므로  와 짝으로 쓴다."),
                    email = table.Column<string>(type: "text", nullable: true, comment: "공급자가 알려 준 이메일. 참고용이다 — 이 값으로 주인을 찾지 않는다.\n            공급자가 안 주면 비어 있다."),
                    display_name = table.Column<string>(type: "text", nullable: true, comment: "공급자가 알려 준 이름·별명. 「연결된 소셜 계정」 목록에서 어느 줄을\n            떼어야 하는지를 이 값으로 알아본다."),
                    last_login_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true, comment: "마지막으로 이 소셜 계정으로 들어온 시각 (UTC). 한 번도 안 썼으면 null."),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<string>(type: "text", nullable: true),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<string>(type: "text", nullable: true),
                    is_deleted = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_account_social_logins", x => x.id);
                    table.ForeignKey(
                        name: "FK_account_social_logins_accounts_account_id",
                        column: x => x.account_id,
                        principalSchema: "scom",
                        principalTable: "accounts",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                },
                comment: "계정 하나에 연결된 소셜 계정 한 개.\n             구글 · 네이버 · 카카오가 모두 여기 한 줄로 남는다.");

            migrationBuilder.CreateIndex(
                name: "IX_account_social_logins_account_id",
                schema: "scom",
                table: "account_social_logins",
                column: "account_id");

            migrationBuilder.CreateIndex(
                name: "IX_account_social_logins_provider_provider_user_id",
                schema: "scom",
                table: "account_social_logins",
                columns: new[] { "provider", "provider_user_id" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "account_social_logins",
                schema: "scom");
        }
    }
}
