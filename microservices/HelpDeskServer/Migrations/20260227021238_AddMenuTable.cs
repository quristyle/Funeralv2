using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace HelpDeskServer.Migrations
{
    /// <inheritdoc />
    public partial class AddMenuTable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "menu",
                schema: "helpdesk",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    label = table.Column<string>(type: "text", nullable: false, comment: "메뉴명"),
                    icon = table.Column<string>(type: "text", nullable: true, comment: "아이콘 (PrimeIcons)"),
                    to = table.Column<string>(type: "text", nullable: true, comment: "이동 경로 (내부 라우터)"),
                    url = table.Column<string>(type: "text", nullable: true, comment: "외부 URL"),
                    parentid = table.Column<int>(type: "integer", nullable: true, comment: "부모 메뉴 ID"),
                    sortorder = table.Column<int>(type: "integer", nullable: false, comment: "정렬 순서"),
                    logintype = table.Column<string>(type: "text", nullable: false, comment: "표시 대상 (admin, customer, all)"),
                    isactive = table.Column<bool>(type: "boolean", nullable: false, comment: "활성화 여부"),
                    createdby = table.Column<string>(type: "text", nullable: false),
                    createdat = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    modifiedby = table.Column<string>(type: "text", nullable: true),
                    modifiedat = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    actionservice = table.Column<string>(type: "text", nullable: true),
                    menucontext = table.Column<string>(type: "text", nullable: true),
                    remoteaddr = table.Column<string>(type: "text", nullable: true),
                    remotemchineinfo = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_menu", x => x.id);
                    table.ForeignKey(
                        name: "FK_menu_menu_parentid",
                        column: x => x.parentid,
                        principalSchema: "helpdesk",
                        principalTable: "menu",
                        principalColumn: "id");
                },
                comment: "메뉴 관리 엔티티");

            migrationBuilder.CreateIndex(
                name: "IX_menu_parentid",
                schema: "helpdesk",
                table: "menu",
                column: "parentid");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "menu",
                schema: "helpdesk");
        }
    }
}
