using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace HelpDeskServer.Migrations
{
    /// <inheritdoc />
    public partial class DropHelpdeskOwnedCompany : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_customer_customercompany_companyid",
                schema: "helpdesk",
                table: "customer");

            migrationBuilder.DropForeignKey(
                name: "FK_teamcompanies_customercompany_companyid",
                schema: "helpdesk",
                table: "teamcompanies");

            migrationBuilder.DropForeignKey(
                name: "FK_teamcompanies_customercompany_customercompanyid",
                schema: "helpdesk",
                table: "teamcompanies");

            migrationBuilder.DropForeignKey(
                name: "FK_wbs_customercompany_customercompanyid",
                schema: "helpdesk",
                table: "wbs");

            migrationBuilder.DropTable(
                name: "customercompany",
                schema: "helpdesk");

            migrationBuilder.DropIndex(
                name: "IX_wbs_customercompanyid",
                schema: "helpdesk",
                table: "wbs");

            migrationBuilder.DropIndex(
                name: "IX_teamcompanies_companyid",
                schema: "helpdesk",
                table: "teamcompanies");

            migrationBuilder.DropIndex(
                name: "IX_teamcompanies_customercompanyid",
                schema: "helpdesk",
                table: "teamcompanies");

            migrationBuilder.DropIndex(
                name: "IX_customer_companyid",
                schema: "helpdesk",
                table: "customer");

            migrationBuilder.DropColumn(
                name: "customercompanyid",
                schema: "helpdesk",
                table: "teamcompanies");

            // 회사 칸을 정수(헬프데스크 제 회사 표의 키)에서 **글자**(포털
            // `scom.companies.id`)로 바꾼다.
            //
            // EF 가 만들어 주는 AlterColumn 은 `ALTER COLUMN ... TYPE text` 만
            // 내보내는데, PostgreSQL 은 integer 를 text 로 **저절로 바꾸지 않는다**
            // (`column cannot be cast automatically`). 그래서 USING 을 직접 적는다.
            //
            // 값은 **옮기지 않고 비운다.** 옛 정수 아이디를 포털 아이디로 바꾸는
            // 대조표는 포털 DB 에만 있어(`scom.companies.remark` 의
            // `helpdesk:company:<원본ID>`) 여기서는 읽을 수 없고, 숫자를 글자로
            // 그대로 옮겨 봐야 어느 회사도 가리키지 못한다. 고객의 회사는
            // **그 사람의 포털 계정 소속 회사**로 다시 잡는다 —
            // `deploy/sql/helpdesk-company-from-portal-2026-09-27.sql` 참고.
            migrationBuilder.Sql("""
                ALTER TABLE helpdesk.customer ALTER COLUMN companyid DROP NOT NULL;
                ALTER TABLE helpdesk.customer ALTER COLUMN companyid TYPE text USING NULL;
                COMMENT ON COLUMN helpdesk.customer.companyid IS '소속 회사 식별자 — 포털(scom.companies.id)의 값이다.';

                ALTER TABLE helpdesk.schedules ALTER COLUMN companyid TYPE text USING NULL;
                COMMENT ON COLUMN helpdesk.schedules.companyid IS '특정 회사 식별자 (IsCommon 이 false 일 때 사용). 포털(scom.companies.id)의 값이다.';

                ALTER TABLE helpdesk.wbs ALTER COLUMN customercompanyid TYPE text USING NULL;
                COMMENT ON COLUMN helpdesk.wbs.customercompanyid IS '관련 고객사 식별자 — 포털(scom.companies.id)의 값이다.';
                """);

            // 팀-회사 매핑의 회사 칸은 **기본키의 일부**라 비울 수 없다.
            // 줄이 사라지지 않도록 글자로 그대로 옮긴다.
            migrationBuilder.Sql("""
                ALTER TABLE helpdesk.teamcompanies ALTER COLUMN companyid TYPE text USING companyid::text;
                COMMENT ON COLUMN helpdesk.teamcompanies.companyid IS '고객사 식별자 — 포털(scom.companies.id)의 값이다. 헬프데스크에는 회사 표가 없으므로 탐색 속성도 없다.';
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // 되돌릴 때도 형 변환은 손으로 적는다(text → integer 역시 저절로 안 된다).
            // 포털 아이디는 정수가 아니므로 값은 되살아나지 않는다.
            migrationBuilder.Sql("""
                ALTER TABLE helpdesk.wbs ALTER COLUMN customercompanyid TYPE integer USING NULL;
                ALTER TABLE helpdesk.teamcompanies ALTER COLUMN companyid TYPE integer USING 0;
                """);

            migrationBuilder.AddColumn<int>(
                name: "customercompanyid",
                schema: "helpdesk",
                table: "teamcompanies",
                type: "integer",
                nullable: true);

            migrationBuilder.Sql("""
                ALTER TABLE helpdesk.schedules ALTER COLUMN companyid TYPE integer USING NULL;
                ALTER TABLE helpdesk.customer ALTER COLUMN companyid TYPE integer USING 0;
                ALTER TABLE helpdesk.customer ALTER COLUMN companyid SET NOT NULL;
                """);

            migrationBuilder.CreateTable(
                name: "customercompany",
                schema: "helpdesk",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    actionservice = table.Column<string>(type: "text", nullable: true),
                    createdat = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    createdby = table.Column<string>(type: "text", nullable: false),
                    menucontext = table.Column<string>(type: "text", nullable: true),
                    modifiedat = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    modifiedby = table.Column<string>(type: "text", nullable: true),
                    name = table.Column<string>(type: "text", nullable: false, comment: "고객사 명"),
                    remoteaddr = table.Column<string>(type: "text", nullable: true),
                    remotemchineinfo = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_customercompany", x => x.id);
                },
                comment: "고객사");

            migrationBuilder.CreateIndex(
                name: "IX_wbs_customercompanyid",
                schema: "helpdesk",
                table: "wbs",
                column: "customercompanyid");

            migrationBuilder.CreateIndex(
                name: "IX_teamcompanies_companyid",
                schema: "helpdesk",
                table: "teamcompanies",
                column: "companyid");

            migrationBuilder.CreateIndex(
                name: "IX_teamcompanies_customercompanyid",
                schema: "helpdesk",
                table: "teamcompanies",
                column: "customercompanyid");

            migrationBuilder.CreateIndex(
                name: "IX_customer_companyid",
                schema: "helpdesk",
                table: "customer",
                column: "companyid");

            migrationBuilder.AddForeignKey(
                name: "FK_customer_customercompany_companyid",
                schema: "helpdesk",
                table: "customer",
                column: "companyid",
                principalSchema: "helpdesk",
                principalTable: "customercompany",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_teamcompanies_customercompany_companyid",
                schema: "helpdesk",
                table: "teamcompanies",
                column: "companyid",
                principalSchema: "helpdesk",
                principalTable: "customercompany",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_teamcompanies_customercompany_customercompanyid",
                schema: "helpdesk",
                table: "teamcompanies",
                column: "customercompanyid",
                principalSchema: "helpdesk",
                principalTable: "customercompany",
                principalColumn: "id");

            migrationBuilder.AddForeignKey(
                name: "FK_wbs_customercompany_customercompanyid",
                schema: "helpdesk",
                table: "wbs",
                column: "customercompanyid",
                principalSchema: "helpdesk",
                principalTable: "customercompany",
                principalColumn: "id");
        }
    }
}
