using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace funeralv2Api.Migrations
{
    /// <inheritdoc />
    public partial class AddDbComments : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterTable(
                name: "rooms",
                schema: "smfr",
                comment: "호실 엔티티");

            migrationBuilder.AlterTable(
                name: "media_sources",
                schema: "smfr",
                comment: "미디어 소스 (동영상, 음원, 이미지 등) 리소스를 저장하는 엔티티 클래스");

            migrationBuilder.AlterTable(
                name: "floors",
                schema: "smfr",
                comment: "층 엔티티");

            migrationBuilder.AlterTable(
                name: "devices",
                schema: "smfr",
                comment: "장비 정보");

            migrationBuilder.AlterTable(
                name: "device_text_overlays",
                schema: "smfr",
                comment: "장비 텍스트 오버레이 설정 엔티티\n            장비(모니터) 화면의 특정 위치에 텍스트를 배치하는 설정을 관리합니다.\n            위치 및 크기 값은 % 단위 (소수점 3자리)로 저장되어 다양한 해상도에서도 정확한 위치를 보장합니다.");

            migrationBuilder.AlterTable(
                name: "device_ribbons",
                schema: "smfr",
                comment: "장비 리본 설정 엔티티\n            장비(모니터) 화면의 특정 위치에 장식(리본) 이미지를 배치하는 설정을 관리합니다.\n            위치 및 크기 값은 % 단위 (소수점 3자리)로 저장되어 다양한 해상도에서도 정확한 위치를 보장합니다.");

            migrationBuilder.AlterTable(
                name: "device_configs",
                schema: "smfr",
                comment: "장비 기본 설정 (Device 1:1 관계)\n            음량, 밝기, 자동 전원, 재시작 시각 등을 관리합니다.");

            migrationBuilder.AlterTable(
                name: "device_attributes",
                schema: "smfr",
                comment: "장비 속성 정보 (Device 1:N 관계)\n            장비 유형별 세부 설정 값을 key-value 형태 또는 구조화된 컬럼으로 관리합니다.");

            migrationBuilder.AlterTable(
                name: "deceaseds",
                schema: "smfr",
                comment: "고인 정보 관리 엔티티");

            migrationBuilder.AlterTable(
                name: "deceased_rooms",
                schema: "smfr",
                comment: "고인별 호실(빈소) 배정 및 사용 기간 이력 관리 엔티티");

            migrationBuilder.AlterTable(
                name: "deceased_mourners",
                schema: "smfr",
                comment: "고인 상주 정보 관리 엔티티");

            migrationBuilder.AlterTable(
                name: "deceased_managers",
                schema: "smfr",
                comment: "고인별 장례 담당 임직원 및 지도사 매핑 엔티티");

            migrationBuilder.AlterTable(
                name: "deceased_facilities",
                schema: "smfr",
                comment: "고인별 장례 시설(안치실, 염습실, 영결식장 등) 이용 내역 엔티티");

            migrationBuilder.AlterTable(
                name: "deceased_contractors",
                schema: "smfr",
                comment: "고인 장례 계약자 정보 관리 엔티티");

            migrationBuilder.AlterTable(
                name: "buildings",
                schema: "smfr",
                comment: "건물 엔티티");

            migrationBuilder.AlterColumn<string>(
                name: "status",
                schema: "smfr",
                table: "rooms",
                type: "text",
                nullable: false,
                comment: "상태 (ACTIVE, INACTIVE)",
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AlterColumn<int>(
                name: "sort_order",
                schema: "smfr",
                table: "rooms",
                type: "integer",
                nullable: false,
                comment: "정렬 순서",
                oldClrType: typeof(int),
                oldType: "integer");

            migrationBuilder.AlterColumn<string>(
                name: "short_name",
                schema: "smfr",
                table: "rooms",
                type: "text",
                nullable: true,
                comment: "짧은 명칭",
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "room_type",
                schema: "smfr",
                table: "rooms",
                type: "text",
                nullable: false,
                comment: "호실 타입 (예: 빈소, 안치실, 참관실 등)",
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AlterColumn<string>(
                name: "remark",
                schema: "smfr",
                table: "rooms",
                type: "text",
                nullable: true,
                comment: "비고/설명",
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "name",
                schema: "smfr",
                table: "rooms",
                type: "text",
                nullable: false,
                comment: "호실 명칭",
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AlterColumn<string>(
                name: "floor_id",
                schema: "smfr",
                table: "rooms",
                type: "text",
                nullable: false,
                comment: "소속 층 ID",
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AlterColumn<string>(
                name: "building_id",
                schema: "smfr",
                table: "rooms",
                type: "text",
                nullable: false,
                comment: "소속 건물 ID",
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AlterColumn<string>(
                name: "webmurl",
                schema: "smfr",
                table: "media_sources",
                type: "text",
                nullable: true,
                comment: "동영상인 경우 WebM 파일의 파일 URL 경로",
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true);

            migrationBuilder.AlterColumn<Guid>(
                name: "webmfileid",
                schema: "smfr",
                table: "media_sources",
                type: "uuid",
                nullable: true,
                comment: "WebM 파일의 FileMetadata 식별자",
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "url",
                schema: "smfr",
                table: "media_sources",
                type: "text",
                nullable: false,
                comment: "파일 URL 경로 (FileServer 등 보관 경로)",
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AlterColumn<string>(
                name: "thumbnailurl",
                schema: "smfr",
                table: "media_sources",
                type: "text",
                nullable: true,
                comment: "동영상인 경우 첫 클립 썸네일 이미지의 파일 URL 경로",
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true);

            migrationBuilder.AlterColumn<Guid>(
                name: "thumbnailfileid",
                schema: "smfr",
                table: "media_sources",
                type: "uuid",
                nullable: true,
                comment: "썸네일 이미지 파일의 FileMetadata 식별자",
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "status",
                schema: "smfr",
                table: "media_sources",
                type: "text",
                nullable: false,
                comment: "미디어 변환 상태 (PROCESSING, COMPLETED, FAILED)",
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AlterColumn<string>(
                name: "sourcetype",
                schema: "smfr",
                table: "media_sources",
                type: "text",
                nullable: false,
                comment: "미디어 유형 (VIDEO, AUDIO, IMAGE)",
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AlterColumn<int>(
                name: "sortorder",
                schema: "smfr",
                table: "media_sources",
                type: "integer",
                nullable: false,
                comment: "정렬 순서",
                oldClrType: typeof(int),
                oldType: "integer");

            migrationBuilder.AlterColumn<string>(
                name: "shortname",
                schema: "smfr",
                table: "media_sources",
                type: "text",
                nullable: true,
                comment: "영상 짧은 명칭",
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "remark",
                schema: "smfr",
                table: "media_sources",
                type: "text",
                nullable: true,
                comment: "설명 및 비고",
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true);

            migrationBuilder.AlterColumn<Guid>(
                name: "originalfileid",
                schema: "smfr",
                table: "media_sources",
                type: "uuid",
                nullable: true,
                comment: "원본 파일의 FileMetadata 식별자",
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "oggurl",
                schema: "smfr",
                table: "media_sources",
                type: "text",
                nullable: true,
                comment: "음원인 경우 OGG 파일의 파일 URL 경로",
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true);

            migrationBuilder.AlterColumn<Guid>(
                name: "oggfileid",
                schema: "smfr",
                table: "media_sources",
                type: "uuid",
                nullable: true,
                comment: "OGG 파일의 FileMetadata 식별자",
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "name",
                schema: "smfr",
                table: "media_sources",
                type: "text",
                nullable: false,
                comment: "미디어 소스 명칭",
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AlterColumn<bool>(
                name: "haswebm",
                schema: "smfr",
                table: "media_sources",
                type: "boolean",
                nullable: false,
                comment: "WebM 파일 변환 완료 여부",
                oldClrType: typeof(bool),
                oldType: "boolean");

            migrationBuilder.AlterColumn<bool>(
                name: "hasthumbnail",
                schema: "smfr",
                table: "media_sources",
                type: "boolean",
                nullable: false,
                comment: "썸네일 이미지 생성 완료 여부",
                oldClrType: typeof(bool),
                oldType: "boolean");

            migrationBuilder.AlterColumn<bool>(
                name: "hasogg",
                schema: "smfr",
                table: "media_sources",
                type: "boolean",
                nullable: false,
                comment: "OGG 파일 변환 완료 여부",
                oldClrType: typeof(bool),
                oldType: "boolean");

            migrationBuilder.AlterColumn<bool>(
                name: "hasaac",
                schema: "smfr",
                table: "media_sources",
                type: "boolean",
                nullable: false,
                comment: "AAC 파일 변환 완료 여부",
                oldClrType: typeof(bool),
                oldType: "boolean");

            migrationBuilder.AlterColumn<long>(
                name: "filesize",
                schema: "smfr",
                table: "media_sources",
                type: "bigint",
                nullable: true,
                comment: "파일 크기 (Bytes)",
                oldClrType: typeof(long),
                oldType: "bigint",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "errormessage",
                schema: "smfr",
                table: "media_sources",
                type: "text",
                nullable: true,
                comment: "미디어 변환 실패 시 발생한 에러 메시지",
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true);

            migrationBuilder.AlterColumn<DateTime>(
                name: "conversionstartedat",
                schema: "smfr",
                table: "media_sources",
                type: "timestamp with time zone",
                nullable: true,
                comment: "변환 시작 일시",
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone",
                oldNullable: true);

            migrationBuilder.AlterColumn<DateTime>(
                name: "conversioncompletedat",
                schema: "smfr",
                table: "media_sources",
                type: "timestamp with time zone",
                nullable: true,
                comment: "변환 완료 일시",
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "conversioncommand",
                schema: "smfr",
                table: "media_sources",
                type: "text",
                nullable: true,
                comment: "변환 시 사용된 FFmpeg 명령어",
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "aacurl",
                schema: "smfr",
                table: "media_sources",
                type: "text",
                nullable: true,
                comment: "음원인 경우 AAC 파일의 파일 URL 경로",
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true);

            migrationBuilder.AlterColumn<Guid>(
                name: "aacfileid",
                schema: "smfr",
                table: "media_sources",
                type: "uuid",
                nullable: true,
                comment: "AAC 파일의 FileMetadata 식별자",
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);

            migrationBuilder.AlterColumn<int>(
                name: "sort_order",
                schema: "smfr",
                table: "floors",
                type: "integer",
                nullable: false,
                comment: "정렬 순서",
                oldClrType: typeof(int),
                oldType: "integer");

            migrationBuilder.AlterColumn<string>(
                name: "remark",
                schema: "smfr",
                table: "floors",
                type: "text",
                nullable: true,
                comment: "비고/설명",
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "name",
                schema: "smfr",
                table: "floors",
                type: "text",
                nullable: false,
                comment: "층 명칭",
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AlterColumn<string>(
                name: "building_id",
                schema: "smfr",
                table: "floors",
                type: "text",
                nullable: false,
                comment: "소속 건물 ID",
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AlterColumn<int>(
                name: "sort_order",
                schema: "smfr",
                table: "devices",
                type: "integer",
                nullable: false,
                comment: "정렬 순서",
                oldClrType: typeof(int),
                oldType: "integer");

            migrationBuilder.AlterColumn<decimal>(
                name: "width",
                schema: "smfr",
                table: "device_text_overlays",
                type: "numeric(6,3)",
                nullable: false,
                comment: "너비 (%, 소수점 3자리)",
                oldClrType: typeof(decimal),
                oldType: "numeric(6,3)");

            migrationBuilder.AlterColumn<string>(
                name: "text_content",
                schema: "smfr",
                table: "device_text_overlays",
                type: "character varying(500)",
                maxLength: 500,
                nullable: false,
                comment: "표시할 텍스트 내용",
                oldClrType: typeof(string),
                oldType: "character varying(500)",
                oldMaxLength: 500);

            migrationBuilder.AlterColumn<string>(
                name: "text_align",
                schema: "smfr",
                table: "device_text_overlays",
                type: "character varying(10)",
                maxLength: 10,
                nullable: false,
                comment: "텍스트 정렬 (left | center | right)",
                oldClrType: typeof(string),
                oldType: "character varying(10)",
                oldMaxLength: 10);

            migrationBuilder.AlterColumn<int>(
                name: "sort_order",
                schema: "smfr",
                table: "device_text_overlays",
                type: "integer",
                nullable: false,
                comment: "정렬 순서",
                oldClrType: typeof(int),
                oldType: "integer");

            migrationBuilder.AlterColumn<string>(
                name: "remark",
                schema: "smfr",
                table: "device_text_overlays",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true,
                comment: "비고",
                oldClrType: typeof(string),
                oldType: "character varying(500)",
                oldMaxLength: 500,
                oldNullable: true);

            migrationBuilder.AlterColumn<decimal>(
                name: "position_top",
                schema: "smfr",
                table: "device_text_overlays",
                type: "numeric(6,3)",
                nullable: false,
                comment: "상단 위치 (%, 소수점 3자리)",
                oldClrType: typeof(decimal),
                oldType: "numeric(6,3)");

            migrationBuilder.AlterColumn<decimal>(
                name: "position_left",
                schema: "smfr",
                table: "device_text_overlays",
                type: "numeric(6,3)",
                nullable: false,
                comment: "좌측 위치 (%, 소수점 3자리)",
                oldClrType: typeof(decimal),
                oldType: "numeric(6,3)");

            migrationBuilder.AlterColumn<decimal>(
                name: "height",
                schema: "smfr",
                table: "device_text_overlays",
                type: "numeric(6,3)",
                nullable: false,
                comment: "높이 (%, 소수점 3자리)",
                oldClrType: typeof(decimal),
                oldType: "numeric(6,3)");

            migrationBuilder.AlterColumn<string>(
                name: "font_weight",
                schema: "smfr",
                table: "device_text_overlays",
                type: "character varying(10)",
                maxLength: 10,
                nullable: false,
                comment: "폰트 굵기 (normal | bold)",
                oldClrType: typeof(string),
                oldType: "character varying(10)",
                oldMaxLength: 10);

            migrationBuilder.AlterColumn<decimal>(
                name: "font_size",
                schema: "smfr",
                table: "device_text_overlays",
                type: "numeric(6,3)",
                nullable: false,
                comment: "폰트 크기 (px 단위 기준, 화면 높이 대비 %로 저장)",
                oldClrType: typeof(decimal),
                oldType: "numeric(6,3)");

            migrationBuilder.AlterColumn<string>(
                name: "font_color",
                schema: "smfr",
                table: "device_text_overlays",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                comment: "폰트 색상 (CSS hex 색상값, 예: #FFFFFF)",
                oldClrType: typeof(string),
                oldType: "character varying(20)",
                oldMaxLength: 20);

            migrationBuilder.AlterColumn<string>(
                name: "device_id",
                schema: "smfr",
                table: "device_text_overlays",
                type: "character varying(50)",
                maxLength: 50,
                nullable: false,
                comment: "장비 FK",
                oldClrType: typeof(string),
                oldType: "character varying(50)",
                oldMaxLength: 50);

            migrationBuilder.AlterColumn<string>(
                name: "background_color",
                schema: "smfr",
                table: "device_text_overlays",
                type: "character varying(30)",
                maxLength: 30,
                nullable: false,
                comment: "배경 색상 (CSS hex 색상값 또는 'transparent')",
                oldClrType: typeof(string),
                oldType: "character varying(30)",
                oldMaxLength: 30);

            migrationBuilder.AlterColumn<decimal>(
                name: "width",
                schema: "smfr",
                table: "device_ribbons",
                type: "numeric(6,3)",
                nullable: false,
                comment: "너비 (%, 소수점 3자리)",
                oldClrType: typeof(decimal),
                oldType: "numeric(6,3)");

            migrationBuilder.AlterColumn<int>(
                name: "sort_order",
                schema: "smfr",
                table: "device_ribbons",
                type: "integer",
                nullable: false,
                comment: "정렬 순서",
                oldClrType: typeof(int),
                oldType: "integer");

            migrationBuilder.AlterColumn<string>(
                name: "remark",
                schema: "smfr",
                table: "device_ribbons",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true,
                comment: "비고",
                oldClrType: typeof(string),
                oldType: "character varying(500)",
                oldMaxLength: 500,
                oldNullable: true);

            migrationBuilder.AlterColumn<decimal>(
                name: "position_top",
                schema: "smfr",
                table: "device_ribbons",
                type: "numeric(6,3)",
                nullable: false,
                comment: "상단 위치 (%, 소수점 3자리)",
                oldClrType: typeof(decimal),
                oldType: "numeric(6,3)");

            migrationBuilder.AlterColumn<decimal>(
                name: "position_left",
                schema: "smfr",
                table: "device_ribbons",
                type: "numeric(6,3)",
                nullable: false,
                comment: "좌측 위치 (%, 소수점 3자리)",
                oldClrType: typeof(decimal),
                oldType: "numeric(6,3)");

            migrationBuilder.AlterColumn<string>(
                name: "media_source_id",
                schema: "smfr",
                table: "device_ribbons",
                type: "character varying(50)",
                maxLength: 50,
                nullable: false,
                comment: "장식(미디어소스) FK - MediaSource.SourceType = IMAGE",
                oldClrType: typeof(string),
                oldType: "character varying(50)",
                oldMaxLength: 50);

            migrationBuilder.AlterColumn<decimal>(
                name: "height",
                schema: "smfr",
                table: "device_ribbons",
                type: "numeric(6,3)",
                nullable: false,
                comment: "높이 (%, 소수점 3자리)",
                oldClrType: typeof(decimal),
                oldType: "numeric(6,3)");

            migrationBuilder.AlterColumn<string>(
                name: "device_id",
                schema: "smfr",
                table: "device_ribbons",
                type: "character varying(50)",
                maxLength: 50,
                nullable: false,
                comment: "장비 FK",
                oldClrType: typeof(string),
                oldType: "character varying(50)",
                oldMaxLength: 50);

            migrationBuilder.AlterColumn<int>(
                name: "volume",
                schema: "smfr",
                table: "device_configs",
                type: "integer",
                nullable: false,
                comment: "기기 음량 (0-100)",
                oldClrType: typeof(int),
                oldType: "integer");

            migrationBuilder.AlterColumn<string>(
                name: "reboot_time",
                schema: "smfr",
                table: "device_configs",
                type: "character varying(5)",
                maxLength: 5,
                nullable: true,
                comment: "일일 자동 재시작 시각 (HH:mm)",
                oldClrType: typeof(string),
                oldType: "character varying(5)",
                oldMaxLength: 5,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "power_on_time",
                schema: "smfr",
                table: "device_configs",
                type: "character varying(5)",
                maxLength: 5,
                nullable: true,
                comment: "자동 켜짐 시각 (HH:mm)",
                oldClrType: typeof(string),
                oldType: "character varying(5)",
                oldMaxLength: 5,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "power_off_time",
                schema: "smfr",
                table: "device_configs",
                type: "character varying(5)",
                maxLength: 5,
                nullable: true,
                comment: "자동 꺼짐 시각 (HH:mm)",
                oldClrType: typeof(string),
                oldType: "character varying(5)",
                oldMaxLength: 5,
                oldNullable: true);

            migrationBuilder.AlterColumn<bool>(
                name: "is_auto_power",
                schema: "smfr",
                table: "device_configs",
                type: "boolean",
                nullable: false,
                comment: "자동 전원 제어 사용 여부",
                oldClrType: typeof(bool),
                oldType: "boolean");

            migrationBuilder.AlterColumn<string>(
                name: "device_id",
                schema: "smfr",
                table: "device_configs",
                type: "text",
                nullable: false,
                comment: "장비 FK",
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AlterColumn<int>(
                name: "brightness",
                schema: "smfr",
                table: "device_configs",
                type: "integer",
                nullable: false,
                comment: "화면 밝기 (0-100)",
                oldClrType: typeof(int),
                oldType: "integer");

            migrationBuilder.AlterColumn<string>(
                name: "video_orientation",
                schema: "smfr",
                table: "device_attributes",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                comment: "영상 표현 (HORIZONTAL / VERTICAL)",
                oldClrType: typeof(string),
                oldType: "character varying(20)",
                oldMaxLength: 20);

            migrationBuilder.AlterColumn<string>(
                name: "video_id",
                schema: "smfr",
                table: "device_attributes",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true,
                comment: "재생 동영상 ID",
                oldClrType: typeof(string),
                oldType: "character varying(50)",
                oldMaxLength: 50,
                oldNullable: true);

            migrationBuilder.AlterColumn<int>(
                name: "screensaver_timeout_sec",
                schema: "smfr",
                table: "device_attributes",
                type: "integer",
                nullable: false,
                comment: "대기 화면 전환 대기 시간(초)",
                oldClrType: typeof(int),
                oldType: "integer");

            migrationBuilder.AlterColumn<string>(
                name: "remark",
                schema: "smfr",
                table: "device_attributes",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true,
                comment: "비고",
                oldClrType: typeof(string),
                oldType: "character varying(500)",
                oldMaxLength: 500,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "portrait_orientation",
                schema: "smfr",
                table: "device_attributes",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                comment: "화면 표현 (HORIZONTAL / VERTICAL)",
                oldClrType: typeof(string),
                oldType: "character varying(20)",
                oldMaxLength: 20);

            migrationBuilder.AlterColumn<string>(
                name: "photo_vertical_alignment",
                schema: "smfr",
                table: "device_attributes",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                comment: "사진 세로 정렬 (TOP / CENTER / BOTTOM) - 기본값: 상단",
                oldClrType: typeof(string),
                oldType: "character varying(20)",
                oldMaxLength: 20);

            migrationBuilder.AlterColumn<string>(
                name: "photo_horizontal_alignment",
                schema: "smfr",
                table: "device_attributes",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                comment: "사진 가로 정렬 (LEFT / CENTER / RIGHT) - 기본값: 중앙",
                oldClrType: typeof(string),
                oldType: "character varying(20)",
                oldMaxLength: 20);

            migrationBuilder.AlterColumn<int>(
                name: "notice_scroll_speed",
                schema: "smfr",
                table: "device_attributes",
                type: "integer",
                nullable: false,
                comment: "공지사항 스크롤 속도 (1=느림, 5=빠름)",
                oldClrType: typeof(int),
                oldType: "integer");

            migrationBuilder.AlterColumn<int>(
                name: "music_volume",
                schema: "smfr",
                table: "device_attributes",
                type: "integer",
                nullable: true,
                comment: "음악 재생 볼륨 (0-100, null이면 장비 기본값 사용)",
                oldClrType: typeof(int),
                oldType: "integer",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "music_id",
                schema: "smfr",
                table: "device_attributes",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true,
                comment: "재생 음악 ID",
                oldClrType: typeof(string),
                oldType: "character varying(50)",
                oldMaxLength: 50,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "memorial_photo_effect",
                schema: "smfr",
                table: "device_attributes",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                comment: "영정사진 표시 효과 (FADE / SLIDE / NONE)",
                oldClrType: typeof(string),
                oldType: "character varying(20)",
                oldMaxLength: 20);

            migrationBuilder.AlterColumn<decimal>(
                name: "memorial_padding_top",
                schema: "smfr",
                table: "device_attributes",
                type: "numeric",
                nullable: true,
                comment: "영정사진 여백 (위 %)",
                oldClrType: typeof(decimal),
                oldType: "numeric",
                oldNullable: true);

            migrationBuilder.AlterColumn<decimal>(
                name: "memorial_padding_right",
                schema: "smfr",
                table: "device_attributes",
                type: "numeric",
                nullable: true,
                comment: "영정사진 여백 (우 %)",
                oldClrType: typeof(decimal),
                oldType: "numeric",
                oldNullable: true);

            migrationBuilder.AlterColumn<decimal>(
                name: "memorial_padding_left",
                schema: "smfr",
                table: "device_attributes",
                type: "numeric",
                nullable: true,
                comment: "영정사진 여백 (좌 %)",
                oldClrType: typeof(decimal),
                oldType: "numeric",
                oldNullable: true);

            migrationBuilder.AlterColumn<decimal>(
                name: "memorial_padding_bottom",
                schema: "smfr",
                table: "device_attributes",
                type: "numeric",
                nullable: true,
                comment: "영정사진 여백 (아래 %)",
                oldClrType: typeof(decimal),
                oldType: "numeric",
                oldNullable: true);

            migrationBuilder.AlterColumn<bool>(
                name: "is_video_enabled",
                schema: "smfr",
                table: "device_attributes",
                type: "boolean",
                nullable: false,
                comment: "동영상 재생 활성화 여부",
                oldClrType: typeof(bool),
                oldType: "boolean");

            migrationBuilder.AlterColumn<bool>(
                name: "is_touch_enabled",
                schema: "smfr",
                table: "device_attributes",
                type: "boolean",
                nullable: false,
                comment: "터치 인터랙션 활성화 여부",
                oldClrType: typeof(bool),
                oldType: "boolean");

            migrationBuilder.AlterColumn<bool>(
                name: "is_screensaver_enabled",
                schema: "smfr",
                table: "device_attributes",
                type: "boolean",
                nullable: false,
                comment: "대기 화면 활성화 여부",
                oldClrType: typeof(bool),
                oldType: "boolean");

            migrationBuilder.AlterColumn<bool>(
                name: "is_room_assignment_visible",
                schema: "smfr",
                table: "device_attributes",
                type: "boolean",
                nullable: false,
                comment: "빈소 배정 현황 표시 여부",
                oldClrType: typeof(bool),
                oldType: "boolean");

            migrationBuilder.AlterColumn<bool>(
                name: "is_qr_code_visible",
                schema: "smfr",
                table: "device_attributes",
                type: "boolean",
                nullable: false,
                comment: "QR코드 표시 여부",
                oldClrType: typeof(bool),
                oldType: "boolean");

            migrationBuilder.AlterColumn<bool>(
                name: "is_notice_visible",
                schema: "smfr",
                table: "device_attributes",
                type: "boolean",
                nullable: false,
                comment: "공지사항 표시 여부",
                oldClrType: typeof(bool),
                oldType: "boolean");

            migrationBuilder.AlterColumn<bool>(
                name: "is_muted",
                schema: "smfr",
                table: "device_attributes",
                type: "boolean",
                nullable: false,
                comment: "음소거 여부",
                oldClrType: typeof(bool),
                oldType: "boolean");

            migrationBuilder.AlterColumn<bool>(
                name: "is_music_enabled",
                schema: "smfr",
                table: "device_attributes",
                type: "boolean",
                nullable: false,
                comment: "음악 재생 활성화 여부",
                oldClrType: typeof(bool),
                oldType: "boolean");

            migrationBuilder.AlterColumn<bool>(
                name: "is_memorial_photo_enabled",
                schema: "smfr",
                table: "device_attributes",
                type: "boolean",
                nullable: false,
                comment: "영정사진 표시 여부",
                oldClrType: typeof(bool),
                oldType: "boolean");

            migrationBuilder.AlterColumn<bool>(
                name: "is_media_loop",
                schema: "smfr",
                table: "device_attributes",
                type: "boolean",
                nullable: false,
                comment: "동영상/음악 반복 재생 여부",
                oldClrType: typeof(bool),
                oldType: "boolean");

            migrationBuilder.AlterColumn<bool>(
                name: "is_floor_guide_enabled",
                schema: "smfr",
                table: "device_attributes",
                type: "boolean",
                nullable: false,
                comment: "층별 안내판 표시 활성화 여부",
                oldClrType: typeof(bool),
                oldType: "boolean");

            migrationBuilder.AlterColumn<bool>(
                name: "is_family_contact_visible",
                schema: "smfr",
                table: "device_attributes",
                type: "boolean",
                nullable: false,
                comment: "유족 연락처 표시 여부",
                oldClrType: typeof(bool),
                oldType: "boolean");

            migrationBuilder.AlterColumn<bool>(
                name: "is_deceased_name_visible",
                schema: "smfr",
                table: "device_attributes",
                type: "boolean",
                nullable: false,
                comment: "고인 이름 표시 여부",
                oldClrType: typeof(bool),
                oldType: "boolean");

            migrationBuilder.AlterColumn<bool>(
                name: "is_building_map_visible",
                schema: "smfr",
                table: "device_attributes",
                type: "boolean",
                nullable: false,
                comment: "건물 전체 안내도 표시 여부",
                oldClrType: typeof(bool),
                oldType: "boolean");

            migrationBuilder.AlterColumn<bool>(
                name: "is_active_rooms_only",
                schema: "smfr",
                table: "device_attributes",
                type: "boolean",
                nullable: false,
                comment: "현재 진행 중인 빈소 목록만 표시 여부",
                oldClrType: typeof(bool),
                oldType: "boolean");

            migrationBuilder.AlterColumn<int>(
                name: "floor_guide_refresh_sec",
                schema: "smfr",
                table: "device_attributes",
                type: "integer",
                nullable: false,
                comment: "층별 안내 새로고침 간격(초)",
                oldClrType: typeof(int),
                oldType: "integer");

            migrationBuilder.AlterColumn<string>(
                name: "entrance_greeting",
                schema: "smfr",
                table: "device_attributes",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true,
                comment: "입구 인사말 메시지",
                oldClrType: typeof(string),
                oldType: "character varying(200)",
                oldMaxLength: 200,
                oldNullable: true);

            migrationBuilder.AlterColumn<decimal>(
                name: "display_padding_top",
                schema: "smfr",
                table: "device_attributes",
                type: "numeric",
                nullable: true,
                comment: "전체 화면 여백 (위 %)",
                oldClrType: typeof(decimal),
                oldType: "numeric",
                oldNullable: true);

            migrationBuilder.AlterColumn<decimal>(
                name: "display_padding_right",
                schema: "smfr",
                table: "device_attributes",
                type: "numeric",
                nullable: true,
                comment: "전체 화면 여백 (우 %)",
                oldClrType: typeof(decimal),
                oldType: "numeric",
                oldNullable: true);

            migrationBuilder.AlterColumn<decimal>(
                name: "display_padding_left",
                schema: "smfr",
                table: "device_attributes",
                type: "numeric",
                nullable: true,
                comment: "전체 화면 여백 (좌 %)",
                oldClrType: typeof(decimal),
                oldType: "numeric",
                oldNullable: true);

            migrationBuilder.AlterColumn<decimal>(
                name: "display_padding_bottom",
                schema: "smfr",
                table: "device_attributes",
                type: "numeric",
                nullable: true,
                comment: "전체 화면 여백 (아래 %)",
                oldClrType: typeof(decimal),
                oldType: "numeric",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "display_orientation",
                schema: "smfr",
                table: "device_attributes",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                comment: "표시 방향 (LANDSCAPE / PORTRAIT)",
                oldClrType: typeof(string),
                oldType: "character varying(20)",
                oldMaxLength: 20);

            migrationBuilder.AlterColumn<string>(
                name: "device_id",
                schema: "smfr",
                table: "device_attributes",
                type: "text",
                nullable: false,
                comment: "장비 FK",
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AlterColumn<int>(
                name: "content_interval_sec",
                schema: "smfr",
                table: "device_attributes",
                type: "integer",
                nullable: false,
                comment: "콘텐츠 전환 간격(초)",
                oldClrType: typeof(int),
                oldType: "integer");

            migrationBuilder.AlterColumn<string>(
                name: "zip_code",
                schema: "smfr",
                table: "buildings",
                type: "text",
                nullable: true,
                comment: "우편번호",
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true);

            migrationBuilder.AlterColumn<int>(
                name: "sort_order",
                schema: "smfr",
                table: "buildings",
                type: "integer",
                nullable: false,
                comment: "정렬 순서",
                oldClrType: typeof(int),
                oldType: "integer");

            migrationBuilder.AlterColumn<string>(
                name: "short_name",
                schema: "smfr",
                table: "buildings",
                type: "text",
                nullable: true,
                comment: "짧은건물명",
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "remark",
                schema: "smfr",
                table: "buildings",
                type: "text",
                nullable: true,
                comment: "비고/설명",
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "parking_photo_group_id",
                schema: "smfr",
                table: "buildings",
                type: "text",
                nullable: true,
                comment: "주차장 안내 이미지 파일그룹 ID",
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "name",
                schema: "smfr",
                table: "buildings",
                type: "text",
                nullable: false,
                comment: "건물명",
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AlterColumn<string>(
                name: "company_id",
                schema: "smfr",
                table: "buildings",
                type: "text",
                nullable: false,
                comment: "소속 회사 ID",
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AlterColumn<string>(
                name: "building_photo_group_id",
                schema: "smfr",
                table: "buildings",
                type: "text",
                nullable: true,
                comment: "건물 전경 사진 파일그룹 ID",
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "address_detail",
                schema: "smfr",
                table: "buildings",
                type: "text",
                nullable: true,
                comment: "상세주소",
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "address",
                schema: "smfr",
                table: "buildings",
                type: "text",
                nullable: true,
                comment: "주소",
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "abbreviation",
                schema: "smfr",
                table: "buildings",
                type: "character varying(3)",
                maxLength: 3,
                nullable: true,
                comment: "건물 약어 (3자리 영문 대문자)",
                oldClrType: typeof(string),
                oldType: "character varying(3)",
                oldMaxLength: 3,
                oldNullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterTable(
                name: "rooms",
                schema: "smfr",
                oldComment: "호실 엔티티");

            migrationBuilder.AlterTable(
                name: "media_sources",
                schema: "smfr",
                oldComment: "미디어 소스 (동영상, 음원, 이미지 등) 리소스를 저장하는 엔티티 클래스");

            migrationBuilder.AlterTable(
                name: "floors",
                schema: "smfr",
                oldComment: "층 엔티티");

            migrationBuilder.AlterTable(
                name: "devices",
                schema: "smfr",
                oldComment: "장비 정보");

            migrationBuilder.AlterTable(
                name: "device_text_overlays",
                schema: "smfr",
                oldComment: "장비 텍스트 오버레이 설정 엔티티\n            장비(모니터) 화면의 특정 위치에 텍스트를 배치하는 설정을 관리합니다.\n            위치 및 크기 값은 % 단위 (소수점 3자리)로 저장되어 다양한 해상도에서도 정확한 위치를 보장합니다.");

            migrationBuilder.AlterTable(
                name: "device_ribbons",
                schema: "smfr",
                oldComment: "장비 리본 설정 엔티티\n            장비(모니터) 화면의 특정 위치에 장식(리본) 이미지를 배치하는 설정을 관리합니다.\n            위치 및 크기 값은 % 단위 (소수점 3자리)로 저장되어 다양한 해상도에서도 정확한 위치를 보장합니다.");

            migrationBuilder.AlterTable(
                name: "device_configs",
                schema: "smfr",
                oldComment: "장비 기본 설정 (Device 1:1 관계)\n            음량, 밝기, 자동 전원, 재시작 시각 등을 관리합니다.");

            migrationBuilder.AlterTable(
                name: "device_attributes",
                schema: "smfr",
                oldComment: "장비 속성 정보 (Device 1:N 관계)\n            장비 유형별 세부 설정 값을 key-value 형태 또는 구조화된 컬럼으로 관리합니다.");

            migrationBuilder.AlterTable(
                name: "deceaseds",
                schema: "smfr",
                oldComment: "고인 정보 관리 엔티티");

            migrationBuilder.AlterTable(
                name: "deceased_rooms",
                schema: "smfr",
                oldComment: "고인별 호실(빈소) 배정 및 사용 기간 이력 관리 엔티티");

            migrationBuilder.AlterTable(
                name: "deceased_mourners",
                schema: "smfr",
                oldComment: "고인 상주 정보 관리 엔티티");

            migrationBuilder.AlterTable(
                name: "deceased_managers",
                schema: "smfr",
                oldComment: "고인별 장례 담당 임직원 및 지도사 매핑 엔티티");

            migrationBuilder.AlterTable(
                name: "deceased_facilities",
                schema: "smfr",
                oldComment: "고인별 장례 시설(안치실, 염습실, 영결식장 등) 이용 내역 엔티티");

            migrationBuilder.AlterTable(
                name: "deceased_contractors",
                schema: "smfr",
                oldComment: "고인 장례 계약자 정보 관리 엔티티");

            migrationBuilder.AlterTable(
                name: "buildings",
                schema: "smfr",
                oldComment: "건물 엔티티");

            migrationBuilder.AlterColumn<string>(
                name: "status",
                schema: "smfr",
                table: "rooms",
                type: "text",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text",
                oldComment: "상태 (ACTIVE, INACTIVE)");

            migrationBuilder.AlterColumn<int>(
                name: "sort_order",
                schema: "smfr",
                table: "rooms",
                type: "integer",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "integer",
                oldComment: "정렬 순서");

            migrationBuilder.AlterColumn<string>(
                name: "short_name",
                schema: "smfr",
                table: "rooms",
                type: "text",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true,
                oldComment: "짧은 명칭");

            migrationBuilder.AlterColumn<string>(
                name: "room_type",
                schema: "smfr",
                table: "rooms",
                type: "text",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text",
                oldComment: "호실 타입 (예: 빈소, 안치실, 참관실 등)");

            migrationBuilder.AlterColumn<string>(
                name: "remark",
                schema: "smfr",
                table: "rooms",
                type: "text",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true,
                oldComment: "비고/설명");

            migrationBuilder.AlterColumn<string>(
                name: "name",
                schema: "smfr",
                table: "rooms",
                type: "text",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text",
                oldComment: "호실 명칭");

            migrationBuilder.AlterColumn<string>(
                name: "floor_id",
                schema: "smfr",
                table: "rooms",
                type: "text",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text",
                oldComment: "소속 층 ID");

            migrationBuilder.AlterColumn<string>(
                name: "building_id",
                schema: "smfr",
                table: "rooms",
                type: "text",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text",
                oldComment: "소속 건물 ID");

            migrationBuilder.AlterColumn<string>(
                name: "webmurl",
                schema: "smfr",
                table: "media_sources",
                type: "text",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true,
                oldComment: "동영상인 경우 WebM 파일의 파일 URL 경로");

            migrationBuilder.AlterColumn<Guid>(
                name: "webmfileid",
                schema: "smfr",
                table: "media_sources",
                type: "uuid",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true,
                oldComment: "WebM 파일의 FileMetadata 식별자");

            migrationBuilder.AlterColumn<string>(
                name: "url",
                schema: "smfr",
                table: "media_sources",
                type: "text",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text",
                oldComment: "파일 URL 경로 (FileServer 등 보관 경로)");

            migrationBuilder.AlterColumn<string>(
                name: "thumbnailurl",
                schema: "smfr",
                table: "media_sources",
                type: "text",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true,
                oldComment: "동영상인 경우 첫 클립 썸네일 이미지의 파일 URL 경로");

            migrationBuilder.AlterColumn<Guid>(
                name: "thumbnailfileid",
                schema: "smfr",
                table: "media_sources",
                type: "uuid",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true,
                oldComment: "썸네일 이미지 파일의 FileMetadata 식별자");

            migrationBuilder.AlterColumn<string>(
                name: "status",
                schema: "smfr",
                table: "media_sources",
                type: "text",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text",
                oldComment: "미디어 변환 상태 (PROCESSING, COMPLETED, FAILED)");

            migrationBuilder.AlterColumn<string>(
                name: "sourcetype",
                schema: "smfr",
                table: "media_sources",
                type: "text",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text",
                oldComment: "미디어 유형 (VIDEO, AUDIO, IMAGE)");

            migrationBuilder.AlterColumn<int>(
                name: "sortorder",
                schema: "smfr",
                table: "media_sources",
                type: "integer",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "integer",
                oldComment: "정렬 순서");

            migrationBuilder.AlterColumn<string>(
                name: "shortname",
                schema: "smfr",
                table: "media_sources",
                type: "text",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true,
                oldComment: "영상 짧은 명칭");

            migrationBuilder.AlterColumn<string>(
                name: "remark",
                schema: "smfr",
                table: "media_sources",
                type: "text",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true,
                oldComment: "설명 및 비고");

            migrationBuilder.AlterColumn<Guid>(
                name: "originalfileid",
                schema: "smfr",
                table: "media_sources",
                type: "uuid",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true,
                oldComment: "원본 파일의 FileMetadata 식별자");

            migrationBuilder.AlterColumn<string>(
                name: "oggurl",
                schema: "smfr",
                table: "media_sources",
                type: "text",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true,
                oldComment: "음원인 경우 OGG 파일의 파일 URL 경로");

            migrationBuilder.AlterColumn<Guid>(
                name: "oggfileid",
                schema: "smfr",
                table: "media_sources",
                type: "uuid",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true,
                oldComment: "OGG 파일의 FileMetadata 식별자");

            migrationBuilder.AlterColumn<string>(
                name: "name",
                schema: "smfr",
                table: "media_sources",
                type: "text",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text",
                oldComment: "미디어 소스 명칭");

            migrationBuilder.AlterColumn<bool>(
                name: "haswebm",
                schema: "smfr",
                table: "media_sources",
                type: "boolean",
                nullable: false,
                oldClrType: typeof(bool),
                oldType: "boolean",
                oldComment: "WebM 파일 변환 완료 여부");

            migrationBuilder.AlterColumn<bool>(
                name: "hasthumbnail",
                schema: "smfr",
                table: "media_sources",
                type: "boolean",
                nullable: false,
                oldClrType: typeof(bool),
                oldType: "boolean",
                oldComment: "썸네일 이미지 생성 완료 여부");

            migrationBuilder.AlterColumn<bool>(
                name: "hasogg",
                schema: "smfr",
                table: "media_sources",
                type: "boolean",
                nullable: false,
                oldClrType: typeof(bool),
                oldType: "boolean",
                oldComment: "OGG 파일 변환 완료 여부");

            migrationBuilder.AlterColumn<bool>(
                name: "hasaac",
                schema: "smfr",
                table: "media_sources",
                type: "boolean",
                nullable: false,
                oldClrType: typeof(bool),
                oldType: "boolean",
                oldComment: "AAC 파일 변환 완료 여부");

            migrationBuilder.AlterColumn<long>(
                name: "filesize",
                schema: "smfr",
                table: "media_sources",
                type: "bigint",
                nullable: true,
                oldClrType: typeof(long),
                oldType: "bigint",
                oldNullable: true,
                oldComment: "파일 크기 (Bytes)");

            migrationBuilder.AlterColumn<string>(
                name: "errormessage",
                schema: "smfr",
                table: "media_sources",
                type: "text",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true,
                oldComment: "미디어 변환 실패 시 발생한 에러 메시지");

            migrationBuilder.AlterColumn<DateTime>(
                name: "conversionstartedat",
                schema: "smfr",
                table: "media_sources",
                type: "timestamp with time zone",
                nullable: true,
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone",
                oldNullable: true,
                oldComment: "변환 시작 일시");

            migrationBuilder.AlterColumn<DateTime>(
                name: "conversioncompletedat",
                schema: "smfr",
                table: "media_sources",
                type: "timestamp with time zone",
                nullable: true,
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone",
                oldNullable: true,
                oldComment: "변환 완료 일시");

            migrationBuilder.AlterColumn<string>(
                name: "conversioncommand",
                schema: "smfr",
                table: "media_sources",
                type: "text",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true,
                oldComment: "변환 시 사용된 FFmpeg 명령어");

            migrationBuilder.AlterColumn<string>(
                name: "aacurl",
                schema: "smfr",
                table: "media_sources",
                type: "text",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true,
                oldComment: "음원인 경우 AAC 파일의 파일 URL 경로");

            migrationBuilder.AlterColumn<Guid>(
                name: "aacfileid",
                schema: "smfr",
                table: "media_sources",
                type: "uuid",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true,
                oldComment: "AAC 파일의 FileMetadata 식별자");

            migrationBuilder.AlterColumn<int>(
                name: "sort_order",
                schema: "smfr",
                table: "floors",
                type: "integer",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "integer",
                oldComment: "정렬 순서");

            migrationBuilder.AlterColumn<string>(
                name: "remark",
                schema: "smfr",
                table: "floors",
                type: "text",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true,
                oldComment: "비고/설명");

            migrationBuilder.AlterColumn<string>(
                name: "name",
                schema: "smfr",
                table: "floors",
                type: "text",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text",
                oldComment: "층 명칭");

            migrationBuilder.AlterColumn<string>(
                name: "building_id",
                schema: "smfr",
                table: "floors",
                type: "text",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text",
                oldComment: "소속 건물 ID");

            migrationBuilder.AlterColumn<int>(
                name: "sort_order",
                schema: "smfr",
                table: "devices",
                type: "integer",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "integer",
                oldComment: "정렬 순서");

            migrationBuilder.AlterColumn<decimal>(
                name: "width",
                schema: "smfr",
                table: "device_text_overlays",
                type: "numeric(6,3)",
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "numeric(6,3)",
                oldComment: "너비 (%, 소수점 3자리)");

            migrationBuilder.AlterColumn<string>(
                name: "text_content",
                schema: "smfr",
                table: "device_text_overlays",
                type: "character varying(500)",
                maxLength: 500,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(500)",
                oldMaxLength: 500,
                oldComment: "표시할 텍스트 내용");

            migrationBuilder.AlterColumn<string>(
                name: "text_align",
                schema: "smfr",
                table: "device_text_overlays",
                type: "character varying(10)",
                maxLength: 10,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(10)",
                oldMaxLength: 10,
                oldComment: "텍스트 정렬 (left | center | right)");

            migrationBuilder.AlterColumn<int>(
                name: "sort_order",
                schema: "smfr",
                table: "device_text_overlays",
                type: "integer",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "integer",
                oldComment: "정렬 순서");

            migrationBuilder.AlterColumn<string>(
                name: "remark",
                schema: "smfr",
                table: "device_text_overlays",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(500)",
                oldMaxLength: 500,
                oldNullable: true,
                oldComment: "비고");

            migrationBuilder.AlterColumn<decimal>(
                name: "position_top",
                schema: "smfr",
                table: "device_text_overlays",
                type: "numeric(6,3)",
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "numeric(6,3)",
                oldComment: "상단 위치 (%, 소수점 3자리)");

            migrationBuilder.AlterColumn<decimal>(
                name: "position_left",
                schema: "smfr",
                table: "device_text_overlays",
                type: "numeric(6,3)",
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "numeric(6,3)",
                oldComment: "좌측 위치 (%, 소수점 3자리)");

            migrationBuilder.AlterColumn<decimal>(
                name: "height",
                schema: "smfr",
                table: "device_text_overlays",
                type: "numeric(6,3)",
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "numeric(6,3)",
                oldComment: "높이 (%, 소수점 3자리)");

            migrationBuilder.AlterColumn<string>(
                name: "font_weight",
                schema: "smfr",
                table: "device_text_overlays",
                type: "character varying(10)",
                maxLength: 10,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(10)",
                oldMaxLength: 10,
                oldComment: "폰트 굵기 (normal | bold)");

            migrationBuilder.AlterColumn<decimal>(
                name: "font_size",
                schema: "smfr",
                table: "device_text_overlays",
                type: "numeric(6,3)",
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "numeric(6,3)",
                oldComment: "폰트 크기 (px 단위 기준, 화면 높이 대비 %로 저장)");

            migrationBuilder.AlterColumn<string>(
                name: "font_color",
                schema: "smfr",
                table: "device_text_overlays",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(20)",
                oldMaxLength: 20,
                oldComment: "폰트 색상 (CSS hex 색상값, 예: #FFFFFF)");

            migrationBuilder.AlterColumn<string>(
                name: "device_id",
                schema: "smfr",
                table: "device_text_overlays",
                type: "character varying(50)",
                maxLength: 50,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(50)",
                oldMaxLength: 50,
                oldComment: "장비 FK");

            migrationBuilder.AlterColumn<string>(
                name: "background_color",
                schema: "smfr",
                table: "device_text_overlays",
                type: "character varying(30)",
                maxLength: 30,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(30)",
                oldMaxLength: 30,
                oldComment: "배경 색상 (CSS hex 색상값 또는 'transparent')");

            migrationBuilder.AlterColumn<decimal>(
                name: "width",
                schema: "smfr",
                table: "device_ribbons",
                type: "numeric(6,3)",
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "numeric(6,3)",
                oldComment: "너비 (%, 소수점 3자리)");

            migrationBuilder.AlterColumn<int>(
                name: "sort_order",
                schema: "smfr",
                table: "device_ribbons",
                type: "integer",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "integer",
                oldComment: "정렬 순서");

            migrationBuilder.AlterColumn<string>(
                name: "remark",
                schema: "smfr",
                table: "device_ribbons",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(500)",
                oldMaxLength: 500,
                oldNullable: true,
                oldComment: "비고");

            migrationBuilder.AlterColumn<decimal>(
                name: "position_top",
                schema: "smfr",
                table: "device_ribbons",
                type: "numeric(6,3)",
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "numeric(6,3)",
                oldComment: "상단 위치 (%, 소수점 3자리)");

            migrationBuilder.AlterColumn<decimal>(
                name: "position_left",
                schema: "smfr",
                table: "device_ribbons",
                type: "numeric(6,3)",
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "numeric(6,3)",
                oldComment: "좌측 위치 (%, 소수점 3자리)");

            migrationBuilder.AlterColumn<string>(
                name: "media_source_id",
                schema: "smfr",
                table: "device_ribbons",
                type: "character varying(50)",
                maxLength: 50,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(50)",
                oldMaxLength: 50,
                oldComment: "장식(미디어소스) FK - MediaSource.SourceType = IMAGE");

            migrationBuilder.AlterColumn<decimal>(
                name: "height",
                schema: "smfr",
                table: "device_ribbons",
                type: "numeric(6,3)",
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "numeric(6,3)",
                oldComment: "높이 (%, 소수점 3자리)");

            migrationBuilder.AlterColumn<string>(
                name: "device_id",
                schema: "smfr",
                table: "device_ribbons",
                type: "character varying(50)",
                maxLength: 50,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(50)",
                oldMaxLength: 50,
                oldComment: "장비 FK");

            migrationBuilder.AlterColumn<int>(
                name: "volume",
                schema: "smfr",
                table: "device_configs",
                type: "integer",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "integer",
                oldComment: "기기 음량 (0-100)");

            migrationBuilder.AlterColumn<string>(
                name: "reboot_time",
                schema: "smfr",
                table: "device_configs",
                type: "character varying(5)",
                maxLength: 5,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(5)",
                oldMaxLength: 5,
                oldNullable: true,
                oldComment: "일일 자동 재시작 시각 (HH:mm)");

            migrationBuilder.AlterColumn<string>(
                name: "power_on_time",
                schema: "smfr",
                table: "device_configs",
                type: "character varying(5)",
                maxLength: 5,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(5)",
                oldMaxLength: 5,
                oldNullable: true,
                oldComment: "자동 켜짐 시각 (HH:mm)");

            migrationBuilder.AlterColumn<string>(
                name: "power_off_time",
                schema: "smfr",
                table: "device_configs",
                type: "character varying(5)",
                maxLength: 5,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(5)",
                oldMaxLength: 5,
                oldNullable: true,
                oldComment: "자동 꺼짐 시각 (HH:mm)");

            migrationBuilder.AlterColumn<bool>(
                name: "is_auto_power",
                schema: "smfr",
                table: "device_configs",
                type: "boolean",
                nullable: false,
                oldClrType: typeof(bool),
                oldType: "boolean",
                oldComment: "자동 전원 제어 사용 여부");

            migrationBuilder.AlterColumn<string>(
                name: "device_id",
                schema: "smfr",
                table: "device_configs",
                type: "text",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text",
                oldComment: "장비 FK");

            migrationBuilder.AlterColumn<int>(
                name: "brightness",
                schema: "smfr",
                table: "device_configs",
                type: "integer",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "integer",
                oldComment: "화면 밝기 (0-100)");

            migrationBuilder.AlterColumn<string>(
                name: "video_orientation",
                schema: "smfr",
                table: "device_attributes",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(20)",
                oldMaxLength: 20,
                oldComment: "영상 표현 (HORIZONTAL / VERTICAL)");

            migrationBuilder.AlterColumn<string>(
                name: "video_id",
                schema: "smfr",
                table: "device_attributes",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(50)",
                oldMaxLength: 50,
                oldNullable: true,
                oldComment: "재생 동영상 ID");

            migrationBuilder.AlterColumn<int>(
                name: "screensaver_timeout_sec",
                schema: "smfr",
                table: "device_attributes",
                type: "integer",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "integer",
                oldComment: "대기 화면 전환 대기 시간(초)");

            migrationBuilder.AlterColumn<string>(
                name: "remark",
                schema: "smfr",
                table: "device_attributes",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(500)",
                oldMaxLength: 500,
                oldNullable: true,
                oldComment: "비고");

            migrationBuilder.AlterColumn<string>(
                name: "portrait_orientation",
                schema: "smfr",
                table: "device_attributes",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(20)",
                oldMaxLength: 20,
                oldComment: "화면 표현 (HORIZONTAL / VERTICAL)");

            migrationBuilder.AlterColumn<string>(
                name: "photo_vertical_alignment",
                schema: "smfr",
                table: "device_attributes",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(20)",
                oldMaxLength: 20,
                oldComment: "사진 세로 정렬 (TOP / CENTER / BOTTOM) - 기본값: 상단");

            migrationBuilder.AlterColumn<string>(
                name: "photo_horizontal_alignment",
                schema: "smfr",
                table: "device_attributes",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(20)",
                oldMaxLength: 20,
                oldComment: "사진 가로 정렬 (LEFT / CENTER / RIGHT) - 기본값: 중앙");

            migrationBuilder.AlterColumn<int>(
                name: "notice_scroll_speed",
                schema: "smfr",
                table: "device_attributes",
                type: "integer",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "integer",
                oldComment: "공지사항 스크롤 속도 (1=느림, 5=빠름)");

            migrationBuilder.AlterColumn<int>(
                name: "music_volume",
                schema: "smfr",
                table: "device_attributes",
                type: "integer",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "integer",
                oldNullable: true,
                oldComment: "음악 재생 볼륨 (0-100, null이면 장비 기본값 사용)");

            migrationBuilder.AlterColumn<string>(
                name: "music_id",
                schema: "smfr",
                table: "device_attributes",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(50)",
                oldMaxLength: 50,
                oldNullable: true,
                oldComment: "재생 음악 ID");

            migrationBuilder.AlterColumn<string>(
                name: "memorial_photo_effect",
                schema: "smfr",
                table: "device_attributes",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(20)",
                oldMaxLength: 20,
                oldComment: "영정사진 표시 효과 (FADE / SLIDE / NONE)");

            migrationBuilder.AlterColumn<decimal>(
                name: "memorial_padding_top",
                schema: "smfr",
                table: "device_attributes",
                type: "numeric",
                nullable: true,
                oldClrType: typeof(decimal),
                oldType: "numeric",
                oldNullable: true,
                oldComment: "영정사진 여백 (위 %)");

            migrationBuilder.AlterColumn<decimal>(
                name: "memorial_padding_right",
                schema: "smfr",
                table: "device_attributes",
                type: "numeric",
                nullable: true,
                oldClrType: typeof(decimal),
                oldType: "numeric",
                oldNullable: true,
                oldComment: "영정사진 여백 (우 %)");

            migrationBuilder.AlterColumn<decimal>(
                name: "memorial_padding_left",
                schema: "smfr",
                table: "device_attributes",
                type: "numeric",
                nullable: true,
                oldClrType: typeof(decimal),
                oldType: "numeric",
                oldNullable: true,
                oldComment: "영정사진 여백 (좌 %)");

            migrationBuilder.AlterColumn<decimal>(
                name: "memorial_padding_bottom",
                schema: "smfr",
                table: "device_attributes",
                type: "numeric",
                nullable: true,
                oldClrType: typeof(decimal),
                oldType: "numeric",
                oldNullable: true,
                oldComment: "영정사진 여백 (아래 %)");

            migrationBuilder.AlterColumn<bool>(
                name: "is_video_enabled",
                schema: "smfr",
                table: "device_attributes",
                type: "boolean",
                nullable: false,
                oldClrType: typeof(bool),
                oldType: "boolean",
                oldComment: "동영상 재생 활성화 여부");

            migrationBuilder.AlterColumn<bool>(
                name: "is_touch_enabled",
                schema: "smfr",
                table: "device_attributes",
                type: "boolean",
                nullable: false,
                oldClrType: typeof(bool),
                oldType: "boolean",
                oldComment: "터치 인터랙션 활성화 여부");

            migrationBuilder.AlterColumn<bool>(
                name: "is_screensaver_enabled",
                schema: "smfr",
                table: "device_attributes",
                type: "boolean",
                nullable: false,
                oldClrType: typeof(bool),
                oldType: "boolean",
                oldComment: "대기 화면 활성화 여부");

            migrationBuilder.AlterColumn<bool>(
                name: "is_room_assignment_visible",
                schema: "smfr",
                table: "device_attributes",
                type: "boolean",
                nullable: false,
                oldClrType: typeof(bool),
                oldType: "boolean",
                oldComment: "빈소 배정 현황 표시 여부");

            migrationBuilder.AlterColumn<bool>(
                name: "is_qr_code_visible",
                schema: "smfr",
                table: "device_attributes",
                type: "boolean",
                nullable: false,
                oldClrType: typeof(bool),
                oldType: "boolean",
                oldComment: "QR코드 표시 여부");

            migrationBuilder.AlterColumn<bool>(
                name: "is_notice_visible",
                schema: "smfr",
                table: "device_attributes",
                type: "boolean",
                nullable: false,
                oldClrType: typeof(bool),
                oldType: "boolean",
                oldComment: "공지사항 표시 여부");

            migrationBuilder.AlterColumn<bool>(
                name: "is_muted",
                schema: "smfr",
                table: "device_attributes",
                type: "boolean",
                nullable: false,
                oldClrType: typeof(bool),
                oldType: "boolean",
                oldComment: "음소거 여부");

            migrationBuilder.AlterColumn<bool>(
                name: "is_music_enabled",
                schema: "smfr",
                table: "device_attributes",
                type: "boolean",
                nullable: false,
                oldClrType: typeof(bool),
                oldType: "boolean",
                oldComment: "음악 재생 활성화 여부");

            migrationBuilder.AlterColumn<bool>(
                name: "is_memorial_photo_enabled",
                schema: "smfr",
                table: "device_attributes",
                type: "boolean",
                nullable: false,
                oldClrType: typeof(bool),
                oldType: "boolean",
                oldComment: "영정사진 표시 여부");

            migrationBuilder.AlterColumn<bool>(
                name: "is_media_loop",
                schema: "smfr",
                table: "device_attributes",
                type: "boolean",
                nullable: false,
                oldClrType: typeof(bool),
                oldType: "boolean",
                oldComment: "동영상/음악 반복 재생 여부");

            migrationBuilder.AlterColumn<bool>(
                name: "is_floor_guide_enabled",
                schema: "smfr",
                table: "device_attributes",
                type: "boolean",
                nullable: false,
                oldClrType: typeof(bool),
                oldType: "boolean",
                oldComment: "층별 안내판 표시 활성화 여부");

            migrationBuilder.AlterColumn<bool>(
                name: "is_family_contact_visible",
                schema: "smfr",
                table: "device_attributes",
                type: "boolean",
                nullable: false,
                oldClrType: typeof(bool),
                oldType: "boolean",
                oldComment: "유족 연락처 표시 여부");

            migrationBuilder.AlterColumn<bool>(
                name: "is_deceased_name_visible",
                schema: "smfr",
                table: "device_attributes",
                type: "boolean",
                nullable: false,
                oldClrType: typeof(bool),
                oldType: "boolean",
                oldComment: "고인 이름 표시 여부");

            migrationBuilder.AlterColumn<bool>(
                name: "is_building_map_visible",
                schema: "smfr",
                table: "device_attributes",
                type: "boolean",
                nullable: false,
                oldClrType: typeof(bool),
                oldType: "boolean",
                oldComment: "건물 전체 안내도 표시 여부");

            migrationBuilder.AlterColumn<bool>(
                name: "is_active_rooms_only",
                schema: "smfr",
                table: "device_attributes",
                type: "boolean",
                nullable: false,
                oldClrType: typeof(bool),
                oldType: "boolean",
                oldComment: "현재 진행 중인 빈소 목록만 표시 여부");

            migrationBuilder.AlterColumn<int>(
                name: "floor_guide_refresh_sec",
                schema: "smfr",
                table: "device_attributes",
                type: "integer",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "integer",
                oldComment: "층별 안내 새로고침 간격(초)");

            migrationBuilder.AlterColumn<string>(
                name: "entrance_greeting",
                schema: "smfr",
                table: "device_attributes",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(200)",
                oldMaxLength: 200,
                oldNullable: true,
                oldComment: "입구 인사말 메시지");

            migrationBuilder.AlterColumn<decimal>(
                name: "display_padding_top",
                schema: "smfr",
                table: "device_attributes",
                type: "numeric",
                nullable: true,
                oldClrType: typeof(decimal),
                oldType: "numeric",
                oldNullable: true,
                oldComment: "전체 화면 여백 (위 %)");

            migrationBuilder.AlterColumn<decimal>(
                name: "display_padding_right",
                schema: "smfr",
                table: "device_attributes",
                type: "numeric",
                nullable: true,
                oldClrType: typeof(decimal),
                oldType: "numeric",
                oldNullable: true,
                oldComment: "전체 화면 여백 (우 %)");

            migrationBuilder.AlterColumn<decimal>(
                name: "display_padding_left",
                schema: "smfr",
                table: "device_attributes",
                type: "numeric",
                nullable: true,
                oldClrType: typeof(decimal),
                oldType: "numeric",
                oldNullable: true,
                oldComment: "전체 화면 여백 (좌 %)");

            migrationBuilder.AlterColumn<decimal>(
                name: "display_padding_bottom",
                schema: "smfr",
                table: "device_attributes",
                type: "numeric",
                nullable: true,
                oldClrType: typeof(decimal),
                oldType: "numeric",
                oldNullable: true,
                oldComment: "전체 화면 여백 (아래 %)");

            migrationBuilder.AlterColumn<string>(
                name: "display_orientation",
                schema: "smfr",
                table: "device_attributes",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(20)",
                oldMaxLength: 20,
                oldComment: "표시 방향 (LANDSCAPE / PORTRAIT)");

            migrationBuilder.AlterColumn<string>(
                name: "device_id",
                schema: "smfr",
                table: "device_attributes",
                type: "text",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text",
                oldComment: "장비 FK");

            migrationBuilder.AlterColumn<int>(
                name: "content_interval_sec",
                schema: "smfr",
                table: "device_attributes",
                type: "integer",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "integer",
                oldComment: "콘텐츠 전환 간격(초)");

            migrationBuilder.AlterColumn<string>(
                name: "zip_code",
                schema: "smfr",
                table: "buildings",
                type: "text",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true,
                oldComment: "우편번호");

            migrationBuilder.AlterColumn<int>(
                name: "sort_order",
                schema: "smfr",
                table: "buildings",
                type: "integer",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "integer",
                oldComment: "정렬 순서");

            migrationBuilder.AlterColumn<string>(
                name: "short_name",
                schema: "smfr",
                table: "buildings",
                type: "text",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true,
                oldComment: "짧은건물명");

            migrationBuilder.AlterColumn<string>(
                name: "remark",
                schema: "smfr",
                table: "buildings",
                type: "text",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true,
                oldComment: "비고/설명");

            migrationBuilder.AlterColumn<string>(
                name: "parking_photo_group_id",
                schema: "smfr",
                table: "buildings",
                type: "text",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true,
                oldComment: "주차장 안내 이미지 파일그룹 ID");

            migrationBuilder.AlterColumn<string>(
                name: "name",
                schema: "smfr",
                table: "buildings",
                type: "text",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text",
                oldComment: "건물명");

            migrationBuilder.AlterColumn<string>(
                name: "company_id",
                schema: "smfr",
                table: "buildings",
                type: "text",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text",
                oldComment: "소속 회사 ID");

            migrationBuilder.AlterColumn<string>(
                name: "building_photo_group_id",
                schema: "smfr",
                table: "buildings",
                type: "text",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true,
                oldComment: "건물 전경 사진 파일그룹 ID");

            migrationBuilder.AlterColumn<string>(
                name: "address_detail",
                schema: "smfr",
                table: "buildings",
                type: "text",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true,
                oldComment: "상세주소");

            migrationBuilder.AlterColumn<string>(
                name: "address",
                schema: "smfr",
                table: "buildings",
                type: "text",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true,
                oldComment: "주소");

            migrationBuilder.AlterColumn<string>(
                name: "abbreviation",
                schema: "smfr",
                table: "buildings",
                type: "character varying(3)",
                maxLength: 3,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(3)",
                oldMaxLength: 3,
                oldNullable: true,
                oldComment: "건물 약어 (3자리 영문 대문자)");
        }
    }
}
