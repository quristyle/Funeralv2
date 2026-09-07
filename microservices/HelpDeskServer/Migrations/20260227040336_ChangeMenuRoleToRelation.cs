using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace HelpDeskServer.Migrations
{
    /// <inheritdoc />
    public partial class ChangeMenuRoleToRelation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "logintype",
                schema: "jsini",
                table: "menu");

            migrationBuilder.CreateTable(
                name: "menurole",
                schema: "jsini",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    menuid = table.Column<int>(type: "integer", nullable: false, comment: "메뉴 ID"),
                    rolename = table.Column<string>(type: "text", nullable: false, comment: "권한명 (admin, customer 등)"),
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
                    table.PrimaryKey("PK_menurole", x => x.id);
                    table.ForeignKey(
                        name: "FK_menurole_menu_menuid",
                        column: x => x.menuid,
                        principalSchema: "jsini",
                        principalTable: "menu",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                },
                comment: "메뉴별 권한 매핑 엔티티");

            migrationBuilder.CreateIndex(
                name: "IX_menurole_menuid",
                schema: "jsini",
                table: "menurole",
                column: "menuid");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "menurole",
                schema: "jsini");

            migrationBuilder.AddColumn<string>(
                name: "logintype",
                schema: "jsini",
                table: "menu",
                type: "text",
                nullable: false,
                defaultValue: "",
                comment: "표시 대상 (admin, customer, all)");
        }
    }
}
