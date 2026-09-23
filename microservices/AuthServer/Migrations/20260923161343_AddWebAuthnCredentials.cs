using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AuthServer.Migrations
{
    /// <inheritdoc />
    public partial class AddWebAuthnCredentials : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "account_webauthn_credentials",
                schema: "scom",
                columns: table => new
                {
                    id = table.Column<string>(type: "text", nullable: false),
                    account_id = table.Column<string>(type: "text", nullable: false, comment: "이 패스키의 주인."),
                    credential_id = table.Column<string>(type: "text", nullable: false, comment: "인증기가 지어 준 자격 증명 아이디 (base64url).\n            로그인할 때 브라우저가 이 값을 들고 오므로 여기로 찾는다."),
                    public_key = table.Column<string>(type: "text", nullable: false, comment: "공개 키. SubjectPublicKeyInfo DER 을 base64 로 담는다.\n            \n            COSE 형식 그대로가 아니라 SPKI 로 두는 이유는 .NET 이 그 형식을\n            바로 읽기 때문이다(ECDsa.ImportSubjectPublicKeyInfo). COSE 를\n            담으면 검증할 때마다 CBOR 을 풀어야 하고, 그 코드가 곧\n            서명 검증에서 가장 틀리기 쉬운 자리가 된다."),
                    algorithm = table.Column<int>(type: "integer", nullable: false, comment: "서명 알고리즘 (COSE 식별자). -7 = ES256, -257 = RS256.\n            검증할 때 어느 열쇠 형식으로 읽을지를 이 값이 정한다."),
                    sign_count = table.Column<long>(type: "bigint", nullable: false, comment: "서명 횟수. 머리말의 복제 감지에 쓴다."),
                    label = table.Column<string>(type: "text", nullable: true, comment: "사용자가 알아볼 이름. 「아이폰 지문」처럼 사람이 붙인다.\n            기기가 여럿이면 어느 줄을 지워야 하는지를 이 이름으로만 알 수 있다."),
                    attachment = table.Column<string>(type: "text", nullable: true, comment: "인증기 종류 — platform(기기에 붙박이: 지문·얼굴) ·\n            cross-platform(따로 꽂는 보안 열쇠)."),
                    last_used_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true, comment: "마지막으로 이 패스키로 들어온 시각 (UTC). 한 번도 안 썼으면 null."),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<string>(type: "text", nullable: true),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<string>(type: "text", nullable: true),
                    is_deleted = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_account_webauthn_credentials", x => x.id);
                    table.ForeignKey(
                        name: "FK_account_webauthn_credentials_accounts_account_id",
                        column: x => x.account_id,
                        principalSchema: "scom",
                        principalTable: "accounts",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                },
                comment: "계정 하나에 등록된 패스키(WebAuthn 인증기) 한 개.\n             휴대폰 지문·얼굴, 윈도우 Hello, 보안 열쇠가 모두 여기 한 줄로 남는다.");

            migrationBuilder.CreateIndex(
                name: "IX_account_webauthn_credentials_account_id",
                schema: "scom",
                table: "account_webauthn_credentials",
                column: "account_id");

            migrationBuilder.CreateIndex(
                name: "IX_account_webauthn_credentials_credential_id",
                schema: "scom",
                table: "account_webauthn_credentials",
                column: "credential_id",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "account_webauthn_credentials",
                schema: "scom");
        }
    }
}
