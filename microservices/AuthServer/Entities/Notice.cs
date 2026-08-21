using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using JSini.Shared.Domain;

namespace AuthServer.Entities;

/// <summary>
/// 공지 엔티티
/// </summary>
/// <remarks>
/// 공지는 JSini 관리 포털이 관리하고 모든 MSA 사용자에게 공통으로 보인다.
/// 각 MSA 가 자기 공지를 따로 두지 않는다.
/// </remarks>
[Table("notices", Schema = "scom")]
public class Notice : BaseEntity<string>
{
    public Notice()
    {
        Id = Guid.NewGuid().ToString();
    }

    /// <summary>제목</summary>
    [Required]
    [Column("title")]
    public string Title { get; set; } = string.Empty;

    /// <summary>본문 (HTML)</summary>
    [Column("content")]
    public string? Content { get; set; }

    /// <summary>
    /// 로그인하지 않은 사용자도 볼 수 있는지.
    /// 켜면 화면이 뜨자마자 팝업으로 보여주고, 끄면 로그인한 뒤에 보여준다.
    /// </summary>
    [Column("is_public")]
    public bool IsPublic { get; set; }

    /// <summary>팝업으로 띄울지. 끄면 공지 목록에만 남는다.</summary>
    [Column("is_popup")]
    public bool IsPopup { get; set; } = true;

    /// <summary>게시 시작 일시. 비우면 제한 없음</summary>
    [Column("start_at")]
    public DateTime? StartAt { get; set; }

    /// <summary>게시 종료 일시. 비우면 제한 없음</summary>
    [Column("end_at")]
    public DateTime? EndAt { get; set; }

    /// <summary>사용 상태 (0: 비활성, 1: 활성)</summary>
    [Column("status")]
    public int Status { get; set; } = 1;

    /// <summary>노출 순서 (작을수록 먼저)</summary>
    [Column("order_no")]
    public int OrderNo { get; set; }

    /// <summary>첨부파일 목록</summary>
    public virtual ICollection<NoticeFile> Files { get; set; } = new List<NoticeFile>();
}

/// <summary>
/// 공지 첨부파일 엔티티
/// </summary>
/// <remarks>
/// 실제 파일은 FileServer 가 보관한다. 여기에는 그쪽이 발급한 아이디와
/// 목록에 보여줄 이름·크기만 둔다.
/// </remarks>
[Table("notice_files", Schema = "scom")]
public class NoticeFile : BaseEntity<string>
{
    public NoticeFile()
    {
        Id = Guid.NewGuid().ToString();
    }

    /// <summary>공지 아이디</summary>
    [Required]
    [Column("notice_id")]
    public string NoticeId { get; set; } = string.Empty;

    [ForeignKey(nameof(NoticeId))]
    public virtual Notice? Notice { get; set; }

    /// <summary>FileServer 가 발급한 파일 아이디</summary>
    [Required]
    [Column("file_id")]
    public string FileId { get; set; } = string.Empty;

    /// <summary>원본 파일명</summary>
    [Required]
    [Column("file_name")]
    public string FileName { get; set; } = string.Empty;

    /// <summary>바이트 크기</summary>
    [Column("file_size")]
    public long FileSize { get; set; }

    /// <summary>MIME 타입</summary>
    [Column("content_type")]
    public string? ContentType { get; set; }

    /// <summary>정렬 순서</summary>
    [Column("sort_no")]
    public int SortNo { get; set; }
}
