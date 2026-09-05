using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace funeralv2Api.Migrations
{
    /// <inheritdoc />
    public partial class AddDeceasedDetailTables : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "burial_plot",
                schema: "smfr",
                table: "deceaseds",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "cause_of_death",
                schema: "smfr",
                table: "deceaseds",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "family_photo_group_id",
                schema: "smfr",
                table: "deceaseds",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "memorial_photo_file_id",
                schema: "smfr",
                table: "deceaseds",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "memorial_photo_url",
                schema: "smfr",
                table: "deceaseds",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ssn",
                schema: "smfr",
                table: "deceaseds",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "deceased_contractors",
                schema: "smfr",
                columns: table => new
                {
                    id = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    deceased_id = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    contact = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    relation = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    address = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    remark = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    signature_file_id = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_deceased_contractors", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "deceased_facilities",
                schema: "smfr",
                columns: table => new
                {
                    id = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    deceased_id = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    facility_type = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    start_time = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    end_time = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    use_hours = table.Column<double>(type: "double precision", nullable: false),
                    unit_price = table.Column<decimal>(type: "numeric", nullable: false),
                    total_price = table.Column<decimal>(type: "numeric", nullable: false),
                    remark = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_deceased_facilities", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "deceased_managers",
                schema: "smfr",
                columns: table => new
                {
                    id = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    deceased_id = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    director_name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    director_contact = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    mutual_aid_company = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    staff_name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    staff_contact = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_deceased_managers", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "deceased_mourners",
                schema: "smfr",
                columns: table => new
                {
                    id = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    deceased_id = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    relation = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    contact = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    email = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    address = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    is_chief = table.Column<bool>(type: "boolean", nullable: false),
                    sort_order = table.Column<int>(type: "integer", nullable: false),
                    is_deleted = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_deceased_mourners", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "deceased_rooms",
                schema: "smfr",
                columns: table => new
                {
                    id = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    deceased_id = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    room_id = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    start_time = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    end_time = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    is_deleted = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_deceased_rooms", x => x.id);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "deceased_contractors",
                schema: "smfr");

            migrationBuilder.DropTable(
                name: "deceased_facilities",
                schema: "smfr");

            migrationBuilder.DropTable(
                name: "deceased_managers",
                schema: "smfr");

            migrationBuilder.DropTable(
                name: "deceased_mourners",
                schema: "smfr");

            migrationBuilder.DropTable(
                name: "deceased_rooms",
                schema: "smfr");

            migrationBuilder.DropColumn(
                name: "burial_plot",
                schema: "smfr",
                table: "deceaseds");

            migrationBuilder.DropColumn(
                name: "cause_of_death",
                schema: "smfr",
                table: "deceaseds");

            migrationBuilder.DropColumn(
                name: "family_photo_group_id",
                schema: "smfr",
                table: "deceaseds");

            migrationBuilder.DropColumn(
                name: "memorial_photo_file_id",
                schema: "smfr",
                table: "deceaseds");

            migrationBuilder.DropColumn(
                name: "memorial_photo_url",
                schema: "smfr",
                table: "deceaseds");

            migrationBuilder.DropColumn(
                name: "ssn",
                schema: "smfr",
                table: "deceaseds");
        }
    }
}
