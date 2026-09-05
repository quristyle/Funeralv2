using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FileServer.Migrations
{
    /// <inheritdoc />
    public partial class AddFileGroup : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "filegroupid",
                schema: "scom",
                table: "filemetadatas",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "isrepresentative",
                schema: "scom",
                table: "filemetadatas",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "sortorder",
                schema: "scom",
                table: "filemetadatas",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateTable(
                name: "filegroups",
                schema: "scom",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    biztype = table.Column<string>(type: "text", nullable: false),
                    createdat = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    createdby = table.Column<string>(type: "text", nullable: true),
                    updatedat = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    updatedby = table.Column<string>(type: "text", nullable: true),
                    isdeleted = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_filegroups", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_filemetadatas_filegroupid",
                schema: "scom",
                table: "filemetadatas",
                column: "filegroupid");

            migrationBuilder.AddForeignKey(
                name: "FK_filemetadatas_filegroups_filegroupid",
                schema: "scom",
                table: "filemetadatas",
                column: "filegroupid",
                principalSchema: "scom",
                principalTable: "filegroups",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_filemetadatas_filegroups_filegroupid",
                schema: "scom",
                table: "filemetadatas");

            migrationBuilder.DropTable(
                name: "filegroups",
                schema: "scom");

            migrationBuilder.DropIndex(
                name: "IX_filemetadatas_filegroupid",
                schema: "scom",
                table: "filemetadatas");

            migrationBuilder.DropColumn(
                name: "filegroupid",
                schema: "scom",
                table: "filemetadatas");

            migrationBuilder.DropColumn(
                name: "isrepresentative",
                schema: "scom",
                table: "filemetadatas");

            migrationBuilder.DropColumn(
                name: "sortorder",
                schema: "scom",
                table: "filemetadatas");
        }
    }
}
