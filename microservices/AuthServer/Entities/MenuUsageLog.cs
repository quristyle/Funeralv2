using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace AuthServer.Entities;

/// <summary>메뉴 열람 한 건 — 누가 어떤 화면을 언제 보았나.</summary>
/// 쌓기만 하고 고치지도 지우지도 않는 표라 BaseEntity 를 상속하지 않는다(updated_at · is_deleted 가 쓸모없이 붙는다).
/// 제목은 적을 때의 값을 베껴 둔다 — 메뉴가 지워져도 그때 본 글이 남아야 하고, 열쇠는 셋을 다 적어 한 칸이 빈 줄이 되지 않게 한다.
[Table("menu_usage_logs", Schema = "scom")]
public class MenuUsageLog
{
    /// <summary>줄 번호</summary>
    public long Id { get; set; }

    /// <summary>본 때 (UTC)</summary>
    [Column("occurred_at")]
    public DateTime OccurredAt { get; set; }

    /// <summary>본 사람의 로그인 아이디 (scom.accounts.user_id)</summary>
    [Required]
    [MaxLength(128)]
    [Column("user_id")]
    public string UserId { get; set; } = string.Empty;

    /// <summary>본 메뉴의 식별자 (scom.system_menus.id). 못 찾으면 비어 있다</summary>
    [MaxLength(64)]
    [Column("menu_id")]
    public string? MenuId { get; set; }

    /// <summary>화면이 선언한 열쇠 (예: admin.status.menu-usage)</summary>
    [MaxLength(128)]
    [Column("route_key")]
    public string? RouteKey { get; set; }

    /// <summary>메뉴 경로. 권한표와 같은 열쇠이고 조회는 이것으로 묶는다</summary>
    [Required]
    [MaxLength(512)]
    [Column("menu_path")]
    public string MenuPath { get; set; } = string.Empty;

    /// <summary>그때 화면에 적혀 있던 메뉴 제목</summary>
    [MaxLength(256)]
    [Column("menu_title")]
    public string? MenuTitle { get; set; }

    /// <summary>실제로 열린 주소. 메뉴 경로와 다를 수 있다</summary>
    [MaxLength(512)]
    [Column("href")]
    public string? Href { get; set; }

    /// <summary>직전에 보던 화면의 메뉴 경로. 첫 화면이면 비어 있다</summary>
    [MaxLength(512)]
    [Column("from_path")]
    public string? FromPath { get; set; }
}
