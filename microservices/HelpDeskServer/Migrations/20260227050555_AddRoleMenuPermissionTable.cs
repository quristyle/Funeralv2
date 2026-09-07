using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace HelpDeskServer.Migrations
{
    /// <inheritdoc />
    public partial class AddRoleMenuPermissionTable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "rolemenupermission",
                schema: "jsini",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    roleid = table.Column<int>(type: "integer", nullable: false),
                    menuid = table.Column<int>(type: "integer", nullable: false),
                    cancreate = table.Column<bool>(type: "boolean", nullable: false),
                    canread = table.Column<bool>(type: "boolean", nullable: false),
                    canupdate = table.Column<bool>(type: "boolean", nullable: false),
                    candelete = table.Column<bool>(type: "boolean", nullable: false),
                    ext1 = table.Column<bool>(type: "boolean", nullable: false),
                    ext2 = table.Column<bool>(type: "boolean", nullable: false),
                    ext3 = table.Column<bool>(type: "boolean", nullable: false),
                    ext4 = table.Column<bool>(type: "boolean", nullable: false),
                    ext5 = table.Column<bool>(type: "boolean", nullable: false),
                    ext6 = table.Column<bool>(type: "boolean", nullable: false),
                    ext7 = table.Column<bool>(type: "boolean", nullable: false),
                    ext8 = table.Column<bool>(type: "boolean", nullable: false),
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
                    table.PrimaryKey("PK_rolemenupermission", x => x.id);
                    table.ForeignKey(
                        name: "FK_rolemenupermission_approle_roleid",
                        column: x => x.roleid,
                        principalSchema: "jsini",
                        principalTable: "approle",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_rolemenupermission_menu_menuid",
                        column: x => x.menuid,
                        principalSchema: "jsini",
                        principalTable: "menu",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                },
                comment: "역할별 메뉴 상세 권한 엔티티");

            migrationBuilder.CreateIndex(
                name: "IX_rolemenupermission_menuid",
                schema: "jsini",
                table: "rolemenupermission",
                column: "menuid");

            migrationBuilder.CreateIndex(
                name: "IX_rolemenupermission_roleid",
                schema: "jsini",
                table: "rolemenupermission",
                column: "roleid");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "rolemenupermission",
                schema: "jsini");
        }
    }
}
