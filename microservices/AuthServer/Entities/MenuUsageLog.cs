using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace AuthServer.Entities;

/// <summary>
/// 메뉴 열람 한 건 — 누가 어떤 화면을 언제 보았나.
/// </summary>
/// <remarks>
/// <para>
/// [왜 AuthServer 인가]
/// </para>
///
/// <para>
/// 메뉴(<c>scom.system_menus</c>)와 계정(<c>scom.accounts</c>)의 정본이 여기다.
/// 열람 기록은 그 둘을 가리키기만 하는 표라, 다른 서비스에 두면 같은 메뉴를
/// 둘이 알게 되고 메뉴 칸을 늘릴 때 한쪽만 고쳐진다
/// (<c>AiUsageDbContext</c> 머리말과 같은 판단이다).
/// </para>
///
/// <para>
/// [<c>BaseEntity</c> 를 상속하지 않는다]
/// </para>
///
/// <para>
/// 쌓기만 하고 <b>고치지도 지우지도 않는</b> 표다. 그러면
/// <c>updated_at</c> · <c>updated_by</c> · <c>is_deleted</c> 셋은 영원히 같은
/// 값인 칸이 되고, <c>created_at</c> 은 <see cref="OccurredAt"/> 과 같은 값을
/// 두 번 적는 칸이 된다. 쌓는 쪽이 초에 여러 줄을 넣을 수 있는 표라서 그
/// 네 칸이 그냥 자리만 먹는다 — <c>scom.ai_usage_logs</c> 가 같은 까닭으로
/// 맨 POCO 다.
/// </para>
///
/// <para>
/// [메뉴 제목을 베껴 둔다 — <c>menu_favorites</c> 와 반대다]
/// </para>
///
/// <para>
/// 즐겨찾기는 제목을 담지 않는다(그 엔티티 머리말) — 메뉴 쪽 값이 정본이고
/// 제목을 고치면 즐겨찾기도 따라가야 하기 때문이다. <b>기록은 반대다.</b>
/// 「2026년 3월에 무엇을 보았나」의 답은 <b>그때 그 사람이 본 글자</b>여야
/// 하고, 메뉴가 지워지면 조인으로는 그 줄이 통째로 사라진다. 그래서 그때의
/// 제목을 그대로 적어 둔다.
/// </para>
///
/// <para>
/// [열쇠를 셋이나 적는 까닭]
/// </para>
///
/// <para>
/// <see cref="MenuId"/> 는 메뉴가 지워지면 가리킬 곳이 없어지고,
/// <see cref="RouteKey"/> 는 아직 안 채운 메뉴가 있고(이행 중),
/// <see cref="MenuPath"/> 는 권한표의 열쇠라 바뀌지 않지만 Vue 시절 값이
/// 섞여 있다. 셋 중 하나만 들고 있으면 <b>그 하나가 빈 줄</b>이 조회에서
/// 사라지므로 받은 것을 다 적어 둔다 — 조회는 경로로 묶고, 메뉴를 다시
/// 찾아갈 때는 열쇠를 쓴다.
/// </para>
/// </remarks>
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
