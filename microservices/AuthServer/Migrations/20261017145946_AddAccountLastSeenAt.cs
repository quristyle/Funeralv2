using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AuthServer.Migrations
{
    /// <inheritdoc />
    public partial class AddAccountLastSeenAt : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "last_seen_at",
                schema: "scom",
                table: "accounts",
                type: "timestamp with time zone",
                nullable: true,
                comment: "마지막으로 포털을 쓴 시각 (UTC). 아직 한 번도 안 썼으면 null 이다.");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "last_seen_at",
                schema: "scom",
                table: "accounts");
        }
    }
}
