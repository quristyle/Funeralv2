using funeralv2Api.DTOs;

namespace funeralv2Api.Services;

/// <summary>
/// 통계 화면 서비스 — 과금 내역 · 빈소 사용 내역.
/// </summary>
public interface IStatService
{
    /// <summary>고인별 과금 내역</summary>
    Task<List<BillingDto>> GetBillingAsync(string? buildingId, DateTime? from, DateTime? to);

    /// <summary>빈소 사용 내역</summary>
    Task<List<RoomUsageDto>> GetRoomUsageAsync(string? buildingId, string? roomId, DateTime? from, DateTime? to);

    /// <summary>두 화면 위에 얹는 요약 숫자</summary>
    Task<StatSummaryDto> GetSummaryAsync(string? buildingId, DateTime? from, DateTime? to);
}
