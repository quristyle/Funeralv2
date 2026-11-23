using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace AuthServer.Migrations
{
    /// <summary>회사 사용처의 처리 담당자 표.</summary>
    /// 스냅숏이 운영보다 뒤처져 있어 EF 가 다른 표(release_runs 지우기 · layout_header_icons · server_ping_targets 만들기)와
    /// 설명 바꾸기를 함께 뽑았다. 운영에는 그것들이 이미 그 모양이라 이 표 하나만 남겼다 — 스냅숏은 그대로 맞춰 두었다.
    public partial class AddCompanyUsageManagers : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "company_usage_managers",
                schema: "scom",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    company_id = table.Column<string>(type: "text", nullable: false, comment: "회사 식별자 (scom.companies.id)"),
                    code_value = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false, comment: "사용처 코드값 (COMPANY_USAGE_LOCATION 그룹의 code_value)"),
                    account_id = table.Column<string>(type: "text", nullable: false, comment: "담당자 계정 식별자 (scom.accounts.id)"),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<string>(type: "text", nullable: true),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<string>(type: "text", nullable: true),
                    is_deleted = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_company_usage_managers", x => x.id);
                    table.ForeignKey(
                        name: "FK_company_usage_managers_accounts_account_id",
                        column: x => x.account_id,
                        principalSchema: "scom",
                        principalTable: "accounts",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_company_usage_managers_companies_company_id",
                        column: x => x.company_id,
                        principalSchema: "scom",
                        principalTable: "companies",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                },
                comment: "회사 사용처의 처리 담당자 — 그 회사의 그 사용처(예: 헬프데스크) 일을 누가 맡는가.");

            migrationBuilder.CreateIndex(
                name: "IX_company_usage_managers_account_id",
                schema: "scom",
                table: "company_usage_managers",
                column: "account_id");

            migrationBuilder.CreateIndex(
                name: "IX_company_usage_managers_company_id_code_value_account_id",
                schema: "scom",
                table: "company_usage_managers",
                columns: new[] { "company_id", "code_value", "account_id" },
                unique: true);

        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "company_usage_managers",
                schema: "scom");
        }
    }
}
