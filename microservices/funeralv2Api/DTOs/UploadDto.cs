namespace funeralv2Api.DTOs;

/// <summary>
/// 파일 업로드 처리 결과를 담는 DTO
/// </summary>
public class UploadResultDto
{
    /// <summary>
    /// 업로드된 파일의 접근 가능 URL
    /// </summary>
    public string Url { get; set; } = string.Empty;

    /// <summary>
    /// 원본 또는 저장된 파일 명칭
    /// </summary>
    public string Filename { get; set; } = string.Empty;

    /// <summary>
    /// 파일 크기 (bytes)
    /// </summary>
    public long Size { get; set; }
}
