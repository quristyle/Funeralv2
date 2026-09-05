using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FileServer.Migrations
{
    /// <inheritdoc />
    public partial class UpdateAllComments : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterTable(
                name: "filemetadatas",
                schema: "scom",
                comment: "개별 파일의 메타데이터 및 상세 정보를 저장하는 엔티티 클래스",
                oldComment: "파일의 메타데이터 정보를 저장하는 엔티티 클래스");

            migrationBuilder.AlterTable(
                name: "filegroups",
                schema: "scom",
                comment: "파일 그룹 엔티티 클래스 (다중 파일 업로드를 그룹 단위로 관리하기 위함)",
                oldComment: "파일 그룹 엔티티 클래스");

            migrationBuilder.AlterColumn<string>(
                name: "storedname",
                schema: "scom",
                table: "filemetadatas",
                type: "text",
                nullable: false,
                comment: "스토리지에 실제 저장된 난수화된 파일명 (예: uuid.png)",
                oldClrType: typeof(string),
                oldType: "text",
                oldComment: "실제 스토리지에 저장된 파일명 (예: uuid.png)");

            migrationBuilder.AlterColumn<long>(
                name: "size",
                schema: "scom",
                table: "filemetadatas",
                type: "bigint",
                nullable: false,
                comment: "파일 크기 (단위: Bytes)",
                oldClrType: typeof(long),
                oldType: "bigint",
                oldComment: "파일 크기 (Bytes)");

            migrationBuilder.AlterColumn<string>(
                name: "path",
                schema: "scom",
                table: "filemetadatas",
                type: "text",
                nullable: false,
                comment: "파일이 위치한 물리적 디렉토리 상대경로",
                oldClrType: typeof(string),
                oldType: "text",
                oldComment: "파일 저장 경로 (디렉토리 상대경로)");

            migrationBuilder.AlterColumn<string>(
                name: "originalname",
                schema: "scom",
                table: "filemetadatas",
                type: "text",
                nullable: false,
                comment: "업로드 당시의 원본 파일명 (예: photo.png)",
                oldClrType: typeof(string),
                oldType: "text",
                oldComment: "원본 파일명 (예: photo.png)");

            migrationBuilder.AlterColumn<bool>(
                name: "isrepresentative",
                schema: "scom",
                table: "filemetadatas",
                type: "boolean",
                nullable: false,
                comment: "그룹 내 대표 파일(썸네일 등) 여부",
                oldClrType: typeof(bool),
                oldType: "boolean",
                oldComment: "대표 파일 여부");

            migrationBuilder.AlterColumn<bool>(
                name: "isimage",
                schema: "scom",
                table: "filemetadatas",
                type: "boolean",
                nullable: false,
                comment: "이미지 파일 형식 여부",
                oldClrType: typeof(bool),
                oldType: "boolean",
                oldComment: "이미지 파일 여부");

            migrationBuilder.AlterColumn<Guid>(
                name: "filegroupid",
                schema: "scom",
                table: "filemetadatas",
                type: "uuid",
                nullable: true,
                comment: "소속된 파일 그룹 식별자 (ID, Nullable)",
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
                comment: "HTTP Content-Type (MIME 타입, 예: image/png, application/pdf 등)",
                oldClrType: typeof(string),
                oldType: "text",
                oldComment: "Content Type (MIME Type)");

            migrationBuilder.AlterColumn<string>(
                name: "biztype",
                schema: "scom",
                table: "filegroups",
                type: "text",
                nullable: false,
                comment: "비즈니스 구분 코드 (예: PROFILE, BOARD, ITEM 등)",
                oldClrType: typeof(string),
                oldType: "text",
                oldComment: "비즈니스 구분 (예: PROFILE, BOARD, ITEM 등)");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterTable(
                name: "filemetadatas",
                schema: "scom",
                comment: "파일의 메타데이터 정보를 저장하는 엔티티 클래스",
                oldComment: "개별 파일의 메타데이터 및 상세 정보를 저장하는 엔티티 클래스");

            migrationBuilder.AlterTable(
                name: "filegroups",
                schema: "scom",
                comment: "파일 그룹 엔티티 클래스",
                oldComment: "파일 그룹 엔티티 클래스 (다중 파일 업로드를 그룹 단위로 관리하기 위함)");

            migrationBuilder.AlterColumn<string>(
                name: "storedname",
                schema: "scom",
                table: "filemetadatas",
                type: "text",
                nullable: false,
                comment: "실제 스토리지에 저장된 파일명 (예: uuid.png)",
                oldClrType: typeof(string),
                oldType: "text",
                oldComment: "스토리지에 실제 저장된 난수화된 파일명 (예: uuid.png)");

            migrationBuilder.AlterColumn<long>(
                name: "size",
                schema: "scom",
                table: "filemetadatas",
                type: "bigint",
                nullable: false,
                comment: "파일 크기 (Bytes)",
                oldClrType: typeof(long),
                oldType: "bigint",
                oldComment: "파일 크기 (단위: Bytes)");

            migrationBuilder.AlterColumn<string>(
                name: "path",
                schema: "scom",
                table: "filemetadatas",
                type: "text",
                nullable: false,
                comment: "파일 저장 경로 (디렉토리 상대경로)",
                oldClrType: typeof(string),
                oldType: "text",
                oldComment: "파일이 위치한 물리적 디렉토리 상대경로");

            migrationBuilder.AlterColumn<string>(
                name: "originalname",
                schema: "scom",
                table: "filemetadatas",
                type: "text",
                nullable: false,
                comment: "원본 파일명 (예: photo.png)",
                oldClrType: typeof(string),
                oldType: "text",
                oldComment: "업로드 당시의 원본 파일명 (예: photo.png)");

            migrationBuilder.AlterColumn<bool>(
                name: "isrepresentative",
                schema: "scom",
                table: "filemetadatas",
                type: "boolean",
                nullable: false,
                comment: "대표 파일 여부",
                oldClrType: typeof(bool),
                oldType: "boolean",
                oldComment: "그룹 내 대표 파일(썸네일 등) 여부");

            migrationBuilder.AlterColumn<bool>(
                name: "isimage",
                schema: "scom",
                table: "filemetadatas",
                type: "boolean",
                nullable: false,
                comment: "이미지 파일 여부",
                oldClrType: typeof(bool),
                oldType: "boolean",
                oldComment: "이미지 파일 형식 여부");

            migrationBuilder.AlterColumn<Guid>(
                name: "filegroupid",
                schema: "scom",
                table: "filemetadatas",
                type: "uuid",
                nullable: true,
                comment: "파일 그룹 ID (Nullable)",
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true,
                oldComment: "소속된 파일 그룹 식별자 (ID, Nullable)");

            migrationBuilder.AlterColumn<string>(
                name: "contenttype",
                schema: "scom",
                table: "filemetadatas",
                type: "text",
                nullable: false,
                comment: "Content Type (MIME Type)",
                oldClrType: typeof(string),
                oldType: "text",
                oldComment: "HTTP Content-Type (MIME 타입, 예: image/png, application/pdf 등)");

            migrationBuilder.AlterColumn<string>(
                name: "biztype",
                schema: "scom",
                table: "filegroups",
                type: "text",
                nullable: false,
                comment: "비즈니스 구분 (예: PROFILE, BOARD, ITEM 등)",
                oldClrType: typeof(string),
                oldType: "text",
                oldComment: "비즈니스 구분 코드 (예: PROFILE, BOARD, ITEM 등)");
        }
    }
}
