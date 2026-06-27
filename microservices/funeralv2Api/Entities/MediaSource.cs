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
    /// 동영상인 경우 첫 클립 썸네일 이미지의 파일 URL 경로
    /// </summary>
    public string? ThumbnailUrl { get; set; }
}
