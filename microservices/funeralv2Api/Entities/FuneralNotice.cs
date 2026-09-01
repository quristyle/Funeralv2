using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace funeralv2Api.Entities;

/// <summary>
/// 장례식장 알림 정보 엔티티.
/// </summary>
/// <remarks>
/// 옛 시스템의 <c>smfr.t_notification</c> 에 해당한다. 옛 표는 수신자 한 명을
/// <c>n_nofi_user</c> 에 적고 읽음 여부를 그 행에 함께 두었는데, 그러면 같은 알림을
/// 여러 사람에게 보낼 때 본문이 복제된다. 여기서는 <c>TargetUserId</c> 가 비어 있으면
/// 전체 공지로 보고, 읽음 여부는 <see cref="FuneralNoticeRead"/> 로 따로 뺀다.
///
/// <see cref="JSini.Shared.Domain.BaseEntity{T}"/> 를 물려받지 않는다 —
/// 물려받으면 감사 칸이 <c>createdat</c> 처럼 붙어 나오는데, 이 스키마의 최근 표들은
/// <c>created_at</c> 이다(<see cref="DeceasedRoom"/> 참고). 칸 이름을 직접 적어 맞춘다.
/// </remarks>
[Table("funeral_notices")]
public class FuneralNotice
{
    [Key]
    [Column("id")]
    [MaxLength(50)]
    public string Id { get; set; } = Guid.NewGuid().ToString("N");

    /// <summary>알림 제목</summary>
    [Required]
    [Column("title")]
    public string Title { get; set; } = string.Empty;

    /// <summary>알림 본문</summary>
    [Column("content")]
    public string? Content { get; set; }

    /// <summary>알림 구분 (NOTICE 공지 · ALERT 경고 · SYSTEM 시스템)</summary>
    [Column("notice_type")]
    [MaxLength(30)]
    public string NoticeType { get; set; } = "NOTICE";

    /// <summary>중요 표시 여부. 목록 맨 위에 붙는다.</summary>
    [Column("is_important")]
    public bool IsImportant { get; set; }

    /// <summary>받는 사람. 비어 있으면 전체 공지다 (옛 <c>n_nofi_user</c>).</summary>
    [Column("target_user_id")]
    [MaxLength(100)]
    public string? TargetUserId { get; set; }

    /// <summary>대상 건물. 비어 있으면 건물을 가리지 않는다.</summary>
    [Column("building_id")]
    [MaxLength(50)]
    public string? BuildingId { get; set; }

    /// <summary>눌렀을 때 갈 화면 경로 (옛 <c>target_page</c>)</summary>
    [Column("target_page")]
    public string? TargetPage { get; set; }

    /// <summary>화면에 넘길 값 (옛 <c>target_param</c>)</summary>
    [Column("target_param")]
    public string? TargetParam { get; set; }

    /// <summary>게시 시작 일시. 비어 있으면 만들자마자 보인다.</summary>
    [Column("start_at")]
    public DateTime? StartAt { get; set; }

    /// <summary>게시 종료 일시. 비어 있으면 계속 보인다.</summary>
    [Column("end_at")]
    public DateTime? EndAt { get; set; }

    [Column("created_at")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [Column("created_by")]
    [MaxLength(100)]
    public string? CreatedBy { get; set; }

    [Column("updated_at")]
    public DateTime? UpdatedAt { get; set; }

    [Column("updated_by")]
    [MaxLength(100)]
    public string? UpdatedBy { get; set; }

    [Column("is_deleted")]
    public bool IsDeleted { get; set; }
}
