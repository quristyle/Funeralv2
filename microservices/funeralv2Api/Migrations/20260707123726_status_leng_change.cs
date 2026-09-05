using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace funeralv2Api.Migrations
{
    /// <inheritdoc />
    public partial class status_leng_change : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "status",
                schema: "smfr",
                table: "deceaseds",
                type: "character varying(30)",
                maxLength: 30,
                nullable: false,
                comment: "장례 진행 상태 (예: IN_HOSPITAL, DISCHARGED, COMPLETED 등, 기본값: IN_HOSPITAL)",
                oldClrType: typeof(string),
                oldType: "character varying(20)",
                oldMaxLength: 20,
                oldComment: "장례 진행 상태 (예: IN_HOSPITAL, DISCHARGED, COMPLETED 등, 기본값: IN_HOSPITAL)");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "status",
                schema: "smfr",
                table: "deceaseds",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                comment: "장례 진행 상태 (예: IN_HOSPITAL, DISCHARGED, COMPLETED 등, 기본값: IN_HOSPITAL)",
                oldClrType: typeof(string),
                oldType: "character varying(30)",
                oldMaxLength: 30,
                oldComment: "장례 진행 상태 (예: IN_HOSPITAL, DISCHARGED, COMPLETED 등, 기본값: IN_HOSPITAL)");
        }
    }
}
