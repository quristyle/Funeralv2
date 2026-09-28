using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HelpDeskServer.Migrations
{
    /// <inheritdoc />
    public partial class AddRequestAcceptedAt : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "acceptedat",
                schema: "helpdesk",
                table: "improvementrequest",
                type: "timestamp with time zone",
                nullable: true,
                comment: "접수일시 — 담당자가 이 글을 맡은 때.");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "acceptedat",
                schema: "helpdesk",
                table: "improvementrequest");
        }
    }
}
