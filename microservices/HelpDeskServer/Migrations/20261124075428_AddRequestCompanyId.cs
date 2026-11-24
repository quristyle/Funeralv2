using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HelpDeskServer.Migrations
{
    /// <inheritdoc />
    public partial class AddRequestCompanyId : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "companyid",
                schema: "helpdesk",
                table: "improvementrequest",
                type: "text",
                nullable: true,
                comment: "고객사 — 글을 쓴 사람이 쓸 때 속해 있던 포털 회사 아이디(scom.companies.id). 회사 걸름은 이 칸을 본다.\n            고객 줄의 회사는 조직도를 따라 바뀌므로 그것을 빌려 쓰면 옮겨 간 사람의 옛 글이 새 회사로 함께 넘어간다.");

            // 지금까지의 글은 쓴 사람의 고객 줄 회사를 빌려 보여 왔다. 보이던 그대로 박아 둔다.
            migrationBuilder.Sql(
                "UPDATE helpdesk.improvementrequest r SET companyid = c.companyid " +
                "FROM helpdesk.customer c WHERE c.id = r.customerid AND r.companyid IS NULL;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "companyid",
                schema: "helpdesk",
                table: "improvementrequest");
        }
    }
}
