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
    public Guid? ThumbnailFileId { get; set; }
    public string? WebmUrl { get; set; }
    public Guid? WebmFileId { get; set; }
    public string? OggUrl { get; set; }
    public Guid? OggFileId { get; set; }
    public string? AacUrl { get; set; }
    public Guid? AacFileId { get; set; }
    public Guid? OriginalFileId { get; set; }
    public string Status { get; set; } = "COMPLETED";
    public string? ErrorMessage { get; set; }
    public DateTime? ConversionStartedAt { get; set; }
    public DateTime? ConversionCompletedAt { get; set; }
    public string? ConversionCommand { get; set; }
    public bool HasWebm { get; set; }
    public bool HasThumbnail { get; set; }
    public bool HasOgg { get; set; }
    public bool HasAac { get; set; }
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
    public Guid? ThumbnailFileId { get; set; }
    public string? WebmUrl { get; set; }
    public Guid? WebmFileId { get; set; }
    public string? OggUrl { get; set; }
    public Guid? OggFileId { get; set; }
    public string? AacUrl { get; set; }
    public Guid? AacFileId { get; set; }
    public Guid? OriginalFileId { get; set; }
    public string Status { get; set; } = "PROCESSING";
    public string? ErrorMessage { get; set; }
    public DateTime? ConversionStartedAt { get; set; }
    public DateTime? ConversionCompletedAt { get; set; }
    public string? ConversionCommand { get; set; }
    public bool HasWebm { get; set; }
    public bool HasThumbnail { get; set; }
    public bool HasOgg { get; set; }
    public bool HasAac { get; set; }

    public long? FileSize { get; set; }
    public int SortOrder { get; set; }
    public string? Remark { get; set; }
}

/// <summary>
/// 미디어 소스 변환 상태 업데이트 DTO
/// </summary>
public class MediaSourceStatusUpdateDto
{
    public string? Status { get; set; }
    public string? ErrorMessage { get; set; }
    public DateTime? ConversionStartedAt { get; set; }
    public DateTime? ConversionCompletedAt { get; set; }
    public string? ConversionCommand { get; set; }
    public bool? HasWebm { get; set; }
    public bool? HasThumbnail { get; set; }
    public bool? HasOgg { get; set; }
    public bool? HasAac { get; set; }
    public string? ThumbnailUrl { get; set; }
    public Guid? ThumbnailFileId { get; set; }
    public string? WebmUrl { get; set; }
    public Guid? WebmFileId { get; set; }
    public string? OggUrl { get; set; }
    public Guid? OggFileId { get; set; }
    public string? AacUrl { get; set; }
    public Guid? AacFileId { get; set; }
}

/// <summary>
/// 미디어 소스 정보 수정 DTO
/// </summary>
public class MediaSourceUpdateDto
{
    public string Name { get; set; } = null!;
    public string? ShortName { get; set; }
    public int SortOrder { get; set; }
    public string? Remark { get; set; }
}
