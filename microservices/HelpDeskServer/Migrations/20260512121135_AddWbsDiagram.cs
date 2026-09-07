using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace HelpDeskServer.Migrations
{
    /// <inheritdoc />
    public partial class AddWbsDiagram : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "wbsdiagram",
                schema: "jsini",
                columns: table => new
                {
                    wbsdiagramrid = table.Column<int>(type: "integer", nullable: false, comment: "다이어그램 고유 식별자")
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    wbsrid = table.Column<int>(type: "integer", nullable: false, comment: "관련 WBS 항목 ID"),
                    diagramdata = table.Column<string>(type: "text", nullable: true, comment: "다이어그램 데이터 (XML 또는 JSON 문자열)"),
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
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
                    table.PrimaryKey("PK_wbsdiagram", x => x.wbsdiagramrid);
                    table.ForeignKey(
                        name: "FK_wbsdiagram_wbs_wbsrid",
                        column: x => x.wbsrid,
                        principalSchema: "jsini",
                        principalTable: "wbs",
                        principalColumn: "wbsrid",
                        onDelete: ReferentialAction.Cascade);
                },
                comment: "WBS 항목별 다이어그램 데이터를 저장하는 엔티티");

            migrationBuilder.CreateIndex(
                name: "IX_wbsdiagram_wbsrid",
                schema: "jsini",
                table: "wbsdiagram",
                column: "wbsrid");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "wbsdiagram",
                schema: "jsini");
        }
    }
}
