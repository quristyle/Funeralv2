using AuthServer.DTOs;

namespace AuthServer.Services;

/// <summary>
/// 보고서 메일 배치 — 고를 수 있는 보고서, 배치의 등록·수정·삭제,
/// 받게 되는 사람 미리보기, 그리고 한 건 보내기.
/// </summary>
public interface IReportMailService
{
    /// <summary>고를 수 있는 보고서들. 메뉴에서 읽는다.</summary>
    Task<List<ReportCatalogItemDto>> GetCatalogAsync(CancellationToken ct = default);

    /// <summary>배치 목록.</summary>
    Task<List<ReportMailScheduleDto>> GetSchedulesAsync(
        string? keyword, bool activeOnly, CancellationToken ct = default);

    /// <summary>배치 하나. 없으면 <c>null</c>.</summary>
    Task<ReportMailScheduleDto?> GetScheduleAsync(string id, CancellationToken ct = default);

    /// <summary>배치를 만든다. 만든 줄을 돌려준다.</summary>
    Task<ReportMailScheduleDto> CreateAsync(
        SaveReportMailScheduleDto request, string actor, CancellationToken ct = default);

    /// <summary>배치를 고친다. 없으면 <c>null</c>.</summary>
    Task<ReportMailScheduleDto?> UpdateAsync(
        string id, SaveReportMailScheduleDto request, string actor, CancellationToken ct = default);

    /// <summary>배치를 지운다(soft). 없으면 <c>false</c>.</summary>
    Task<bool> DeleteAsync(string id, string actor, CancellationToken ct = default);

    /// <summary>이 배치의 메일을 받게 되는 사람들.</summary>
    Task<ReportMailRecipientsDto?> GetRecipientsAsync(string id, CancellationToken ct = default);

    /// <summary>
    /// 지금 한 번 보낸다. 주기와 무관하고 <c>LastSentAt</c> 도 건드리지 않는다 —
    /// 그 까닭은 구현의 머리말에 있다.
    /// </summary>
    Task<(bool Ok, string Message)> SendNowAsync(
        string id, string actor, CancellationToken ct = default);
}
