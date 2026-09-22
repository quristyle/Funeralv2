using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace HelpDeskServer.Migrations
{
    /// <summary>
    /// 체크리스트(<c>/helpdesk/system/checklist</c>) 화면을 걷어내면서 표도 같이 지운다.
    /// 화면·엔드포인트·모델을 전부 없앴으므로 이 표를 읽거나 쓰는 코드가 남아 있지 않다.
    /// </summary>
    public partial class RemoveChecklist : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "checklist",
                schema: "helpdesk");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "checklist",
                schema: "helpdesk",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    actionservice = table.Column<string>(type: "text", nullable: true),
                    category = table.Column<string>(type: "text", nullable: false, comment: "분류 (예: Network, Server, Application)"),
                    completedat = table.Column<DateTime>(type: "timestamp with time zone", nullable: true, comment: "완료 일시"),
                    createdat = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    createdby = table.Column<string>(type: "text", nullable: false),
                    ischecked = table.Column<bool>(type: "boolean", nullable: false, comment: "점검 여부"),
                    itemname = table.Column<string>(type: "text", nullable: false, comment: "점검 항목 명"),
                    menucontext = table.Column<string>(type: "text", nullable: true),
                    modifiedat = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    modifiedby = table.Column<string>(type: "text", nullable: true),
                    note = table.Column<string>(type: "text", nullable: true, comment: "비고"),
                    remoteaddr = table.Column<string>(type: "text", nullable: true),
                    remotemchineinfo = table.Column<string>(type: "text", nullable: true),
                    sortorder = table.Column<int>(type: "integer", nullable: false, comment: "정렬 순서")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_checklist", x => x.id);
                },
                comment: "시스템 운영전환 체크리스트");
        }
    }
}
