using System;
using Funeralv2.Shared.Domain;

namespace FileServer.Entities;

/// <summary>
/// 파일의 메타데이터 정보를 저장하는 엔티티 클래스
/// </summary>
public class FileMetadata : BaseEntity<Guid>
{
    /// <summary>
    /// 원본 파일명 (예: photo.png)
    /// </summary>
    public string OriginalName { get; set; } = string.Empty;

    /// <summary>
    /// 실제 스토리지에 저장된 파일명 (예: uuid.png)
    /// </summary>
    public string StoredName { get; set; } = string.Empty;

    /// <summary>
    /// 파일 저장 경로 (디렉토리 상대경로)
    /// </summary>
    public string Path { get; set; } = string.Empty;

    /// <summary>
    /// 파일 크기 (Bytes)
    /// </summary>
    public long Size { get; set; }

    /// <summary>
    /// Content Type (MIME Type)
    /// </summary>
    public string ContentType { get; set; } = string.Empty;

    /// <summary>
    /// 이미지 파일 여부
    /// </summary>
    public bool IsImage { get; set; }

    /// <summary>
    /// 파일 그룹 ID (Nullable)
    /// </summary>
    public Guid? FileGroupId { get; set; }

    /// <summary>
    /// 대표 파일 여부
    /// </summary>
    public bool IsRepresentative { get; set; }

    /// <summary>
    /// 정렬 순서
    /// </summary>
    public int SortOrder { get; set; }

    /// <summary>
    /// 파일 그룹 탐색 속성
    /// </summary>
    public FileGroup? FileGroup { get; set; }
}
