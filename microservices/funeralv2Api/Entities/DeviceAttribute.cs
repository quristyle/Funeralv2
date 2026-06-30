using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Funeralv2.Shared.Domain;

namespace funeralv2Api.Entities;

/// <summary>
/// 장비 속성 정보 (Device 1:N 관계)
/// 장비 유형별 세부 설정 값을 key-value 형태 또는 구조화된 컬럼으로 관리합니다.
/// </summary>
[Table("device_attributes", Schema = "smfr")]
public class DeviceAttribute : BaseEntity<string>
{
    public DeviceAttribute()
    {
        Id = Guid.NewGuid().ToString();
    }

    /// <summary>장비 FK</summary>
    [Required]
    [Column("device_id")]
    public string DeviceId { get; set; } = string.Empty;

    [ForeignKey(nameof(DeviceId))]
    public Device? Device { get; set; }

    // ─── 공통 표시 설정 ──────────────────────────────────────────
    /// <summary>표시 방향 (LANDSCAPE / PORTRAIT)</summary>
    [Column("display_orientation")]
    [MaxLength(20)]
    public string DisplayOrientation { get; set; } = "LANDSCAPE";

    /// <summary>콘텐츠 전환 간격(초)</summary>
    [Column("content_interval_sec")]
    public int ContentIntervalSec { get; set; } = 10;

    /// <summary>대기 화면 활성화 여부</summary>
    [Column("is_screensaver_enabled")]
    public bool IsScreensaverEnabled { get; set; } = false;

    /// <summary>대기 화면 전환 대기 시간(초)</summary>
    [Column("screensaver_timeout_sec")]
    public int ScreensaverTimeoutSec { get; set; } = 300;

    // ─── 영정사진/추모 콘텐츠 설정 (DID_MEMORIAL) ────────────────
    /// <summary>영정사진 표시 여부</summary>
    [Column("is_memorial_photo_enabled")]
    public bool IsMemorialPhotoEnabled { get; set; } = false;

    /// <summary>영정사진 표시 효과 (FADE / SLIDE / NONE)</summary>
    [Column("memorial_photo_effect")]
    [MaxLength(20)]
    public string MemorialPhotoEffect { get; set; } = "FADE";

    /// <summary>고인 이름 표시 여부</summary>
    [Column("is_deceased_name_visible")]
    public bool IsDeceasedNameVisible { get; set; } = true;

    /// <summary>유족 연락처 표시 여부</summary>
    [Column("is_family_contact_visible")]
    public bool IsFamilyContactVisible { get; set; } = false;

    // ─── 멀티미디어 콘텐츠 설정 (DID_MULTIMEDIA) ─────────────────
    /// <summary>동영상 재생 활성화 여부</summary>
    [Column("is_video_enabled")]
    public bool IsVideoEnabled { get; set; } = false;

    /// <summary>음악 재생 활성화 여부</summary>
    [Column("is_music_enabled")]
    public bool IsMusicEnabled { get; set; } = false;

    /// <summary>재생 동영상 ID</summary>
    [Column("video_id")]
    [MaxLength(50)]
    public string? VideoId { get; set; }

    /// <summary>재생 음악 ID</summary>
    [Column("music_id")]
    [MaxLength(50)]
    public string? MusicId { get; set; }

    /// <summary>음악 재생 볼륨 (0-100, null이면 장비 기본값 사용)</summary>
    [Column("music_volume")]
    public int? MusicVolume { get; set; }

    /// <summary>동영상/음악 반복 재생 여부</summary>
    [Column("is_media_loop")]
    public bool IsMediaLoop { get; set; } = true;

    /// <summary>음소거 여부</summary>
    [Column("is_muted")]
    public bool IsMuted { get; set; } = false;

    // ─── 층별 안내 설정 (SIGNBOARD_FLOOR) ────────────────────────
    /// <summary>층별 안내판 표시 활성화 여부</summary>
    [Column("is_floor_guide_enabled")]
    public bool IsFloorGuideEnabled { get; set; } = false;

    /// <summary>빈소 배정 현황 표시 여부</summary>
    [Column("is_room_assignment_visible")]
    public bool IsRoomAssignmentVisible { get; set; } = true;

    /// <summary>현재 진행 중인 빈소 목록만 표시 여부</summary>
    [Column("is_active_rooms_only")]
    public bool IsActiveRoomsOnly { get; set; } = true;

    /// <summary>층별 안내 새로고침 간격(초)</summary>
    [Column("floor_guide_refresh_sec")]
    public int FloorGuideRefreshSec { get; set; } = 30;

    // ─── 입구 정보/키오스크 설정 (KIOSK_ENTRANCE) ────────────────
    /// <summary>터치 인터랙션 활성화 여부</summary>
    [Column("is_touch_enabled")]
    public bool IsTouchEnabled { get; set; } = false;

    /// <summary>QR코드 표시 여부</summary>
    [Column("is_qr_code_visible")]
    public bool IsQrCodeVisible { get; set; } = false;

    /// <summary>건물 전체 안내도 표시 여부</summary>
    [Column("is_building_map_visible")]
    public bool IsBuildingMapVisible { get; set; } = true;

    /// <summary>입구 인사말 메시지</summary>
    [Column("entrance_greeting")]
    [MaxLength(200)]
    public string? EntranceGreeting { get; set; }

    /// <summary>공지사항 표시 여부</summary>
    [Column("is_notice_visible")]
    public bool IsNoticeVisible { get; set; } = true;

    /// <summary>공지사항 스크롤 속도 (1=느림, 5=빠름)</summary>
    [Column("notice_scroll_speed")]
    public int NoticeScrollSpeed { get; set; } = 2;

    // ─── 기타 확장 속성 ──────────────────────────────────────────
    /// <summary>비고</summary>
    [Column("remark")]
    [MaxLength(500)]
    public string? Remark { get; set; }
}
