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

    /// <summary>
    /// 미디어 리소스 썸네일 재추출
    /// </summary>
    Task<bool> RetryThumbnailAsync(string id);

    /// <summary>
    /// 미디어 리소스 webm 재변환
    /// </summary>
    Task<bool> RetryWebmAsync(string id);

    /// <summary>
    /// 미디어 리소스 audio 재변환
    /// </summary>
    Task<bool> RetryAudioAsync(string id);

    /// <summary>
    /// 지정한 미디어 리소스 상세 조회
    /// </summary>
    Task<MediaSourceDto?> GetMediaSourceByIdAsync(string id);

    /// <summary>
    /// 미디어 리소스 정보 수정
    /// </summary>
    Task<MediaSourceDto?> UpdateMediaSourceAsync(string id, MediaSourceUpdateDto dto);
}
