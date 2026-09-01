using funeralv2Api.DTOs;

namespace funeralv2Api.Services;

/// <summary>
/// 정보 화면 묶음 서비스 — 알림정보 · 호실히스토리 · 고인정보조회 · 나의정보 · 미리보기.
/// </summary>
public interface IInfoService
{
    // ── 알림정보 ────────────────────────────────────────────────

    /// <summary>지금 이 사람이 볼 수 있는 알림. 전체 공지와 본인 앞으로 온 것을 합친다.</summary>
    Task<List<NoticeDto>> GetNoticesAsync(string userId, string? buildingId, bool includeExpired);

    Task<NoticeDto?> GetNoticeByIdAsync(string userId, string id);

    Task<NoticeDto> CreateNoticeAsync(string userId, NoticeCreateDto dto);

    Task<NoticeDto?> UpdateNoticeAsync(string userId, string id, NoticeUpdateDto dto);

    Task<bool> DeleteNoticeAsync(string id);

    /// <summary>읽음으로 표시한다. 이미 읽었으면 아무 일도 하지 않는다.</summary>
    Task<bool> MarkNoticeReadAsync(string userId, string id);

    /// <summary>안 읽은 알림 수</summary>
    Task<int> CountUnreadNoticesAsync(string userId, string? buildingId);

    // ── 호실 히스토리 ───────────────────────────────────────────

    /// <summary>호실을 거쳐 간 고인들. 끝난 것과 지금 쓰는 것을 모두 담는다.</summary>
    Task<List<RoomHistoryDto>> GetRoomHistoriesAsync(string? buildingId, string? roomId, DateTime? from, DateTime? to);

    // ── 고인 정보 조회 ──────────────────────────────────────────

    /// <summary>고인을 이름·기간·건물로 찾는다.</summary>
    Task<List<DeceasedLookupDto>> SearchDeceasedAsync(string? keyword, string? buildingId, string? roomId, DateTime? from, DateTime? to, string? status);

    // ── 나의 정보 ───────────────────────────────────────────────

    Task<MyInfoDto> GetMyInfoAsync(string userId, string? role);

    // ── 미리보기 ────────────────────────────────────────────────

    /// <summary>미리볼 수 있는 장비 목록</summary>
    Task<List<DevicePreviewDto>> GetDevicePreviewsAsync(string? buildingId, string? roomId);
}
