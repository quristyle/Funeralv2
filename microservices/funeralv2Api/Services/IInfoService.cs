using funeralv2Api.DTOs;

namespace funeralv2Api.Services;

/// <summary>
/// 정보 화면 묶음 서비스 — 호실히스토리 · 고인정보조회 · 나의정보 · 미리보기.
/// </summary>
/// <remarks>
/// 알림정보는 2026-09-03 에 걷어냈다 (<see cref="Endpoints.InfoEndpoints"/> 머리말).
/// </remarks>
public interface IInfoService
{
    // ── 호실 히스토리 ───────────────────────────────────────────

    /// <summary>
    /// 호실을 거쳐 간 고인들. 끝난 것과 지금 쓰는 것을 모두 담는다.
    /// </summary>
    /// <param name="keyword">고인 성명 일부. 이름으로 바로 찾을 때 쓴다.</param>
    /// <param name="inUse">
    /// <c>true</c> 사용 중만 · <c>false</c> 출상만 · <c>null</c> 둘 다.
    /// </param>
    Task<List<RoomHistoryDto>> GetRoomHistoriesAsync(
        string? buildingId, string? roomId, DateTime? from, DateTime? to,
        string? keyword, bool? inUse);

    // ── 고인 정보 조회 ──────────────────────────────────────────

    /// <summary>고인을 이름·기간·건물로 찾는다.</summary>
    Task<List<DeceasedLookupDto>> SearchDeceasedAsync(string? keyword, string? buildingId, string? roomId, DateTime? from, DateTime? to, string? status);

    // ── 나의 정보 ───────────────────────────────────────────────

    Task<MyInfoDto> GetMyInfoAsync(string userId, string? role);

    // ── 미리보기 ────────────────────────────────────────────────

    /// <summary>미리볼 수 있는 장비 목록</summary>
    Task<List<DevicePreviewDto>> GetDevicePreviewsAsync(string? buildingId, string? roomId);
}
