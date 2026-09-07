using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HelpDeskServer.Migrations
{
    /// <inheritdoc />
    public partial class AddCompletionToSchedule : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "completeddate",
                schema: "jsini",
                table: "schedules",
                type: "timestamp with time zone",
                nullable: true,
                comment: "완료일 (null이면 미완료 상태)");

            migrationBuilder.AddColumn<bool>(
                name: "iscompleted",
                schema: "jsini",
                table: "schedules",
                type: "boolean",
                nullable: false,
                defaultValue: false,
                comment: "완료 여부");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "completeddate",
                schema: "jsini",
                table: "schedules");

            migrationBuilder.DropColumn(
                name: "iscompleted",
                schema: "jsini",
                table: "schedules");
        }
    }
}
