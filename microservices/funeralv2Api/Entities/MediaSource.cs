using System;
using System.ComponentModel.DataAnnotations.Schema;
using Funeralv2.Shared.Domain;

namespace funeralv2Api.Entities;

/// <summary>
/// 미디어 소스 (동영상, 음원, 이미지 등) 리소스를 저장하는 엔티티 클래스
/// </summary>
[Table("media_sources", Schema = "smfr")]
public class MediaSource : BaseEntity<string>
{
    public MediaSource()
    {
        Id = Guid.NewGuid().ToString("N");
    }

    /// <summary>
    /// 미디어 소스 명칭
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// 영상 짧은 명칭
    /// </summary>
    public string? ShortName { get; set; }

    /// <summary>
    /// 미디어 유형 (VIDEO, AUDIO, IMAGE)
    /// </summary>
    public string SourceType { get; set; } = "VIDEO";

    /// <summary>
    /// 파일 URL 경로 (FileServer 등 보관 경로)
    /// </summary>
    public string Url { get; set; } = string.Empty;

    /// <summary>
    /// 파일 크기 (Bytes)
    /// </summary>
    public long? FileSize { get; set; }

    /// <summary>
    /// 정렬 순서
    /// </summary>
    public int SortOrder { get; set; }

    /// <summary>
    /// 설명 및 비고
    /// </summary>
    public string? Remark { get; set; }

    /// <summary>
    /// 원본 파일의 FileMetadata 식별자
    /// </summary>
    public Guid? OriginalFileId { get; set; }

    /// <summary>
    /// 동영상인 경우 첫 클립 썸네일 이미지의 파일 URL 경로
    /// </summary>
    public string? ThumbnailUrl { get; set; }

    /// <summary>
    /// 썸네일 이미지 파일의 FileMetadata 식별자
    /// </summary>
    public Guid? ThumbnailFileId { get; set; }
    
    /// <summary>
    /// 동영상인 경우 WebM 파일의 파일 URL 경로
    /// </summary>
    public string? WebmUrl { get; set; }

    /// <summary>
    /// WebM 파일의 FileMetadata 식별자
    /// </summary>
    public Guid? WebmFileId { get; set; }

    /// <summary>
    /// 음원인 경우 OGG 파일의 파일 URL 경로
    /// </summary>
    public string? OggUrl { get; set; }

    /// <summary>
    /// OGG 파일의 FileMetadata 식별자
    /// </summary>
    public Guid? OggFileId { get; set; }

    /// <summary>
    /// 음원인 경우 AAC 파일의 파일 URL 경로
    /// </summary>
    public string? AacUrl { get; set; }

    /// <summary>
    /// AAC 파일의 FileMetadata 식별자
    /// </summary>
    public Guid? AacFileId { get; set; }

    /// <summary>
    /// 미디어 변환 상태 (PROCESSING, COMPLETED, FAILED)
    /// </summary>
    public string Status { get; set; } = "COMPLETED";

    /// <summary>
    /// WebM 파일 변환 완료 여부
    /// </summary>
    public bool HasWebm { get; set; }

    /// <summary>
    /// 썸네일 이미지 생성 완료 여부
    /// </summary>
    public bool HasThumbnail { get; set; }

    /// <summary>
    /// OGG 파일 변환 완료 여부
    /// </summary>
    public bool HasOgg { get; set; }

    /// <summary>
    /// AAC 파일 변환 완료 여부
    /// </summary>
    public bool HasAac { get; set; }
}
