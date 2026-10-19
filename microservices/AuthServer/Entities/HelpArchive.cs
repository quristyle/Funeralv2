using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using JSini.Shared.Domain;

namespace AuthServer.Entities;

/// <summary>자료실 항목(<c>/help/archive</c>). F.A.Q 와 같은 방침이다 — 관리 포털이 관리하고 모든 사용자에게 공통으로 보인다.</summary>
[Table("help_archives", Schema = "scom")]
public class HelpArchive : BaseEntity<string>
{
    public HelpArchive()
    {
        Id = Guid.NewGuid().ToString();
    }

    /// <summary>분류. 비우면 화면이 '기타' 로 묶어 보여준다.</summary>
    [Column("category")]
    public string? Category { get; set; }

    /// <summary>자료명</summary>
    [Required]
    [Column("title")]
    public string Title { get; set; } = string.Empty;

    /// <summary>자료 설명 (HTML). 이 화면의 핵심이다 — 무엇을 내려받는 것인지 알려 준다.</summary>
    [Column("description")]
    public string? Description { get; set; }

    /// <summary>노출 순서 (작을수록 먼저)</summary>
    [Column("order_no")]
    public int OrderNo { get; set; }

    /// <summary>사용 상태 (0: 비활성, 1: 활성). 비활성은 관리자에게만 보인다.</summary>
    [Column("status")]
    public int Status { get; set; } = 1;

    /// <summary>내려받은 횟수 (항목 기준 합계)</summary>
    [Column("download_count")]
    public int DownloadCount { get; set; }

    /// <summary>첨부파일 목록</summary>
    public ICollection<HelpArchiveFile>? Files { get; set; }
}

/// <summary>자료실 첨부파일. 모양은 NoticeFile 과 맞춘다 — 첨부는 전부 FileServer 로 가고 여기는 발급받은 file_id 만 든다.</summary>
[Table("help_archive_files", Schema = "scom")]
public class HelpArchiveFile : BaseEntity<string>
{
    public HelpArchiveFile()
    {
        Id = Guid.NewGuid().ToString();
    }

    /// <summary>자료실 항목 아이디</summary>
    [Required]
    [Column("archive_id")]
    public string ArchiveId { get; set; } = string.Empty;

    [ForeignKey(nameof(ArchiveId))]
    public virtual HelpArchive? Archive { get; set; }

    /// <summary>FileServer 가 발급한 파일 아이디</summary>
    [Required]
    [Column("file_id")]
    public string FileId { get; set; } = string.Empty;

    /// <summary>원본 파일명. 내려받을 때 이 이름으로 저장된다.</summary>
    [Required]
    [Column("file_name")]
    public string FileName { get; set; } = string.Empty;

    /// <summary>바이트 크기. 목록에서 얼마나 큰 파일인지 미리 알려 준다.</summary>
    [Column("file_size")]
    public long FileSize { get; set; }

    /// <summary>MIME 타입</summary>
    [Column("content_type")]
    public string? ContentType { get; set; }

    /// <summary>정렬 순서</summary>
    [Column("sort_no")]
    public int SortNo { get; set; }

    /// <summary>이 파일이 내려받힌 횟수</summary>
    [Column("download_count")]
    public int DownloadCount { get; set; }
}
