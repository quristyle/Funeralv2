using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace funeralv2Api.Migrations
{
    /// <inheritdoc />
    public partial class UpdateAllComments : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterTable(
                name: "rooms",
                schema: "smfr",
                comment: "호실(빈소, 안치실, 참관실 등) 정보 엔티티 클래스",
                oldComment: "호실 엔티티");

            migrationBuilder.AlterTable(
                name: "floors",
                schema: "smfr",
                comment: "건물 내 층(Floor) 정보 엔티티 클래스",
                oldComment: "층 엔티티");

            migrationBuilder.AlterTable(
                name: "devices",
                schema: "smfr",
                comment: "장비(디바이스) 정보 엔티티 클래스",
                oldComment: "장비 정보");

            migrationBuilder.AlterTable(
                name: "device_text_overlays",
                schema: "smfr",
                comment: "장비 텍스트 오버레이 설정 엔티티 클래스\n            장비(모니터) 화면의 특정 위치에 텍스트를 배치하는 설정을 관리합니다.\n            위치 및 크기 값은 % 단위 (소수점 3자리)로 저장되어 다양한 해상도에서도 정확한 위치를 보장합니다.",
                oldComment: "장비 텍스트 오버레이 설정 엔티티\n            장비(모니터) 화면의 특정 위치에 텍스트를 배치하는 설정을 관리합니다.\n            위치 및 크기 값은 % 단위 (소수점 3자리)로 저장되어 다양한 해상도에서도 정확한 위치를 보장합니다.");

            migrationBuilder.AlterTable(
                name: "device_ribbons",
                schema: "smfr",
                comment: "장비 리본 설정 엔티티 클래스\n            장비(모니터) 화면의 특정 위치에 장식(리본) 이미지를 배치하는 설정을 관리합니다.\n            위치 및 크기 값은 % 단위 (소수점 3자리)로 저장되어 다양한 해상도에서도 정확한 위치를 보장합니다.",
                oldComment: "장비 리본 설정 엔티티\n            장비(모니터) 화면의 특정 위치에 장식(리본) 이미지를 배치하는 설정을 관리합니다.\n            위치 및 크기 값은 % 단위 (소수점 3자리)로 저장되어 다양한 해상도에서도 정확한 위치를 보장합니다.");

            migrationBuilder.AlterTable(
                name: "device_configs",
                schema: "smfr",
                comment: "장비 기본 설정 엔티티 클래스 (Device 1:1 관계)\n            음량, 밝기, 자동 전원, 재시작 시각 등을 관리합니다.",
                oldComment: "장비 기본 설정 (Device 1:1 관계)\n            음량, 밝기, 자동 전원, 재시작 시각 등을 관리합니다.");

            migrationBuilder.AlterTable(
                name: "device_attributes",
                schema: "smfr",
                comment: "장비 속성 정보 엔티티 클래스 (Device 1:N 관계)\n            장비 유형별 세부 설정 값을 key-value 형태 또는 구조화된 컬럼으로 관리합니다.",
                oldComment: "장비 속성 정보 (Device 1:N 관계)\n            장비 유형별 세부 설정 값을 key-value 형태 또는 구조화된 컬럼으로 관리합니다.");

            migrationBuilder.AlterTable(
                name: "deceaseds",
                schema: "smfr",
                comment: "고인 정보 관리 엔티티 클래스",
                oldComment: "고인 정보 관리 엔티티");

            migrationBuilder.AlterTable(
                name: "deceased_rooms",
                schema: "smfr",
                comment: "고인별 호실(빈소) 배정 및 사용 기간 이력 관리 엔티티 클래스",
                oldComment: "고인별 호실(빈소) 배정 및 사용 기간 이력 관리 엔티티");

            migrationBuilder.AlterTable(
                name: "deceased_mourners",
                schema: "smfr",
                comment: "고인 상주 정보 관리 엔티티 클래스",
                oldComment: "고인 상주 정보 관리 엔티티");

            migrationBuilder.AlterTable(
                name: "deceased_managers",
                schema: "smfr",
                comment: "고인별 장례 담당 임직원 및 지도사 매핑 엔티티 클래스",
                oldComment: "고인별 장례 담당 임직원 및 지도사 매핑 엔티티");

            migrationBuilder.AlterTable(
                name: "deceased_facilities",
                schema: "smfr",
                comment: "고인별 장례 시설(안치실, 염습실, 영결식장 등) 이용 내역 엔티티 클래스",
                oldComment: "고인별 장례 시설(안치실, 염습실, 영결식장 등) 이용 내역 엔티티");

            migrationBuilder.AlterTable(
                name: "deceased_contractors",
                schema: "smfr",
                comment: "고인 장례 계약자 정보 관리 엔티티 클래스",
                oldComment: "고인 장례 계약자 정보 관리 엔티티");

            migrationBuilder.AlterTable(
                name: "buildings",
                schema: "smfr",
                comment: "건물(시설물) 정보 엔티티 클래스",
                oldComment: "건물 엔티티");

            migrationBuilder.AlterColumn<string>(
                name: "status",
                schema: "smfr",
                table: "rooms",
                type: "text",
                nullable: false,
                comment: "사용 상태 (예: ACTIVE, INACTIVE 등, 기본값: ACTIVE)",
                oldClrType: typeof(string),
                oldType: "text",
                oldComment: "상태 (ACTIVE, INACTIVE)");

            migrationBuilder.AlterColumn<string>(
                name: "short_name",
                schema: "smfr",
                table: "rooms",
                type: "text",
                nullable: true,
                comment: "호실 약칭",
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
                comment: "호실 유형 (예: 빈소, 안치실, 참관실, 영결식장 등)",
                oldClrType: typeof(string),
                oldType: "text",
                oldComment: "호실 타입 (예: 빈소, 안치실, 참관실 등)");

            migrationBuilder.AlterColumn<string>(
                name: "remark",
                schema: "smfr",
                table: "rooms",
                type: "text",
                nullable: true,
                comment: "비고 및 추가 설명",
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true,
                oldComment: "비고/설명");

            migrationBuilder.AlterColumn<string>(
                name: "floor_id",
                schema: "smfr",
                table: "rooms",
                type: "text",
                nullable: false,
                comment: "소속 층 식별자 (ID)",
                oldClrType: typeof(string),
                oldType: "text",
                oldComment: "소속 층 ID");

            migrationBuilder.AlterColumn<string>(
                name: "building_id",
                schema: "smfr",
                table: "rooms",
                type: "text",
                nullable: false,
                comment: "소속 건물 식별자 (ID)",
                oldClrType: typeof(string),
                oldType: "text",
                oldComment: "소속 건물 ID");

            migrationBuilder.AlterColumn<string>(
                name: "remark",
                schema: "smfr",
                table: "floors",
                type: "text",
                nullable: true,
                comment: "비고 및 추가 설명",
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
                comment: "층 명칭 (예: 1F, B1 등)",
                oldClrType: typeof(string),
                oldType: "text",
                oldComment: "층 명칭");

            migrationBuilder.AlterColumn<string>(
                name: "building_id",
                schema: "smfr",
                table: "floors",
                type: "text",
                nullable: false,
                comment: "소속 건물 식별자 (ID)",
                oldClrType: typeof(string),
                oldType: "text",
                oldComment: "소속 건물 ID");

            migrationBuilder.AlterColumn<string>(
                name: "status",
                schema: "smfr",
                table: "devices",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                comment: "장비 상태 (예: ONLINE, OFFLINE, UNKNOWN 등, 기본값: UNKNOWN)",
                oldClrType: typeof(string),
                oldType: "character varying(20)",
                oldMaxLength: 20);

            migrationBuilder.AlterColumn<string>(
                name: "short_name",
                schema: "smfr",
                table: "devices",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true,
                comment: "장비 약칭",
                oldClrType: typeof(string),
                oldType: "character varying(100)",
                oldMaxLength: 100,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "room_id",
                schema: "smfr",
                table: "devices",
                type: "text",
                nullable: true,
                comment: "배정된 호실(빈소) 식별자 (ID)",
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "name",
                schema: "smfr",
                table: "devices",
                type: "character varying(100)",
                maxLength: 100,
                nullable: false,
                comment: "장비명",
                oldClrType: typeof(string),
                oldType: "character varying(100)",
                oldMaxLength: 100);

            migrationBuilder.AlterColumn<string>(
                name: "mac_address",
                schema: "smfr",
                table: "devices",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true,
                comment: "MAC 주소",
                oldClrType: typeof(string),
                oldType: "character varying(50)",
                oldMaxLength: 50,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "ip_address",
                schema: "smfr",
                table: "devices",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true,
                comment: "IP 주소",
                oldClrType: typeof(string),
                oldType: "character varying(50)",
                oldMaxLength: 50,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "floor_id",
                schema: "smfr",
                table: "devices",
                type: "text",
                nullable: true,
                comment: "배정된 층 식별자 (ID)",
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "device_type",
                schema: "smfr",
                table: "devices",
                type: "character varying(50)",
                maxLength: 50,
                nullable: false,
                comment: "장비 유형 (예: DID, KIOSK, SIGNBOARD 등, 기본값: DID)",
                oldClrType: typeof(string),
                oldType: "character varying(50)",
                oldMaxLength: 50);

            migrationBuilder.AlterColumn<string>(
                name: "company_id",
                schema: "smfr",
                table: "devices",
                type: "text",
                nullable: true,
                comment: "소속 회사 식별자 (ID)",
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "code",
                schema: "smfr",
                table: "devices",
                type: "character varying(50)",
                maxLength: 50,
                nullable: false,
                comment: "장비 코드 (고유 식별 코드)",
                oldClrType: typeof(string),
                oldType: "character varying(50)",
                oldMaxLength: 50);

            migrationBuilder.AlterColumn<string>(
                name: "building_id",
                schema: "smfr",
                table: "devices",
                type: "text",
                nullable: true,
                comment: "배정된 건물 식별자 (ID)",
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true);

            migrationBuilder.AlterColumn<decimal>(
                name: "width",
                schema: "smfr",
                table: "device_text_overlays",
                type: "numeric(6,3)",
                nullable: false,
                comment: "화면 내 표시 너비 (%, 소수점 3자리)",
                oldClrType: typeof(decimal),
                oldType: "numeric(6,3)",
                oldComment: "너비 (%, 소수점 3자리)");

            migrationBuilder.AlterColumn<string>(
                name: "text_align",
                schema: "smfr",
                table: "device_text_overlays",
                type: "character varying(10)",
                maxLength: 10,
                nullable: false,
                comment: "텍스트 정렬 방식 (left | center | right)",
                oldClrType: typeof(string),
                oldType: "character varying(10)",
                oldMaxLength: 10,
                oldComment: "텍스트 정렬 (left | center | right)");

            migrationBuilder.AlterColumn<string>(
                name: "remark",
                schema: "smfr",
                table: "device_text_overlays",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true,
                comment: "비고 및 추가 설명",
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
                comment: "화면 내 상단 위치 (%, 소수점 3자리)",
                oldClrType: typeof(decimal),
                oldType: "numeric(6,3)",
                oldComment: "상단 위치 (%, 소수점 3자리)");

            migrationBuilder.AlterColumn<decimal>(
                name: "position_left",
                schema: "smfr",
                table: "device_text_overlays",
                type: "numeric(6,3)",
                nullable: false,
                comment: "화면 내 좌측 위치 (%, 소수점 3자리)",
                oldClrType: typeof(decimal),
                oldType: "numeric(6,3)",
                oldComment: "좌측 위치 (%, 소수점 3자리)");

            migrationBuilder.AlterColumn<decimal>(
                name: "height",
                schema: "smfr",
                table: "device_text_overlays",
                type: "numeric(6,3)",
                nullable: false,
                comment: "화면 내 표시 높이 (%, 소수점 3자리)",
                oldClrType: typeof(decimal),
                oldType: "numeric(6,3)",
                oldComment: "높이 (%, 소수점 3자리)");

            migrationBuilder.AlterColumn<decimal>(
                name: "font_size",
                schema: "smfr",
                table: "device_text_overlays",
                type: "numeric(6,3)",
                nullable: false,
                comment: "폰트 크기 (화면 높이 대비 %, 소수점 3자리)",
                oldClrType: typeof(decimal),
                oldType: "numeric(6,3)",
                oldComment: "폰트 크기 (px 단위 기준, 화면 높이 대비 %로 저장)");

            migrationBuilder.AlterColumn<string>(
                name: "device_id",
                schema: "smfr",
                table: "device_text_overlays",
                type: "character varying(50)",
                maxLength: 50,
                nullable: false,
                comment: "장비 식별자 (ID)",
                oldClrType: typeof(string),
                oldType: "character varying(50)",
                oldMaxLength: 50,
                oldComment: "장비 FK");

            migrationBuilder.AlterColumn<decimal>(
                name: "width",
                schema: "smfr",
                table: "device_ribbons",
                type: "numeric(6,3)",
                nullable: false,
                comment: "화면 내 표시 너비 (%, 소수점 3자리)",
                oldClrType: typeof(decimal),
                oldType: "numeric(6,3)",
                oldComment: "너비 (%, 소수점 3자리)");

            migrationBuilder.AlterColumn<string>(
                name: "remark",
                schema: "smfr",
                table: "device_ribbons",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true,
                comment: "비고 및 추가 설명",
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
                comment: "화면 내 상단 위치 (%, 소수점 3자리)",
                oldClrType: typeof(decimal),
                oldType: "numeric(6,3)",
                oldComment: "상단 위치 (%, 소수점 3자리)");

            migrationBuilder.AlterColumn<decimal>(
                name: "position_left",
                schema: "smfr",
                table: "device_ribbons",
                type: "numeric(6,3)",
                nullable: false,
                comment: "화면 내 좌측 위치 (%, 소수점 3자리)",
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
                comment: "장식(미디어소스) 식별자 (ID, MediaSource.SourceType = IMAGE)",
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
                comment: "화면 내 표시 높이 (%, 소수점 3자리)",
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
                comment: "장비 식별자 (ID)",
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
                comment: "기기 음량 (0-100, 기본값: 50)",
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
                comment: "일일 자동 재시작 시각 (예: HH:mm 형식)",
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
                comment: "자동 켜짐 시각 (예: HH:mm 형식)",
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
                comment: "자동 꺼짐 시각 (예: HH:mm 형식)",
                oldClrType: typeof(string),
                oldType: "character varying(5)",
                oldMaxLength: 5,
                oldNullable: true,
                oldComment: "자동 꺼짐 시각 (HH:mm)");

            migrationBuilder.AlterColumn<string>(
                name: "device_id",
                schema: "smfr",
                table: "device_configs",
                type: "text",
                nullable: false,
                comment: "장비 식별자 (ID)",
                oldClrType: typeof(string),
                oldType: "text",
                oldComment: "장비 FK");

            migrationBuilder.AlterColumn<int>(
                name: "brightness",
                schema: "smfr",
                table: "device_configs",
                type: "integer",
                nullable: false,
                comment: "화면 밝기 (0-100, 기본값: 80)",
                oldClrType: typeof(int),
                oldType: "integer",
                oldComment: "화면 밝기 (0-100)");

            migrationBuilder.AlterColumn<string>(
                name: "video_id",
                schema: "smfr",
                table: "device_attributes",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true,
                comment: "재생 동영상 식별자 (ID)",
                oldClrType: typeof(string),
                oldType: "character varying(50)",
                oldMaxLength: 50,
                oldNullable: true,
                oldComment: "재생 동영상 ID");

            migrationBuilder.AlterColumn<string>(
                name: "remark",
                schema: "smfr",
                table: "device_attributes",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true,
                comment: "비고 및 추가 설명",
                oldClrType: typeof(string),
                oldType: "character varying(500)",
                oldMaxLength: 500,
                oldNullable: true,
                oldComment: "비고");

            migrationBuilder.AlterColumn<string>(
                name: "music_id",
                schema: "smfr",
                table: "device_attributes",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true,
                comment: "재생 음악 식별자 (ID)",
                oldClrType: typeof(string),
                oldType: "character varying(50)",
                oldMaxLength: 50,
                oldNullable: true,
                oldComment: "재생 음악 ID");

            migrationBuilder.AlterColumn<string>(
                name: "device_id",
                schema: "smfr",
                table: "device_attributes",
                type: "text",
                nullable: false,
                comment: "장비 식별자 (ID)",
                oldClrType: typeof(string),
                oldType: "text",
                oldComment: "장비 FK");

            migrationBuilder.AlterColumn<string>(
                name: "updated_by",
                schema: "smfr",
                table: "deceaseds",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true,
                comment: "최종 수정자 식별자 (ID)",
                oldClrType: typeof(string),
                oldType: "character varying(50)",
                oldMaxLength: 50,
                oldNullable: true);

            migrationBuilder.AlterColumn<DateTime>(
                name: "updated_at",
                schema: "smfr",
                table: "deceaseds",
                type: "timestamp with time zone",
                nullable: true,
                comment: "최종 수정 일시",
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "status",
                schema: "smfr",
                table: "deceaseds",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                comment: "장례 진행 상태 (예: IN_HOSPITAL, DISCHARGED, COMPLETED 등, 기본값: IN_HOSPITAL)",
                oldClrType: typeof(string),
                oldType: "character varying(20)",
                oldMaxLength: 20);

            migrationBuilder.AlterColumn<string>(
                name: "ssn",
                schema: "smfr",
                table: "deceaseds",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true,
                comment: "주민등록번호",
                oldClrType: typeof(string),
                oldType: "character varying(50)",
                oldMaxLength: 50,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "remark",
                schema: "smfr",
                table: "deceaseds",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true,
                comment: "비고 및 추가 설명",
                oldClrType: typeof(string),
                oldType: "character varying(500)",
                oldMaxLength: 500,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "religion",
                schema: "smfr",
                table: "deceaseds",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true,
                comment: "종교",
                oldClrType: typeof(string),
                oldType: "character varying(50)",
                oldMaxLength: 50,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "name",
                schema: "smfr",
                table: "deceaseds",
                type: "character varying(100)",
                maxLength: 100,
                nullable: false,
                comment: "고인 성명",
                oldClrType: typeof(string),
                oldType: "character varying(100)",
                oldMaxLength: 100);

            migrationBuilder.AlterColumn<string>(
                name: "memorial_photo_url",
                schema: "smfr",
                table: "deceaseds",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true,
                comment: "영정사진 웹 URL 주소",
                oldClrType: typeof(string),
                oldType: "character varying(500)",
                oldMaxLength: 500,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "memorial_photo_file_id",
                schema: "smfr",
                table: "deceaseds",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true,
                comment: "영정사진 원본 파일 식별자 (ID)",
                oldClrType: typeof(string),
                oldType: "character varying(50)",
                oldMaxLength: 50,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "memorial_edited_photo_url",
                schema: "smfr",
                table: "deceaseds",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true,
                comment: "편집된(보정/배경합성 등) 영정사진 웹 URL 주소",
                oldClrType: typeof(string),
                oldType: "character varying(500)",
                oldMaxLength: 500,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "memorial_edited_photo_file_id",
                schema: "smfr",
                table: "deceaseds",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true,
                comment: "편집된 영정사진 파일 식별자 (ID)",
                oldClrType: typeof(string),
                oldType: "character varying(50)",
                oldMaxLength: 50,
                oldNullable: true);

            migrationBuilder.AlterColumn<bool>(
                name: "is_deleted",
                schema: "smfr",
                table: "deceaseds",
                type: "boolean",
                nullable: false,
                comment: "삭제 여부 플래그",
                oldClrType: typeof(bool),
                oldType: "boolean");

            migrationBuilder.AlterColumn<string>(
                name: "gender",
                schema: "smfr",
                table: "deceaseds",
                type: "character varying(10)",
                maxLength: 10,
                nullable: false,
                comment: "고인 성별 (MALE, FEMALE 등)",
                oldClrType: typeof(string),
                oldType: "character varying(10)",
                oldMaxLength: 10);

            migrationBuilder.AlterColumn<DateTime>(
                name: "funeral_date",
                schema: "smfr",
                table: "deceaseds",
                type: "timestamp with time zone",
                nullable: true,
                comment: "입관/장례 일시",
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "family_photo_group_id",
                schema: "smfr",
                table: "deceaseds",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true,
                comment: "유족 추모용 사진 파일 그룹 식별자 (ID)",
                oldClrType: typeof(string),
                oldType: "character varying(50)",
                oldMaxLength: 50,
                oldNullable: true);

            migrationBuilder.AlterColumn<DateTime>(
                name: "death_date",
                schema: "smfr",
                table: "deceaseds",
                type: "timestamp with time zone",
                nullable: false,
                comment: "사망 일시",
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone");

            migrationBuilder.AlterColumn<string>(
                name: "created_by",
                schema: "smfr",
                table: "deceaseds",
                type: "character varying(50)",
                maxLength: 50,
                nullable: false,
                comment: "최초 등록자 식별자 (ID)",
                oldClrType: typeof(string),
                oldType: "character varying(50)",
                oldMaxLength: 50);

            migrationBuilder.AlterColumn<DateTime>(
                name: "created_at",
                schema: "smfr",
                table: "deceaseds",
                type: "timestamp with time zone",
                nullable: false,
                comment: "최초 등록 일시",
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone");

            migrationBuilder.AlterColumn<string>(
                name: "cause_of_death",
                schema: "smfr",
                table: "deceaseds",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true,
                comment: "사망 원인",
                oldClrType: typeof(string),
                oldType: "character varying(200)",
                oldMaxLength: 200,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "burial_plot",
                schema: "smfr",
                table: "deceaseds",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true,
                comment: "장지 위치",
                oldClrType: typeof(string),
                oldType: "character varying(200)",
                oldMaxLength: 200,
                oldNullable: true);

            migrationBuilder.AlterColumn<DateTime>(
                name: "burial_date",
                schema: "smfr",
                table: "deceaseds",
                type: "timestamp with time zone",
                nullable: true,
                comment: "발인 일시",
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone",
                oldNullable: true);

            migrationBuilder.AlterColumn<int>(
                name: "age",
                schema: "smfr",
                table: "deceaseds",
                type: "integer",
                nullable: false,
                comment: "고인 연세 (나이)",
                oldClrType: typeof(int),
                oldType: "integer");

            migrationBuilder.AlterColumn<string>(
                name: "id",
                schema: "smfr",
                table: "deceaseds",
                type: "character varying(50)",
                maxLength: 50,
                nullable: false,
                comment: "고인 식별자 (ID, GUID 또는 고유 코드)",
                oldClrType: typeof(string),
                oldType: "character varying(50)",
                oldMaxLength: 50);

            migrationBuilder.AlterColumn<DateTime>(
                name: "start_time",
                schema: "smfr",
                table: "deceased_rooms",
                type: "timestamp with time zone",
                nullable: false,
                comment: "호실 사용 시작 일시",
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone");

            migrationBuilder.AlterColumn<string>(
                name: "room_id",
                schema: "smfr",
                table: "deceased_rooms",
                type: "character varying(50)",
                maxLength: 50,
                nullable: false,
                comment: "배정된 호실(빈소) 식별자 (ID)",
                oldClrType: typeof(string),
                oldType: "character varying(50)",
                oldMaxLength: 50);

            migrationBuilder.AlterColumn<bool>(
                name: "is_deleted",
                schema: "smfr",
                table: "deceased_rooms",
                type: "boolean",
                nullable: false,
                comment: "삭제 여부 플래그",
                oldClrType: typeof(bool),
                oldType: "boolean");

            migrationBuilder.AlterColumn<DateTime>(
                name: "end_time",
                schema: "smfr",
                table: "deceased_rooms",
                type: "timestamp with time zone",
                nullable: true,
                comment: "호실 사용 종료 일시",
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "deceased_id",
                schema: "smfr",
                table: "deceased_rooms",
                type: "character varying(50)",
                maxLength: 50,
                nullable: false,
                comment: "연관된 고인 식별자 (ID)",
                oldClrType: typeof(string),
                oldType: "character varying(50)",
                oldMaxLength: 50);

            migrationBuilder.AlterColumn<string>(
                name: "id",
                schema: "smfr",
                table: "deceased_rooms",
                type: "character varying(50)",
                maxLength: 50,
                nullable: false,
                comment: "배정 내역 식별자 (ID)",
                oldClrType: typeof(string),
                oldType: "character varying(50)",
                oldMaxLength: 50);

            migrationBuilder.AlterColumn<int>(
                name: "sort_order",
                schema: "smfr",
                table: "deceased_mourners",
                type: "integer",
                nullable: false,
                comment: "정렬 순서",
                oldClrType: typeof(int),
                oldType: "integer");

            migrationBuilder.AlterColumn<string>(
                name: "relation",
                schema: "smfr",
                table: "deceased_mourners",
                type: "character varying(50)",
                maxLength: 50,
                nullable: false,
                comment: "고인과의 관계 (예: 배우자, 자녀, 자부, 사위, 손자 등)",
                oldClrType: typeof(string),
                oldType: "character varying(50)",
                oldMaxLength: 50);

            migrationBuilder.AlterColumn<string>(
                name: "name",
                schema: "smfr",
                table: "deceased_mourners",
                type: "character varying(100)",
                maxLength: 100,
                nullable: false,
                comment: "상주 성명",
                oldClrType: typeof(string),
                oldType: "character varying(100)",
                oldMaxLength: 100);

            migrationBuilder.AlterColumn<bool>(
                name: "is_deleted",
                schema: "smfr",
                table: "deceased_mourners",
                type: "boolean",
                nullable: false,
                comment: "삭제 여부 플래그",
                oldClrType: typeof(bool),
                oldType: "boolean");

            migrationBuilder.AlterColumn<bool>(
                name: "is_chief",
                schema: "smfr",
                table: "deceased_mourners",
                type: "boolean",
                nullable: false,
                comment: "대표 상주 여부",
                oldClrType: typeof(bool),
                oldType: "boolean");

            migrationBuilder.AlterColumn<string>(
                name: "email",
                schema: "smfr",
                table: "deceased_mourners",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true,
                comment: "이메일 주소",
                oldClrType: typeof(string),
                oldType: "character varying(100)",
                oldMaxLength: 100,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "deceased_id",
                schema: "smfr",
                table: "deceased_mourners",
                type: "character varying(50)",
                maxLength: 50,
                nullable: false,
                comment: "연관된 고인 식별자 (ID)",
                oldClrType: typeof(string),
                oldType: "character varying(50)",
                oldMaxLength: 50);

            migrationBuilder.AlterColumn<string>(
                name: "contact",
                schema: "smfr",
                table: "deceased_mourners",
                type: "character varying(50)",
                maxLength: 50,
                nullable: false,
                comment: "상주 연락처 (전화번호)",
                oldClrType: typeof(string),
                oldType: "character varying(50)",
                oldMaxLength: 50);

            migrationBuilder.AlterColumn<string>(
                name: "address",
                schema: "smfr",
                table: "deceased_mourners",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true,
                comment: "주소",
                oldClrType: typeof(string),
                oldType: "character varying(200)",
                oldMaxLength: 200,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "id",
                schema: "smfr",
                table: "deceased_mourners",
                type: "character varying(50)",
                maxLength: 50,
                nullable: false,
                comment: "상주 식별자 (ID)",
                oldClrType: typeof(string),
                oldType: "character varying(50)",
                oldMaxLength: 50);

            migrationBuilder.AlterColumn<string>(
                name: "staff_name",
                schema: "smfr",
                table: "deceased_managers",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true,
                comment: "당사 담당 직원 성명",
                oldClrType: typeof(string),
                oldType: "character varying(100)",
                oldMaxLength: 100,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "staff_contact",
                schema: "smfr",
                table: "deceased_managers",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true,
                comment: "당사 담당 직원 연락처 (전화번호)",
                oldClrType: typeof(string),
                oldType: "character varying(50)",
                oldMaxLength: 50,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "mutual_aid_company",
                schema: "smfr",
                table: "deceased_managers",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true,
                comment: "상조회사 명칭",
                oldClrType: typeof(string),
                oldType: "character varying(100)",
                oldMaxLength: 100,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "director_name",
                schema: "smfr",
                table: "deceased_managers",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true,
                comment: "장례지도사 성명",
                oldClrType: typeof(string),
                oldType: "character varying(100)",
                oldMaxLength: 100,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "director_contact",
                schema: "smfr",
                table: "deceased_managers",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true,
                comment: "장례지도사 연락처 (전화번호)",
                oldClrType: typeof(string),
                oldType: "character varying(50)",
                oldMaxLength: 50,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "deceased_id",
                schema: "smfr",
                table: "deceased_managers",
                type: "character varying(50)",
                maxLength: 50,
                nullable: false,
                comment: "연관된 고인 식별자 (ID)",
                oldClrType: typeof(string),
                oldType: "character varying(50)",
                oldMaxLength: 50);

            migrationBuilder.AlterColumn<string>(
                name: "id",
                schema: "smfr",
                table: "deceased_managers",
                type: "character varying(50)",
                maxLength: 50,
                nullable: false,
                comment: "매핑 정보 식별자 (ID)",
                oldClrType: typeof(string),
                oldType: "character varying(50)",
                oldMaxLength: 50);

            migrationBuilder.AlterColumn<double>(
                name: "use_hours",
                schema: "smfr",
                table: "deceased_facilities",
                type: "double precision",
                nullable: false,
                comment: "시설 총 이용 시간",
                oldClrType: typeof(double),
                oldType: "double precision");

            migrationBuilder.AlterColumn<decimal>(
                name: "unit_price",
                schema: "smfr",
                table: "deceased_facilities",
                type: "numeric",
                nullable: false,
                comment: "시설 시간당 이용 단가",
                oldClrType: typeof(decimal),
                oldType: "numeric");

            migrationBuilder.AlterColumn<decimal>(
                name: "total_price",
                schema: "smfr",
                table: "deceased_facilities",
                type: "numeric",
                nullable: false,
                comment: "시설 이용 총 금액",
                oldClrType: typeof(decimal),
                oldType: "numeric");

            migrationBuilder.AlterColumn<DateTime>(
                name: "start_time",
                schema: "smfr",
                table: "deceased_facilities",
                type: "timestamp with time zone",
                nullable: true,
                comment: "시설 이용 시작 일시",
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "remark",
                schema: "smfr",
                table: "deceased_facilities",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true,
                comment: "비고 및 추가 설명",
                oldClrType: typeof(string),
                oldType: "character varying(500)",
                oldMaxLength: 500,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "facility_type",
                schema: "smfr",
                table: "deceased_facilities",
                type: "character varying(50)",
                maxLength: 50,
                nullable: false,
                comment: "시설 유형 (예: MORGUE, WASH_ROOM, HALL, ETC 등)",
                oldClrType: typeof(string),
                oldType: "character varying(50)",
                oldMaxLength: 50);

            migrationBuilder.AlterColumn<DateTime>(
                name: "end_time",
                schema: "smfr",
                table: "deceased_facilities",
                type: "timestamp with time zone",
                nullable: true,
                comment: "시설 이용 종료 일시",
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "deceased_id",
                schema: "smfr",
                table: "deceased_facilities",
                type: "character varying(50)",
                maxLength: 50,
                nullable: false,
                comment: "연관된 고인 식별자 (ID)",
                oldClrType: typeof(string),
                oldType: "character varying(50)",
                oldMaxLength: 50);

            migrationBuilder.AlterColumn<string>(
                name: "id",
                schema: "smfr",
                table: "deceased_facilities",
                type: "character varying(50)",
                maxLength: 50,
                nullable: false,
                comment: "이용 내역 식별자 (ID)",
                oldClrType: typeof(string),
                oldType: "character varying(50)",
                oldMaxLength: 50);

            migrationBuilder.AlterColumn<string>(
                name: "signature_file_id",
                schema: "smfr",
                table: "deceased_contractors",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true,
                comment: "계약 서명 이미지 파일 식별자 (ID)",
                oldClrType: typeof(string),
                oldType: "character varying(50)",
                oldMaxLength: 50,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "remark",
                schema: "smfr",
                table: "deceased_contractors",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true,
                comment: "비고 및 추가 설명",
                oldClrType: typeof(string),
                oldType: "character varying(500)",
                oldMaxLength: 500,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "relation",
                schema: "smfr",
                table: "deceased_contractors",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true,
                comment: "고인과의 관계",
                oldClrType: typeof(string),
                oldType: "character varying(50)",
                oldMaxLength: 50,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "name",
                schema: "smfr",
                table: "deceased_contractors",
                type: "character varying(100)",
                maxLength: 100,
                nullable: false,
                comment: "계약자 성명",
                oldClrType: typeof(string),
                oldType: "character varying(100)",
                oldMaxLength: 100);

            migrationBuilder.AlterColumn<string>(
                name: "deceased_id",
                schema: "smfr",
                table: "deceased_contractors",
                type: "character varying(50)",
                maxLength: 50,
                nullable: false,
                comment: "연관된 고인 식별자 (ID)",
                oldClrType: typeof(string),
                oldType: "character varying(50)",
                oldMaxLength: 50);

            migrationBuilder.AlterColumn<string>(
                name: "contact",
                schema: "smfr",
                table: "deceased_contractors",
                type: "character varying(50)",
                maxLength: 50,
                nullable: false,
                comment: "계약자 연락처 (전화번호)",
                oldClrType: typeof(string),
                oldType: "character varying(50)",
                oldMaxLength: 50);

            migrationBuilder.AlterColumn<string>(
                name: "address",
                schema: "smfr",
                table: "deceased_contractors",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true,
                comment: "계약자 주소",
                oldClrType: typeof(string),
                oldType: "character varying(200)",
                oldMaxLength: 200,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "id",
                schema: "smfr",
                table: "deceased_contractors",
                type: "character varying(50)",
                maxLength: 50,
                nullable: false,
                comment: "계약자 식별자 (ID)",
                oldClrType: typeof(string),
                oldType: "character varying(50)",
                oldMaxLength: 50);

            migrationBuilder.AlterColumn<string>(
                name: "short_name",
                schema: "smfr",
                table: "buildings",
                type: "text",
                nullable: true,
                comment: "건물 약칭",
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
                comment: "비고 및 추가 설명",
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
                comment: "주차장 안내 이미지 파일 그룹 식별자 (ID)",
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true,
                oldComment: "주차장 안내 이미지 파일그룹 ID");

            migrationBuilder.AlterColumn<string>(
                name: "company_id",
                schema: "smfr",
                table: "buildings",
                type: "text",
                nullable: false,
                comment: "소속 회사 식별자 (ID)",
                oldClrType: typeof(string),
                oldType: "text",
                oldComment: "소속 회사 ID");

            migrationBuilder.AlterColumn<string>(
                name: "building_photo_group_id",
                schema: "smfr",
                table: "buildings",
                type: "text",
                nullable: true,
                comment: "건물 전경 사진 파일 그룹 식별자 (ID)",
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
                comment: "상세 주소",
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
                comment: "건물 주소",
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true,
                oldComment: "주소");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterTable(
                name: "rooms",
                schema: "smfr",
                comment: "호실 엔티티",
                oldComment: "호실(빈소, 안치실, 참관실 등) 정보 엔티티 클래스");

            migrationBuilder.AlterTable(
                name: "floors",
                schema: "smfr",
                comment: "층 엔티티",
                oldComment: "건물 내 층(Floor) 정보 엔티티 클래스");

            migrationBuilder.AlterTable(
                name: "devices",
                schema: "smfr",
                comment: "장비 정보",
                oldComment: "장비(디바이스) 정보 엔티티 클래스");

            migrationBuilder.AlterTable(
                name: "device_text_overlays",
                schema: "smfr",
                comment: "장비 텍스트 오버레이 설정 엔티티\n            장비(모니터) 화면의 특정 위치에 텍스트를 배치하는 설정을 관리합니다.\n            위치 및 크기 값은 % 단위 (소수점 3자리)로 저장되어 다양한 해상도에서도 정확한 위치를 보장합니다.",
                oldComment: "장비 텍스트 오버레이 설정 엔티티 클래스\n            장비(모니터) 화면의 특정 위치에 텍스트를 배치하는 설정을 관리합니다.\n            위치 및 크기 값은 % 단위 (소수점 3자리)로 저장되어 다양한 해상도에서도 정확한 위치를 보장합니다.");

            migrationBuilder.AlterTable(
                name: "device_ribbons",
                schema: "smfr",
                comment: "장비 리본 설정 엔티티\n            장비(모니터) 화면의 특정 위치에 장식(리본) 이미지를 배치하는 설정을 관리합니다.\n            위치 및 크기 값은 % 단위 (소수점 3자리)로 저장되어 다양한 해상도에서도 정확한 위치를 보장합니다.",
                oldComment: "장비 리본 설정 엔티티 클래스\n            장비(모니터) 화면의 특정 위치에 장식(리본) 이미지를 배치하는 설정을 관리합니다.\n            위치 및 크기 값은 % 단위 (소수점 3자리)로 저장되어 다양한 해상도에서도 정확한 위치를 보장합니다.");

            migrationBuilder.AlterTable(
                name: "device_configs",
                schema: "smfr",
                comment: "장비 기본 설정 (Device 1:1 관계)\n            음량, 밝기, 자동 전원, 재시작 시각 등을 관리합니다.",
                oldComment: "장비 기본 설정 엔티티 클래스 (Device 1:1 관계)\n            음량, 밝기, 자동 전원, 재시작 시각 등을 관리합니다.");

            migrationBuilder.AlterTable(
                name: "device_attributes",
                schema: "smfr",
                comment: "장비 속성 정보 (Device 1:N 관계)\n            장비 유형별 세부 설정 값을 key-value 형태 또는 구조화된 컬럼으로 관리합니다.",
                oldComment: "장비 속성 정보 엔티티 클래스 (Device 1:N 관계)\n            장비 유형별 세부 설정 값을 key-value 형태 또는 구조화된 컬럼으로 관리합니다.");

            migrationBuilder.AlterTable(
                name: "deceaseds",
                schema: "smfr",
                comment: "고인 정보 관리 엔티티",
                oldComment: "고인 정보 관리 엔티티 클래스");

            migrationBuilder.AlterTable(
                name: "deceased_rooms",
                schema: "smfr",
                comment: "고인별 호실(빈소) 배정 및 사용 기간 이력 관리 엔티티",
                oldComment: "고인별 호실(빈소) 배정 및 사용 기간 이력 관리 엔티티 클래스");

            migrationBuilder.AlterTable(
                name: "deceased_mourners",
                schema: "smfr",
                comment: "고인 상주 정보 관리 엔티티",
                oldComment: "고인 상주 정보 관리 엔티티 클래스");

            migrationBuilder.AlterTable(
                name: "deceased_managers",
                schema: "smfr",
                comment: "고인별 장례 담당 임직원 및 지도사 매핑 엔티티",
                oldComment: "고인별 장례 담당 임직원 및 지도사 매핑 엔티티 클래스");

            migrationBuilder.AlterTable(
                name: "deceased_facilities",
                schema: "smfr",
                comment: "고인별 장례 시설(안치실, 염습실, 영결식장 등) 이용 내역 엔티티",
                oldComment: "고인별 장례 시설(안치실, 염습실, 영결식장 등) 이용 내역 엔티티 클래스");

            migrationBuilder.AlterTable(
                name: "deceased_contractors",
                schema: "smfr",
                comment: "고인 장례 계약자 정보 관리 엔티티",
                oldComment: "고인 장례 계약자 정보 관리 엔티티 클래스");

            migrationBuilder.AlterTable(
                name: "buildings",
                schema: "smfr",
                comment: "건물 엔티티",
                oldComment: "건물(시설물) 정보 엔티티 클래스");

            migrationBuilder.AlterColumn<string>(
                name: "status",
                schema: "smfr",
                table: "rooms",
                type: "text",
                nullable: false,
                comment: "상태 (ACTIVE, INACTIVE)",
                oldClrType: typeof(string),
                oldType: "text",
                oldComment: "사용 상태 (예: ACTIVE, INACTIVE 등, 기본값: ACTIVE)");

            migrationBuilder.AlterColumn<string>(
                name: "short_name",
                schema: "smfr",
                table: "rooms",
                type: "text",
                nullable: true,
                comment: "짧은 명칭",
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true,
                oldComment: "호실 약칭");

            migrationBuilder.AlterColumn<string>(
                name: "room_type",
                schema: "smfr",
                table: "rooms",
                type: "text",
                nullable: false,
                comment: "호실 타입 (예: 빈소, 안치실, 참관실 등)",
                oldClrType: typeof(string),
                oldType: "text",
                oldComment: "호실 유형 (예: 빈소, 안치실, 참관실, 영결식장 등)");

            migrationBuilder.AlterColumn<string>(
                name: "remark",
                schema: "smfr",
                table: "rooms",
                type: "text",
                nullable: true,
                comment: "비고/설명",
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true,
                oldComment: "비고 및 추가 설명");

            migrationBuilder.AlterColumn<string>(
                name: "floor_id",
                schema: "smfr",
                table: "rooms",
                type: "text",
                nullable: false,
                comment: "소속 층 ID",
                oldClrType: typeof(string),
                oldType: "text",
                oldComment: "소속 층 식별자 (ID)");

            migrationBuilder.AlterColumn<string>(
                name: "building_id",
                schema: "smfr",
                table: "rooms",
                type: "text",
                nullable: false,
                comment: "소속 건물 ID",
                oldClrType: typeof(string),
                oldType: "text",
                oldComment: "소속 건물 식별자 (ID)");

            migrationBuilder.AlterColumn<string>(
                name: "remark",
                schema: "smfr",
                table: "floors",
                type: "text",
                nullable: true,
                comment: "비고/설명",
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true,
                oldComment: "비고 및 추가 설명");

            migrationBuilder.AlterColumn<string>(
                name: "name",
                schema: "smfr",
                table: "floors",
                type: "text",
                nullable: false,
                comment: "층 명칭",
                oldClrType: typeof(string),
                oldType: "text",
                oldComment: "층 명칭 (예: 1F, B1 등)");

            migrationBuilder.AlterColumn<string>(
                name: "building_id",
                schema: "smfr",
                table: "floors",
                type: "text",
                nullable: false,
                comment: "소속 건물 ID",
                oldClrType: typeof(string),
                oldType: "text",
                oldComment: "소속 건물 식별자 (ID)");

            migrationBuilder.AlterColumn<string>(
                name: "status",
                schema: "smfr",
                table: "devices",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(20)",
                oldMaxLength: 20,
                oldComment: "장비 상태 (예: ONLINE, OFFLINE, UNKNOWN 등, 기본값: UNKNOWN)");

            migrationBuilder.AlterColumn<string>(
                name: "short_name",
                schema: "smfr",
                table: "devices",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(100)",
                oldMaxLength: 100,
                oldNullable: true,
                oldComment: "장비 약칭");

            migrationBuilder.AlterColumn<string>(
                name: "room_id",
                schema: "smfr",
                table: "devices",
                type: "text",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true,
                oldComment: "배정된 호실(빈소) 식별자 (ID)");

            migrationBuilder.AlterColumn<string>(
                name: "name",
                schema: "smfr",
                table: "devices",
                type: "character varying(100)",
                maxLength: 100,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(100)",
                oldMaxLength: 100,
                oldComment: "장비명");

            migrationBuilder.AlterColumn<string>(
                name: "mac_address",
                schema: "smfr",
                table: "devices",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(50)",
                oldMaxLength: 50,
                oldNullable: true,
                oldComment: "MAC 주소");

            migrationBuilder.AlterColumn<string>(
                name: "ip_address",
                schema: "smfr",
                table: "devices",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(50)",
                oldMaxLength: 50,
                oldNullable: true,
                oldComment: "IP 주소");

            migrationBuilder.AlterColumn<string>(
                name: "floor_id",
                schema: "smfr",
                table: "devices",
                type: "text",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true,
                oldComment: "배정된 층 식별자 (ID)");

            migrationBuilder.AlterColumn<string>(
                name: "device_type",
                schema: "smfr",
                table: "devices",
                type: "character varying(50)",
                maxLength: 50,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(50)",
                oldMaxLength: 50,
                oldComment: "장비 유형 (예: DID, KIOSK, SIGNBOARD 등, 기본값: DID)");

            migrationBuilder.AlterColumn<string>(
                name: "company_id",
                schema: "smfr",
                table: "devices",
                type: "text",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true,
                oldComment: "소속 회사 식별자 (ID)");

            migrationBuilder.AlterColumn<string>(
                name: "code",
                schema: "smfr",
                table: "devices",
                type: "character varying(50)",
                maxLength: 50,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(50)",
                oldMaxLength: 50,
                oldComment: "장비 코드 (고유 식별 코드)");

            migrationBuilder.AlterColumn<string>(
                name: "building_id",
                schema: "smfr",
                table: "devices",
                type: "text",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true,
                oldComment: "배정된 건물 식별자 (ID)");

            migrationBuilder.AlterColumn<decimal>(
                name: "width",
                schema: "smfr",
                table: "device_text_overlays",
                type: "numeric(6,3)",
                nullable: false,
                comment: "너비 (%, 소수점 3자리)",
                oldClrType: typeof(decimal),
                oldType: "numeric(6,3)",
                oldComment: "화면 내 표시 너비 (%, 소수점 3자리)");

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
                oldMaxLength: 10,
                oldComment: "텍스트 정렬 방식 (left | center | right)");

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
                oldNullable: true,
                oldComment: "비고 및 추가 설명");

            migrationBuilder.AlterColumn<decimal>(
                name: "position_top",
                schema: "smfr",
                table: "device_text_overlays",
                type: "numeric(6,3)",
                nullable: false,
                comment: "상단 위치 (%, 소수점 3자리)",
                oldClrType: typeof(decimal),
                oldType: "numeric(6,3)",
                oldComment: "화면 내 상단 위치 (%, 소수점 3자리)");

            migrationBuilder.AlterColumn<decimal>(
                name: "position_left",
                schema: "smfr",
                table: "device_text_overlays",
                type: "numeric(6,3)",
                nullable: false,
                comment: "좌측 위치 (%, 소수점 3자리)",
                oldClrType: typeof(decimal),
                oldType: "numeric(6,3)",
                oldComment: "화면 내 좌측 위치 (%, 소수점 3자리)");

            migrationBuilder.AlterColumn<decimal>(
                name: "height",
                schema: "smfr",
                table: "device_text_overlays",
                type: "numeric(6,3)",
                nullable: false,
                comment: "높이 (%, 소수점 3자리)",
                oldClrType: typeof(decimal),
                oldType: "numeric(6,3)",
                oldComment: "화면 내 표시 높이 (%, 소수점 3자리)");

            migrationBuilder.AlterColumn<decimal>(
                name: "font_size",
                schema: "smfr",
                table: "device_text_overlays",
                type: "numeric(6,3)",
                nullable: false,
                comment: "폰트 크기 (px 단위 기준, 화면 높이 대비 %로 저장)",
                oldClrType: typeof(decimal),
                oldType: "numeric(6,3)",
                oldComment: "폰트 크기 (화면 높이 대비 %, 소수점 3자리)");

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
                oldMaxLength: 50,
                oldComment: "장비 식별자 (ID)");

            migrationBuilder.AlterColumn<decimal>(
                name: "width",
                schema: "smfr",
                table: "device_ribbons",
                type: "numeric(6,3)",
                nullable: false,
                comment: "너비 (%, 소수점 3자리)",
                oldClrType: typeof(decimal),
                oldType: "numeric(6,3)",
                oldComment: "화면 내 표시 너비 (%, 소수점 3자리)");

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
                oldNullable: true,
                oldComment: "비고 및 추가 설명");

            migrationBuilder.AlterColumn<decimal>(
                name: "position_top",
                schema: "smfr",
                table: "device_ribbons",
                type: "numeric(6,3)",
                nullable: false,
                comment: "상단 위치 (%, 소수점 3자리)",
                oldClrType: typeof(decimal),
                oldType: "numeric(6,3)",
                oldComment: "화면 내 상단 위치 (%, 소수점 3자리)");

            migrationBuilder.AlterColumn<decimal>(
                name: "position_left",
                schema: "smfr",
                table: "device_ribbons",
                type: "numeric(6,3)",
                nullable: false,
                comment: "좌측 위치 (%, 소수점 3자리)",
                oldClrType: typeof(decimal),
                oldType: "numeric(6,3)",
                oldComment: "화면 내 좌측 위치 (%, 소수점 3자리)");

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
                oldMaxLength: 50,
                oldComment: "장식(미디어소스) 식별자 (ID, MediaSource.SourceType = IMAGE)");

            migrationBuilder.AlterColumn<decimal>(
                name: "height",
                schema: "smfr",
                table: "device_ribbons",
                type: "numeric(6,3)",
                nullable: false,
                comment: "높이 (%, 소수점 3자리)",
                oldClrType: typeof(decimal),
                oldType: "numeric(6,3)",
                oldComment: "화면 내 표시 높이 (%, 소수점 3자리)");

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
                oldMaxLength: 50,
                oldComment: "장비 식별자 (ID)");

            migrationBuilder.AlterColumn<int>(
                name: "volume",
                schema: "smfr",
                table: "device_configs",
                type: "integer",
                nullable: false,
                comment: "기기 음량 (0-100)",
                oldClrType: typeof(int),
                oldType: "integer",
                oldComment: "기기 음량 (0-100, 기본값: 50)");

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
                oldNullable: true,
                oldComment: "일일 자동 재시작 시각 (예: HH:mm 형식)");

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
                oldNullable: true,
                oldComment: "자동 켜짐 시각 (예: HH:mm 형식)");

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
                oldNullable: true,
                oldComment: "자동 꺼짐 시각 (예: HH:mm 형식)");

            migrationBuilder.AlterColumn<string>(
                name: "device_id",
                schema: "smfr",
                table: "device_configs",
                type: "text",
                nullable: false,
                comment: "장비 FK",
                oldClrType: typeof(string),
                oldType: "text",
                oldComment: "장비 식별자 (ID)");

            migrationBuilder.AlterColumn<int>(
                name: "brightness",
                schema: "smfr",
                table: "device_configs",
                type: "integer",
                nullable: false,
                comment: "화면 밝기 (0-100)",
                oldClrType: typeof(int),
                oldType: "integer",
                oldComment: "화면 밝기 (0-100, 기본값: 80)");

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
                oldNullable: true,
                oldComment: "재생 동영상 식별자 (ID)");

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
                oldNullable: true,
                oldComment: "비고 및 추가 설명");

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
                oldNullable: true,
                oldComment: "재생 음악 식별자 (ID)");

            migrationBuilder.AlterColumn<string>(
                name: "device_id",
                schema: "smfr",
                table: "device_attributes",
                type: "text",
                nullable: false,
                comment: "장비 FK",
                oldClrType: typeof(string),
                oldType: "text",
                oldComment: "장비 식별자 (ID)");

            migrationBuilder.AlterColumn<string>(
                name: "updated_by",
                schema: "smfr",
                table: "deceaseds",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(50)",
                oldMaxLength: 50,
                oldNullable: true,
                oldComment: "최종 수정자 식별자 (ID)");

            migrationBuilder.AlterColumn<DateTime>(
                name: "updated_at",
                schema: "smfr",
                table: "deceaseds",
                type: "timestamp with time zone",
                nullable: true,
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone",
                oldNullable: true,
                oldComment: "최종 수정 일시");

            migrationBuilder.AlterColumn<string>(
                name: "status",
                schema: "smfr",
                table: "deceaseds",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(20)",
                oldMaxLength: 20,
                oldComment: "장례 진행 상태 (예: IN_HOSPITAL, DISCHARGED, COMPLETED 등, 기본값: IN_HOSPITAL)");

            migrationBuilder.AlterColumn<string>(
                name: "ssn",
                schema: "smfr",
                table: "deceaseds",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(50)",
                oldMaxLength: 50,
                oldNullable: true,
                oldComment: "주민등록번호");

            migrationBuilder.AlterColumn<string>(
                name: "remark",
                schema: "smfr",
                table: "deceaseds",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(500)",
                oldMaxLength: 500,
                oldNullable: true,
                oldComment: "비고 및 추가 설명");

            migrationBuilder.AlterColumn<string>(
                name: "religion",
                schema: "smfr",
                table: "deceaseds",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(50)",
                oldMaxLength: 50,
                oldNullable: true,
                oldComment: "종교");

            migrationBuilder.AlterColumn<string>(
                name: "name",
                schema: "smfr",
                table: "deceaseds",
                type: "character varying(100)",
                maxLength: 100,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(100)",
                oldMaxLength: 100,
                oldComment: "고인 성명");

            migrationBuilder.AlterColumn<string>(
                name: "memorial_photo_url",
                schema: "smfr",
                table: "deceaseds",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(500)",
                oldMaxLength: 500,
                oldNullable: true,
                oldComment: "영정사진 웹 URL 주소");

            migrationBuilder.AlterColumn<string>(
                name: "memorial_photo_file_id",
                schema: "smfr",
                table: "deceaseds",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(50)",
                oldMaxLength: 50,
                oldNullable: true,
                oldComment: "영정사진 원본 파일 식별자 (ID)");

            migrationBuilder.AlterColumn<string>(
                name: "memorial_edited_photo_url",
                schema: "smfr",
                table: "deceaseds",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(500)",
                oldMaxLength: 500,
                oldNullable: true,
                oldComment: "편집된(보정/배경합성 등) 영정사진 웹 URL 주소");

            migrationBuilder.AlterColumn<string>(
                name: "memorial_edited_photo_file_id",
                schema: "smfr",
                table: "deceaseds",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(50)",
                oldMaxLength: 50,
                oldNullable: true,
                oldComment: "편집된 영정사진 파일 식별자 (ID)");

            migrationBuilder.AlterColumn<bool>(
                name: "is_deleted",
                schema: "smfr",
                table: "deceaseds",
                type: "boolean",
                nullable: false,
                oldClrType: typeof(bool),
                oldType: "boolean",
                oldComment: "삭제 여부 플래그");

            migrationBuilder.AlterColumn<string>(
                name: "gender",
                schema: "smfr",
                table: "deceaseds",
                type: "character varying(10)",
                maxLength: 10,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(10)",
                oldMaxLength: 10,
                oldComment: "고인 성별 (MALE, FEMALE 등)");

            migrationBuilder.AlterColumn<DateTime>(
                name: "funeral_date",
                schema: "smfr",
                table: "deceaseds",
                type: "timestamp with time zone",
                nullable: true,
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone",
                oldNullable: true,
                oldComment: "입관/장례 일시");

            migrationBuilder.AlterColumn<string>(
                name: "family_photo_group_id",
                schema: "smfr",
                table: "deceaseds",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(50)",
                oldMaxLength: 50,
                oldNullable: true,
                oldComment: "유족 추모용 사진 파일 그룹 식별자 (ID)");

            migrationBuilder.AlterColumn<DateTime>(
                name: "death_date",
                schema: "smfr",
                table: "deceaseds",
                type: "timestamp with time zone",
                nullable: false,
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone",
                oldComment: "사망 일시");

            migrationBuilder.AlterColumn<string>(
                name: "created_by",
                schema: "smfr",
                table: "deceaseds",
                type: "character varying(50)",
                maxLength: 50,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(50)",
                oldMaxLength: 50,
                oldComment: "최초 등록자 식별자 (ID)");

            migrationBuilder.AlterColumn<DateTime>(
                name: "created_at",
                schema: "smfr",
                table: "deceaseds",
                type: "timestamp with time zone",
                nullable: false,
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone",
                oldComment: "최초 등록 일시");

            migrationBuilder.AlterColumn<string>(
                name: "cause_of_death",
                schema: "smfr",
                table: "deceaseds",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(200)",
                oldMaxLength: 200,
                oldNullable: true,
                oldComment: "사망 원인");

            migrationBuilder.AlterColumn<string>(
                name: "burial_plot",
                schema: "smfr",
                table: "deceaseds",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(200)",
                oldMaxLength: 200,
                oldNullable: true,
                oldComment: "장지 위치");

            migrationBuilder.AlterColumn<DateTime>(
                name: "burial_date",
                schema: "smfr",
                table: "deceaseds",
                type: "timestamp with time zone",
                nullable: true,
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone",
                oldNullable: true,
                oldComment: "발인 일시");

            migrationBuilder.AlterColumn<int>(
                name: "age",
                schema: "smfr",
                table: "deceaseds",
                type: "integer",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "integer",
                oldComment: "고인 연세 (나이)");

            migrationBuilder.AlterColumn<string>(
                name: "id",
                schema: "smfr",
                table: "deceaseds",
                type: "character varying(50)",
                maxLength: 50,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(50)",
                oldMaxLength: 50,
                oldComment: "고인 식별자 (ID, GUID 또는 고유 코드)");

            migrationBuilder.AlterColumn<DateTime>(
                name: "start_time",
                schema: "smfr",
                table: "deceased_rooms",
                type: "timestamp with time zone",
                nullable: false,
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone",
                oldComment: "호실 사용 시작 일시");

            migrationBuilder.AlterColumn<string>(
                name: "room_id",
                schema: "smfr",
                table: "deceased_rooms",
                type: "character varying(50)",
                maxLength: 50,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(50)",
                oldMaxLength: 50,
                oldComment: "배정된 호실(빈소) 식별자 (ID)");

            migrationBuilder.AlterColumn<bool>(
                name: "is_deleted",
                schema: "smfr",
                table: "deceased_rooms",
                type: "boolean",
                nullable: false,
                oldClrType: typeof(bool),
                oldType: "boolean",
                oldComment: "삭제 여부 플래그");

            migrationBuilder.AlterColumn<DateTime>(
                name: "end_time",
                schema: "smfr",
                table: "deceased_rooms",
                type: "timestamp with time zone",
                nullable: true,
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone",
                oldNullable: true,
                oldComment: "호실 사용 종료 일시");

            migrationBuilder.AlterColumn<string>(
                name: "deceased_id",
                schema: "smfr",
                table: "deceased_rooms",
                type: "character varying(50)",
                maxLength: 50,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(50)",
                oldMaxLength: 50,
                oldComment: "연관된 고인 식별자 (ID)");

            migrationBuilder.AlterColumn<string>(
                name: "id",
                schema: "smfr",
                table: "deceased_rooms",
                type: "character varying(50)",
                maxLength: 50,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(50)",
                oldMaxLength: 50,
                oldComment: "배정 내역 식별자 (ID)");

            migrationBuilder.AlterColumn<int>(
                name: "sort_order",
                schema: "smfr",
                table: "deceased_mourners",
                type: "integer",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "integer",
                oldComment: "정렬 순서");

            migrationBuilder.AlterColumn<string>(
                name: "relation",
                schema: "smfr",
                table: "deceased_mourners",
                type: "character varying(50)",
                maxLength: 50,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(50)",
                oldMaxLength: 50,
                oldComment: "고인과의 관계 (예: 배우자, 자녀, 자부, 사위, 손자 등)");

            migrationBuilder.AlterColumn<string>(
                name: "name",
                schema: "smfr",
                table: "deceased_mourners",
                type: "character varying(100)",
                maxLength: 100,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(100)",
                oldMaxLength: 100,
                oldComment: "상주 성명");

            migrationBuilder.AlterColumn<bool>(
                name: "is_deleted",
                schema: "smfr",
                table: "deceased_mourners",
                type: "boolean",
                nullable: false,
                oldClrType: typeof(bool),
                oldType: "boolean",
                oldComment: "삭제 여부 플래그");

            migrationBuilder.AlterColumn<bool>(
                name: "is_chief",
                schema: "smfr",
                table: "deceased_mourners",
                type: "boolean",
                nullable: false,
                oldClrType: typeof(bool),
                oldType: "boolean",
                oldComment: "대표 상주 여부");

            migrationBuilder.AlterColumn<string>(
                name: "email",
                schema: "smfr",
                table: "deceased_mourners",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(100)",
                oldMaxLength: 100,
                oldNullable: true,
                oldComment: "이메일 주소");

            migrationBuilder.AlterColumn<string>(
                name: "deceased_id",
                schema: "smfr",
                table: "deceased_mourners",
                type: "character varying(50)",
                maxLength: 50,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(50)",
                oldMaxLength: 50,
                oldComment: "연관된 고인 식별자 (ID)");

            migrationBuilder.AlterColumn<string>(
                name: "contact",
                schema: "smfr",
                table: "deceased_mourners",
                type: "character varying(50)",
                maxLength: 50,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(50)",
                oldMaxLength: 50,
                oldComment: "상주 연락처 (전화번호)");

            migrationBuilder.AlterColumn<string>(
                name: "address",
                schema: "smfr",
                table: "deceased_mourners",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(200)",
                oldMaxLength: 200,
                oldNullable: true,
                oldComment: "주소");

            migrationBuilder.AlterColumn<string>(
                name: "id",
                schema: "smfr",
                table: "deceased_mourners",
                type: "character varying(50)",
                maxLength: 50,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(50)",
                oldMaxLength: 50,
                oldComment: "상주 식별자 (ID)");

            migrationBuilder.AlterColumn<string>(
                name: "staff_name",
                schema: "smfr",
                table: "deceased_managers",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(100)",
                oldMaxLength: 100,
                oldNullable: true,
                oldComment: "당사 담당 직원 성명");

            migrationBuilder.AlterColumn<string>(
                name: "staff_contact",
                schema: "smfr",
                table: "deceased_managers",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(50)",
                oldMaxLength: 50,
                oldNullable: true,
                oldComment: "당사 담당 직원 연락처 (전화번호)");

            migrationBuilder.AlterColumn<string>(
                name: "mutual_aid_company",
                schema: "smfr",
                table: "deceased_managers",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(100)",
                oldMaxLength: 100,
                oldNullable: true,
                oldComment: "상조회사 명칭");

            migrationBuilder.AlterColumn<string>(
                name: "director_name",
                schema: "smfr",
                table: "deceased_managers",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(100)",
                oldMaxLength: 100,
                oldNullable: true,
                oldComment: "장례지도사 성명");

            migrationBuilder.AlterColumn<string>(
                name: "director_contact",
                schema: "smfr",
                table: "deceased_managers",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(50)",
                oldMaxLength: 50,
                oldNullable: true,
                oldComment: "장례지도사 연락처 (전화번호)");

            migrationBuilder.AlterColumn<string>(
                name: "deceased_id",
                schema: "smfr",
                table: "deceased_managers",
                type: "character varying(50)",
                maxLength: 50,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(50)",
                oldMaxLength: 50,
                oldComment: "연관된 고인 식별자 (ID)");

            migrationBuilder.AlterColumn<string>(
                name: "id",
                schema: "smfr",
                table: "deceased_managers",
                type: "character varying(50)",
                maxLength: 50,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(50)",
                oldMaxLength: 50,
                oldComment: "매핑 정보 식별자 (ID)");

            migrationBuilder.AlterColumn<double>(
                name: "use_hours",
                schema: "smfr",
                table: "deceased_facilities",
                type: "double precision",
                nullable: false,
                oldClrType: typeof(double),
                oldType: "double precision",
                oldComment: "시설 총 이용 시간");

            migrationBuilder.AlterColumn<decimal>(
                name: "unit_price",
                schema: "smfr",
                table: "deceased_facilities",
                type: "numeric",
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "numeric",
                oldComment: "시설 시간당 이용 단가");

            migrationBuilder.AlterColumn<decimal>(
                name: "total_price",
                schema: "smfr",
                table: "deceased_facilities",
                type: "numeric",
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "numeric",
                oldComment: "시설 이용 총 금액");

            migrationBuilder.AlterColumn<DateTime>(
                name: "start_time",
                schema: "smfr",
                table: "deceased_facilities",
                type: "timestamp with time zone",
                nullable: true,
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone",
                oldNullable: true,
                oldComment: "시설 이용 시작 일시");

            migrationBuilder.AlterColumn<string>(
                name: "remark",
                schema: "smfr",
                table: "deceased_facilities",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(500)",
                oldMaxLength: 500,
                oldNullable: true,
                oldComment: "비고 및 추가 설명");

            migrationBuilder.AlterColumn<string>(
                name: "facility_type",
                schema: "smfr",
                table: "deceased_facilities",
                type: "character varying(50)",
                maxLength: 50,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(50)",
                oldMaxLength: 50,
                oldComment: "시설 유형 (예: MORGUE, WASH_ROOM, HALL, ETC 등)");

            migrationBuilder.AlterColumn<DateTime>(
                name: "end_time",
                schema: "smfr",
                table: "deceased_facilities",
                type: "timestamp with time zone",
                nullable: true,
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone",
                oldNullable: true,
                oldComment: "시설 이용 종료 일시");

            migrationBuilder.AlterColumn<string>(
                name: "deceased_id",
                schema: "smfr",
                table: "deceased_facilities",
                type: "character varying(50)",
                maxLength: 50,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(50)",
                oldMaxLength: 50,
                oldComment: "연관된 고인 식별자 (ID)");

            migrationBuilder.AlterColumn<string>(
                name: "id",
                schema: "smfr",
                table: "deceased_facilities",
                type: "character varying(50)",
                maxLength: 50,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(50)",
                oldMaxLength: 50,
                oldComment: "이용 내역 식별자 (ID)");

            migrationBuilder.AlterColumn<string>(
                name: "signature_file_id",
                schema: "smfr",
                table: "deceased_contractors",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(50)",
                oldMaxLength: 50,
                oldNullable: true,
                oldComment: "계약 서명 이미지 파일 식별자 (ID)");

            migrationBuilder.AlterColumn<string>(
                name: "remark",
                schema: "smfr",
                table: "deceased_contractors",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(500)",
                oldMaxLength: 500,
                oldNullable: true,
                oldComment: "비고 및 추가 설명");

            migrationBuilder.AlterColumn<string>(
                name: "relation",
                schema: "smfr",
                table: "deceased_contractors",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(50)",
                oldMaxLength: 50,
                oldNullable: true,
                oldComment: "고인과의 관계");

            migrationBuilder.AlterColumn<string>(
                name: "name",
                schema: "smfr",
                table: "deceased_contractors",
                type: "character varying(100)",
                maxLength: 100,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(100)",
                oldMaxLength: 100,
                oldComment: "계약자 성명");

            migrationBuilder.AlterColumn<string>(
                name: "deceased_id",
                schema: "smfr",
                table: "deceased_contractors",
                type: "character varying(50)",
                maxLength: 50,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(50)",
                oldMaxLength: 50,
                oldComment: "연관된 고인 식별자 (ID)");

            migrationBuilder.AlterColumn<string>(
                name: "contact",
                schema: "smfr",
                table: "deceased_contractors",
                type: "character varying(50)",
                maxLength: 50,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(50)",
                oldMaxLength: 50,
                oldComment: "계약자 연락처 (전화번호)");

            migrationBuilder.AlterColumn<string>(
                name: "address",
                schema: "smfr",
                table: "deceased_contractors",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(200)",
                oldMaxLength: 200,
                oldNullable: true,
                oldComment: "계약자 주소");

            migrationBuilder.AlterColumn<string>(
                name: "id",
                schema: "smfr",
                table: "deceased_contractors",
                type: "character varying(50)",
                maxLength: 50,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(50)",
                oldMaxLength: 50,
                oldComment: "계약자 식별자 (ID)");

            migrationBuilder.AlterColumn<string>(
                name: "short_name",
                schema: "smfr",
                table: "buildings",
                type: "text",
                nullable: true,
                comment: "짧은건물명",
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true,
                oldComment: "건물 약칭");

            migrationBuilder.AlterColumn<string>(
                name: "remark",
                schema: "smfr",
                table: "buildings",
                type: "text",
                nullable: true,
                comment: "비고/설명",
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true,
                oldComment: "비고 및 추가 설명");

            migrationBuilder.AlterColumn<string>(
                name: "parking_photo_group_id",
                schema: "smfr",
                table: "buildings",
                type: "text",
                nullable: true,
                comment: "주차장 안내 이미지 파일그룹 ID",
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true,
                oldComment: "주차장 안내 이미지 파일 그룹 식별자 (ID)");

            migrationBuilder.AlterColumn<string>(
                name: "company_id",
                schema: "smfr",
                table: "buildings",
                type: "text",
                nullable: false,
                comment: "소속 회사 ID",
                oldClrType: typeof(string),
                oldType: "text",
                oldComment: "소속 회사 식별자 (ID)");

            migrationBuilder.AlterColumn<string>(
                name: "building_photo_group_id",
                schema: "smfr",
                table: "buildings",
                type: "text",
                nullable: true,
                comment: "건물 전경 사진 파일그룹 ID",
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true,
                oldComment: "건물 전경 사진 파일 그룹 식별자 (ID)");

            migrationBuilder.AlterColumn<string>(
                name: "address_detail",
                schema: "smfr",
                table: "buildings",
                type: "text",
                nullable: true,
                comment: "상세주소",
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true,
                oldComment: "상세 주소");

            migrationBuilder.AlterColumn<string>(
                name: "address",
                schema: "smfr",
                table: "buildings",
                type: "text",
                nullable: true,
                comment: "주소",
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true,
                oldComment: "건물 주소");
        }
    }
}
