using System.ComponentModel.DataAnnotations;

namespace funeralv2Api.DTOs;

/// <summary>
/// 미디어 소스 상세 정보 응답 DTO
/// </summary>
public class MediaSourceDto
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? ShortName { get; set; }
    public string SourceType { get; set; } = "VIDEO";
    public string Url { get; set; } = string.Empty;
    public string? ThumbnailUrl { get; set; }
    public string? WebmUrl { get; set; }
    public string Status { get; set; } = "COMPLETED";
    public bool HasWebm { get; set; }
    public bool HasThumbnail { get; set; }
    public long? FileSize { get; set; }
    public int SortOrder { get; set; }
    public string? Remark { get; set; }
}

/// <summary>
/// 미디어 소스 생성 DTO
/// </summary>
public class MediaSourceCreateDto
{
    [Required(ErrorMessage = "소스 명칭은 필수입니다.")]
    public string Name { get; set; } = string.Empty;

    public string? ShortName { get; set; }

    [Required(ErrorMessage = "소스 유형은 필수입니다.")]
    public string SourceType { get; set; } = "VIDEO";

    [Required(ErrorMessage = "파일 경로는 필수입니다.")]
    public string Url { get; set; } = string.Empty;

    public string? ThumbnailUrl { get; set; }
    public string? WebmUrl { get; set; }
    public string Status { get; set; } = "READY";
    public bool HasWebm { get; set; }
    public bool HasThumbnail { get; set; }

    public long? FileSize { get; set; }
    public int SortOrder { get; set; }
    public string? Remark { get; set; }
}

/// <summary>
/// 미디어 소스 변환 상태 업데이트 DTO
/// </summary>
public class MediaSourceStatusUpdateDto
{
    public string Status { get; set; } = "COMPLETED";
    public bool HasWebm { get; set; }
    public bool HasThumbnail { get; set; }
    public string? ThumbnailUrl { get; set; }
    public string? WebmUrl { get; set; }
}
