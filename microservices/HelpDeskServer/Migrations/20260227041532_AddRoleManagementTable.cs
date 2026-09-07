using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace HelpDeskServer.Migrations
{
    /// <inheritdoc />
    public partial class AddRoleManagementTable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "approle",
                schema: "helpdesk",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    name = table.Column<string>(type: "text", nullable: false, comment: "그룹명 (영문 식별자 권장)"),
                    displayname = table.Column<string>(type: "text", nullable: false, comment: "표시용 명칭"),
                    description = table.Column<string>(type: "text", nullable: true, comment: "설명"),
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
                    table.PrimaryKey("PK_approle", x => x.id);
                },
                comment: "권한 그룹(역할) 엔티티");

            migrationBuilder.CreateTable(
                name: "appuserrole",
                schema: "helpdesk",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    roleid = table.Column<int>(type: "integer", nullable: false, comment: "권한 그룹 ID"),
                    usertype = table.Column<string>(type: "text", nullable: false, comment: "사용자 타입 (admin, customer)"),
                    userid = table.Column<int>(type: "integer", nullable: false, comment: "사용자 ID (AdminId 또는 CustomerId)"),
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
                    table.PrimaryKey("PK_appuserrole", x => x.id);
                    table.ForeignKey(
                        name: "FK_appuserrole_approle_roleid",
                        column: x => x.roleid,
                        principalSchema: "helpdesk",
                        principalTable: "approle",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                },
                comment: "사용자별 권한 그룹 매핑 엔티티");

            migrationBuilder.CreateIndex(
                name: "IX_appuserrole_roleid",
                schema: "helpdesk",
                table: "appuserrole",
                column: "roleid");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "appuserrole",
                schema: "helpdesk");

            migrationBuilder.DropTable(
                name: "approle",
                schema: "helpdesk");
        }
    }
}
