using System.ComponentModel.DataAnnotations;

namespace funeralv2Api.DTOs;

/// <summary>
/// 장비 속성 응답 DTO
/// </summary>
public class DeviceAttributeDto
{
    public string Id { get; set; } = string.Empty;
    public string DeviceId { get; set; } = string.Empty;

    // 공통 표시 설정
    public string DisplayOrientation { get; set; } = "LANDSCAPE";
    public string PortraitOrientation { get; set; } = "HORIZONTAL";
    public string VideoOrientation { get; set; } = "HORIZONTAL";
    public decimal? DisplayPaddingTop { get; set; } = 0;
    public decimal? DisplayPaddingLeft { get; set; } = 0;
    public decimal? DisplayPaddingRight { get; set; } = 0;
    public decimal? DisplayPaddingBottom { get; set; } = 0;
    public int ContentIntervalSec { get; set; } = 10;
    public bool IsScreensaverEnabled { get; set; } = false;
    public int ScreensaverTimeoutSec { get; set; } = 300;

    // 영정사진/추모 콘텐츠 설정
    public bool IsMemorialPhotoEnabled { get; set; } = false;
    public string MemorialPhotoEffect { get; set; } = "FADE";
    public bool IsDeceasedNameVisible { get; set; } = true;
    public bool IsFamilyContactVisible { get; set; } = false;
    public decimal? MemorialPaddingTop { get; set; } = 0;
    public decimal? MemorialPaddingLeft { get; set; } = 0;
    public decimal? MemorialPaddingRight { get; set; } = 0;
    public decimal? MemorialPaddingBottom { get; set; } = 0;

    // 멀티미디어 콘텐츠 설정
    public bool IsVideoEnabled { get; set; } = false;
    public bool IsMusicEnabled { get; set; } = false;
    public string? VideoId { get; set; }
    public string? MusicId { get; set; }
    public int? MusicVolume { get; set; }
    public bool IsMediaLoop { get; set; } = true;
    public bool IsMuted { get; set; } = false;

    // 층별 안내 설정
    public bool IsFloorGuideEnabled { get; set; } = false;
    public bool IsRoomAssignmentVisible { get; set; } = true;
    public bool IsActiveRoomsOnly { get; set; } = true;
    public int FloorGuideRefreshSec { get; set; } = 30;

    // 입구 정보/키오스크 설정
    public bool IsTouchEnabled { get; set; } = false;
    public bool IsQrCodeVisible { get; set; } = false;
    public bool IsBuildingMapVisible { get; set; } = true;
    public string? EntranceGreeting { get; set; }
    public bool IsNoticeVisible { get; set; } = true;
    public int NoticeScrollSpeed { get; set; } = 2;

    public string? Remark { get; set; }
}

/// <summary>
/// 장비 속성 생성/수정 DTO (Upsert 방식 단일 DTO)
/// </summary>
public class DeviceAttributeUpsertDto
{
    [Required(ErrorMessage = "장비 ID는 필수입니다.")]
    public string DeviceId { get; set; } = string.Empty;

    // 공통 표시 설정
    public string DisplayOrientation { get; set; } = "LANDSCAPE";
    public string PortraitOrientation { get; set; } = "HORIZONTAL";
    public string VideoOrientation { get; set; } = "HORIZONTAL";
    public decimal? DisplayPaddingTop { get; set; } = 0;
    public decimal? DisplayPaddingLeft { get; set; } = 0;
    public decimal? DisplayPaddingRight { get; set; } = 0;
    public decimal? DisplayPaddingBottom { get; set; } = 0;
    public int ContentIntervalSec { get; set; } = 10;
    public bool IsScreensaverEnabled { get; set; } = false;
    public int ScreensaverTimeoutSec { get; set; } = 300;

    // 영정사진/추모 콘텐츠 설정
    public bool IsMemorialPhotoEnabled { get; set; } = false;
    public string MemorialPhotoEffect { get; set; } = "FADE";
    public bool IsDeceasedNameVisible { get; set; } = true;
    public bool IsFamilyContactVisible { get; set; } = false;
    public decimal? MemorialPaddingTop { get; set; } = 0;
    public decimal? MemorialPaddingLeft { get; set; } = 0;
    public decimal? MemorialPaddingRight { get; set; } = 0;
    public decimal? MemorialPaddingBottom { get; set; } = 0;

    // 멀티미디어 콘텐츠 설정
    public bool IsVideoEnabled { get; set; } = false;
    public bool IsMusicEnabled { get; set; } = false;
    public string? VideoId { get; set; }
    public string? MusicId { get; set; }
    public int? MusicVolume { get; set; }
    public bool IsMediaLoop { get; set; } = true;
    public bool IsMuted { get; set; } = false;

    // 층별 안내 설정
    public bool IsFloorGuideEnabled { get; set; } = false;
    public bool IsRoomAssignmentVisible { get; set; } = true;
    public bool IsActiveRoomsOnly { get; set; } = true;
    public int FloorGuideRefreshSec { get; set; } = 30;

    // 입구 정보/키오스크 설정
    public bool IsTouchEnabled { get; set; } = false;
    public bool IsQrCodeVisible { get; set; } = false;
    public bool IsBuildingMapVisible { get; set; } = true;
    public string? EntranceGreeting { get; set; }
    public bool IsNoticeVisible { get; set; } = true;
    public int NoticeScrollSpeed { get; set; } = 2;

    public string? Remark { get; set; }
}
