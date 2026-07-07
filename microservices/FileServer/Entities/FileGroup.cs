using System;
using System.Collections.Generic;
using Funeralv2.Shared.Domain;

namespace FileServer.Entities;

/// <summary>
/// 파일 그룹 엔티티 클래스 (다중 파일 업로드를 그룹 단위로 관리하기 위함)
/// </summary>
public class FileGroup : BaseEntity<Guid>
{
    /// <summary>
    /// 비즈니스 구분 코드 (예: PROFILE, BOARD, ITEM 등)
    /// </summary>
    public string BizType { get; set; } = string.Empty;

    /// <summary>
    /// 그룹 내에 소속된 파일 상세 메타데이터 목록 탐색 속성 (1:N 관계)
    /// </summary>
    public ICollection<FileMetadata> Files { get; set; } = new List<FileMetadata>();
}
