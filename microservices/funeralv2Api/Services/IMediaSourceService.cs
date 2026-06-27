using funeralv2Api.DTOs;

namespace funeralv2Api.Services;

/// <summary>
/// 미디어 소스 관리 서비스 인터페이스
/// </summary>
public interface IMediaSourceService
{
    /// <summary>
    /// 지정한 미디어 타입의 리소스 목록 조회
    /// </summary>
    Task<List<MediaSourceDto>> GetMediaSourcesAsync(string? type);

    /// <summary>
    /// 미디어 리소스 생성
    /// </summary>
    Task<MediaSourceDto> CreateMediaSourceAsync(MediaSourceCreateDto dto);

    /// <summary>
    /// 미디어 리소스 삭제
    /// </summary>
    Task<bool> DeleteMediaSourceAsync(string id);

    /// <summary>
    /// 미디어 리소스 변환 상태 업데이트
    /// </summary>
    Task<MediaSourceDto?> UpdateMediaSourceStatusAsync(string id, MediaSourceStatusUpdateDto dto);
}
