using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FileServer.Migrations
{
    /// <inheritdoc />
    public partial class AddDbComments : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterTable(
                name: "filemetadatas",
                schema: "scom",
                comment: "파일의 메타데이터 정보를 저장하는 엔티티 클래스");

            migrationBuilder.AlterTable(
                name: "filegroups",
                schema: "scom",
                comment: "파일 그룹 엔티티 클래스");

            migrationBuilder.AlterColumn<string>(
                name: "storedname",
                schema: "scom",
                table: "filemetadatas",
                type: "text",
                nullable: false,
                comment: "실제 스토리지에 저장된 파일명 (예: uuid.png)",
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AlterColumn<int>(
                name: "sortorder",
                schema: "scom",
                table: "filemetadatas",
                type: "integer",
                nullable: false,
                comment: "정렬 순서",
                oldClrType: typeof(int),
                oldType: "integer");

            migrationBuilder.AlterColumn<long>(
                name: "size",
                schema: "scom",
                table: "filemetadatas",
                type: "bigint",
                nullable: false,
                comment: "파일 크기 (Bytes)",
                oldClrType: typeof(long),
                oldType: "bigint");

            migrationBuilder.AlterColumn<string>(
                name: "path",
                schema: "scom",
                table: "filemetadatas",
                type: "text",
                nullable: false,
                comment: "파일 저장 경로 (디렉토리 상대경로)",
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AlterColumn<string>(
                name: "originalname",
                schema: "scom",
                table: "filemetadatas",
                type: "text",
                nullable: false,
                comment: "원본 파일명 (예: photo.png)",
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AlterColumn<bool>(
                name: "isrepresentative",
                schema: "scom",
                table: "filemetadatas",
                type: "boolean",
                nullable: false,
                comment: "대표 파일 여부",
                oldClrType: typeof(bool),
                oldType: "boolean");

            migrationBuilder.AlterColumn<bool>(
                name: "isimage",
                schema: "scom",
                table: "filemetadatas",
                type: "boolean",
                nullable: false,
                comment: "이미지 파일 여부",
                oldClrType: typeof(bool),
                oldType: "boolean");

            migrationBuilder.AlterColumn<Guid>(
                name: "filegroupid",
                schema: "scom",
                table: "filemetadatas",
                type: "uuid",
                nullable: true,
                comment: "파일 그룹 ID (Nullable)",
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "contenttype",
                schema: "scom",
                table: "filemetadatas",
                type: "text",
                nullable: false,
                comment: "Content Type (MIME Type)",
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AlterColumn<string>(
                name: "biztype",
                schema: "scom",
                table: "filegroups",
                type: "text",
                nullable: false,
                comment: "비즈니스 구분 (예: PROFILE, BOARD, ITEM 등)",
                oldClrType: typeof(string),
                oldType: "text");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterTable(
                name: "filemetadatas",
                schema: "scom",
                oldComment: "파일의 메타데이터 정보를 저장하는 엔티티 클래스");

            migrationBuilder.AlterTable(
                name: "filegroups",
                schema: "scom",
                oldComment: "파일 그룹 엔티티 클래스");

            migrationBuilder.AlterColumn<string>(
                name: "storedname",
                schema: "scom",
                table: "filemetadatas",
                type: "text",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text",
                oldComment: "실제 스토리지에 저장된 파일명 (예: uuid.png)");

            migrationBuilder.AlterColumn<int>(
                name: "sortorder",
                schema: "scom",
                table: "filemetadatas",
                type: "integer",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "integer",
                oldComment: "정렬 순서");

            migrationBuilder.AlterColumn<long>(
                name: "size",
                schema: "scom",
                table: "filemetadatas",
                type: "bigint",
                nullable: false,
                oldClrType: typeof(long),
                oldType: "bigint",
                oldComment: "파일 크기 (Bytes)");

            migrationBuilder.AlterColumn<string>(
                name: "path",
                schema: "scom",
                table: "filemetadatas",
                type: "text",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text",
                oldComment: "파일 저장 경로 (디렉토리 상대경로)");

            migrationBuilder.AlterColumn<string>(
                name: "originalname",
                schema: "scom",
                table: "filemetadatas",
                type: "text",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text",
                oldComment: "원본 파일명 (예: photo.png)");

            migrationBuilder.AlterColumn<bool>(
                name: "isrepresentative",
                schema: "scom",
                table: "filemetadatas",
                type: "boolean",
                nullable: false,
                oldClrType: typeof(bool),
                oldType: "boolean",
                oldComment: "대표 파일 여부");

            migrationBuilder.AlterColumn<bool>(
                name: "isimage",
                schema: "scom",
                table: "filemetadatas",
                type: "boolean",
                nullable: false,
                oldClrType: typeof(bool),
                oldType: "boolean",
                oldComment: "이미지 파일 여부");

            migrationBuilder.AlterColumn<Guid>(
                name: "filegroupid",
                schema: "scom",
                table: "filemetadatas",
                type: "uuid",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true,
                oldComment: "파일 그룹 ID (Nullable)");

            migrationBuilder.AlterColumn<string>(
                name: "contenttype",
                schema: "scom",
                table: "filemetadatas",
                type: "text",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text",
                oldComment: "Content Type (MIME Type)");

            migrationBuilder.AlterColumn<string>(
                name: "biztype",
                schema: "scom",
                table: "filegroups",
                type: "text",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text",
                oldComment: "비즈니스 구분 (예: PROFILE, BOARD, ITEM 등)");
        }
    }
}
