using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AuthServer.Migrations
{
    /// <summary>
    /// 비밀번호 재설정 링크를 담을 표를 만든다.
    /// </summary>
    /// <remarks>
    /// <para>
    /// [생성된 것을 <b>손으로 잘라 냈다</b> — 그대로 두면 운영에서 죽는다]
    /// </para>
    ///
    /// <para>
    /// <c>dotnet ef migrations add</c> 가 만들어 준 원본에는 이 표 말고도
    /// <c>account_login_logs</c> · <c>account_preferences</c> ·
    /// <c>birthday_messages</c> · <c>release_runs</c> · <c>help_archives</c> …
    /// 와 <c>accounts</c> · <c>system_menus</c> 의 칸 여덟 개를 <b>새로 만드는</b>
    /// 코드가 함께 들어 있었다.
    /// </para>
    ///
    /// <para>
    /// 그것들은 <b>운영 DB 에 이미 있다.</b> 모델 스냅샷
    /// (<c>AppDbContextModelSnapshot</c>)이 실제 DB 보다 뒤처져 있어서 EF 가
    /// "없으니 만들어야 한다" 고 판단한 것뿐이다. 그대로 실행하면 첫 번째
    /// <c>CreateTable</c> 에서 <c>relation already exists</c> 로 멈춘다.
    /// </para>
    ///
    /// <para>
    /// 그래서 <b>정말로 새로 생기는 것만</b> 남겼다. 함께 갱신된 스냅샷은 그대로
    /// 둔다 — 이제 스냅샷이 실제 DB 와 맞으므로 <b>다음 마이그레이션부터는 이런
    /// 손질이 필요 없다.</b>
    /// </para>
    ///
    /// <para>
    /// 빈 DB 에서 처음부터 만들 수 없는 것은 이 마이그레이션 때문이 아니라
    /// 초기 스키마 생성분이 이미 유실되어 있기 때문이다. 그 상태는 이 변경으로
    /// 나빠지지도 나아지지도 않는다.
    /// </para>
    /// </remarks>
    public partial class AddPasswordResetTokens : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "password_reset_tokens",
                schema: "scom",
                columns: table => new
                {
                    id = table.Column<string>(type: "text", nullable: false),
                    account_id = table.Column<string>(type: "text", nullable: false, comment: "이 링크가 가리키는 계정."),
                    token_hash = table.Column<string>(type: "text", nullable: false, comment: "토큰 원문의 SHA-256 해시 (base64). 원문은 어디에도 없다."),
                    expires_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, comment: "만료 시각 (UTC)."),
                    used_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true, comment: "실제로 비밀번호를 바꾼 시각 (UTC). 아직 안 썼으면 null."),
                    request_ip = table.Column<string>(type: "text", nullable: true, comment: "요청이 들어온 아이피."),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<string>(type: "text", nullable: true),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<string>(type: "text", nullable: true),
                    is_deleted = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_password_reset_tokens", x => x.id);
                    table.ForeignKey(
                        name: "FK_password_reset_tokens_accounts_account_id",
                        column: x => x.account_id,
                        principalSchema: "scom",
                        principalTable: "accounts",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                },
                comment: "비밀번호 재설정 링크. 토큰 해시만 들어 있다.");

            migrationBuilder.CreateIndex(
                name: "IX_password_reset_tokens_account_id",
                schema: "scom",
                table: "password_reset_tokens",
                column: "account_id");

            // 링크를 누를 때마다 해시로 찾는다. 색인이 없으면 표를 통째로 훑고,
            // 이 표는 쓴 것도 지우지 않으므로 계속 자란다.
            migrationBuilder.CreateIndex(
                name: "IX_password_reset_tokens_token_hash",
                schema: "scom",
                table: "password_reset_tokens",
                column: "token_hash",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "password_reset_tokens",
                schema: "scom");
        }
    }
}
