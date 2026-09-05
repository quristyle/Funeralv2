using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace funeralv2Api.Migrations
{
    /// <inheritdoc />
    public partial class AddDevicePublicIp : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "public_ip_address",
                schema: "smfr",
                table: "devices",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true,
                comment: "공인 IP 주소");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "public_ip_address",
                schema: "smfr",
                table: "devices");
        }
    }
}
