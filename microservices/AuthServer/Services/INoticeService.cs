using AuthServer.DTOs;

namespace AuthServer.Services;

/// <summary>
/// 공지 서비스 인터페이스
/// </summary>
public interface INoticeService
{
    /// <summary>관리 화면용 전체 목록 (비활성·기간 지난 것 포함)</summary>
    Task<List<NoticeDto>> GetAllAsync(string? keyword);

    /// <summary>단건 조회</summary>
    Task<NoticeDto?> GetByIdAsync(string id);

    /// <summary>
    /// 지금 팝업으로 띄울 공지.
    /// </summary>
    /// <param name="publicOnly">
    /// 참이면 로그인 없이 볼 수 있는 공지만 (`is_public`). 로그인 전 화면에서 쓴다.
    /// 거짓이면 전부. 로그인 뒤에 쓴다.
    /// </param>
    Task<List<NoticeDto>> GetPopupAsync(bool publicOnly);

    Task<NoticeDto> CreateAsync(SaveNoticeDto request, string? userId);
    Task<bool> UpdateAsync(string id, SaveNoticeDto request, string? userId);
    Task<bool> DeleteAsync(string id, string? userId);
}
