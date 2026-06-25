using System;
using System.Collections.Generic;
using Funeralv2.Shared.Domain;

namespace FileServer.Entities;

/// <summary>
/// 파일 그룹 엔티티 클래스
/// </summary>
public class FileGroup : BaseEntity<Guid>
{
    /// <summary>
    /// 비즈니스 구분 (예: PROFILE, BOARD, ITEM 등)
    /// </summary>
    public string BizType { get; set; } = string.Empty;

    /// <summary>
    /// 그룹 내에 속한 파일 메타데이터 목록
    /// </summary>
    public ICollection<FileMetadata> Files { get; set; } = new List<FileMetadata>();
}
