using System;
using JSini.Shared.Domain;

namespace FileServer.Entities;

/// <summary>
/// 개별 파일의 메타데이터 및 상세 정보를 저장하는 엔티티 클래스
/// </summary>
public class FileMetadata : BaseEntity<Guid>
{
    /// <summary>
    /// 업로드 당시의 원본 파일명 (예: photo.png)
    /// </summary>
    public string OriginalName { get; set; } = string.Empty;

    /// <summary>
    /// 스토리지에 실제 저장된 난수화된 파일명 (예: uuid.png)
    /// </summary>
    public string StoredName { get; set; } = string.Empty;

    /// <summary>
    /// 파일이 위치한 물리적 디렉토리 상대경로
    /// </summary>
    public string Path { get; set; } = string.Empty;

    /// <summary>
    /// 파일 크기 (단위: Bytes)
    /// </summary>
    public long Size { get; set; }

    /// <summary>
    /// HTTP Content-Type (MIME 타입, 예: image/png, application/pdf 등)
    /// </summary>
    public string ContentType { get; set; } = string.Empty;

    /// <summary>
    /// 이미지 파일 형식 여부
    /// </summary>
    public bool IsImage { get; set; }

    /// <summary>
    /// 소속된 파일 그룹 식별자 (ID, Nullable)
    /// </summary>
    public Guid? FileGroupId { get; set; }

    /// <summary>
    /// 그룹 내 대표 파일(썸네일 등) 여부
    /// </summary>
    public bool IsRepresentative { get; set; }

    /// <summary>
    /// 정렬 순서
    /// </summary>
    public int SortOrder { get; set; }

    /// <summary>
    /// 로그인하지 않은 사람에게도 내려줄 파일인지 여부.
    /// 기본은 false 다. 회사 소개 사이트의 공개 자료실처럼 익명 열람이 필요한 파일만 켠다.
    /// </summary>
    public bool IsPublic { get; set; }

    /// <summary>
    /// 소속 파일 그룹 엔티티 탐색 속성
    /// </summary>
    public FileGroup? FileGroup { get; set; }
}
