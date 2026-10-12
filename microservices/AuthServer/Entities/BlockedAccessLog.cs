using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace AuthServer.Entities;

/// <summary>
/// 차단 한 건 — <b>권한 밖의 화면을 누가 어떤 주소로 열려 했나.</b>
/// </summary>
/// <remarks>
/// <para>
/// [왜 <c>menu_usage_logs</c> 와 한 표가 아닌가]
/// </para>
///
/// <para>
/// 저쪽은 <b>열린 화면</b>을 적는다. 둘을 한 표에 섞으면 「누가 포털을 많이
/// 쓰나」를 묻는 모든 조회가 <b>막힌 줄까지 함께 세게</b> 되고, 그러면
/// 열람 수가 실제보다 많아진다. 칸 하나로 갈라 두면 조회마다 그 조건을
/// 빠뜨리지 않아야 하는데, 열람 쪽 조회가 넷이다(기록·사람별·화면별·타임라인).
/// </para>
///
/// <para>
/// 보는 사람도 다르다. 열람 기록은 「무엇이 쓰이나」를 보는 운영 자료고
/// 이쪽은 <b>「권한이 어디가 어긋나 있나」</b>를 보는 점검 자료다 — 묻는
/// 물음이 달라 같은 화면에 두어도 서로를 가린다.
/// </para>
///
/// <para>
/// [<c>BaseEntity</c> 를 상속하지 않는다]
/// </para>
///
/// <para>
/// 쌓기만 하고 고치지도 지우지도 않는 표다(<see cref="MenuUsageLog"/> 와 같은
/// 까닭). <c>updated_at</c>·<c>is_deleted</c> 가 영원히 같은 값인 칸이 된다.
/// </para>
///
/// <para>
/// [메뉴 식별자를 적지 않는다]
/// </para>
///
/// <para>
/// 이 표의 줄 절반은 <b>메뉴가 아예 없어서</b> 막힌 것이다
/// (<c>UNMANAGED</c>). 식별자 칸을 두면 그 줄에서 늘 비어 있고, 비어 있는
/// 까닭이 「메뉴가 지워졌다」인지 「처음부터 없었다」인지 구분되지 않는다.
/// 대신 <see cref="Reason"/> 이 그것을 말한다.
/// </para>
/// </remarks>
[Table("blocked_access_logs", Schema = "scom")]
public class BlockedAccessLog
{
    /// <summary>줄 번호</summary>
    public long Id { get; set; }

    /// <summary>막힌 때 (UTC)</summary>
    [Column("occurred_at")]
    public DateTime OccurredAt { get; set; }

    /// <summary>막힌 사람의 로그인 아이디 (scom.accounts.user_id)</summary>
    [Required]
    [MaxLength(128)]
    [Column("user_id")]
    public string UserId { get; set; } = string.Empty;

    /// <summary>
    /// 막힌 주소. <b>사람이 실제로 연 그 값</b>이라 매개변수가 들어 있다
    /// (<c>/admin/system/account/jupark/app</c>) — 「누구의 것을 보려 했나」가
    /// 이 칸에만 남는다.
    /// </summary>
    [Required]
    [MaxLength(512)]
    [Column("href")]
    public string Href { get; set; } = string.Empty;

    /// <summary>화면이 선언한 열쇠. 신고가 없는 화면은 비어 있다</summary>
    [MaxLength(128)]
    [Column("route_key")]
    public string? RouteKey { get; set; }

    /// <summary>라우터가 끼우려던 화면 클래스. 「어느 파일이냐」의 답이다</summary>
    [MaxLength(256)]
    [Column("screen_type")]
    public string? ScreenType { get; set; }

    /// <summary>권한을 따진 메뉴 경로. 메뉴를 못 찾았으면 비어 있다</summary>
    [MaxLength(512)]
    [Column("menu_path")]
    public string? MenuPath { get; set; }

    /// <summary>
    /// 왜 막혔나 — <c>NO_PERMISSION</c>(권한 없음) · <c>UNMANAGED</c>(관리 밖).
    /// <b>둘을 가르는 것이 이 표의 요점이다</b>: 앞엣것은 권한 화면에서 체크
    /// 하나면 끝나고, 뒤엣것은 메뉴를 만들거나 코드를 고치기 전에는 안 열린다.
    /// </summary>
    [Required]
    [MaxLength(32)]
    [Column("reason")]
    public string Reason { get; set; } = string.Empty;

    /// <summary>직전에 보던 화면. 「어디서 눌러 들어왔나」다</summary>
    [MaxLength(512)]
    [Column("from_path")]
    public string? FromPath { get; set; }

    /// <summary>
    /// 접속 주소. <b>위조할 수 있는 값이라 보여 주기 전용이다</b> —
    /// 이것으로 무엇을 판단하지 않는다(<c>ClientAddress</c> 머리말).
    /// </summary>
    [MaxLength(64)]
    [Column("client_ip")]
    public string? ClientIp { get; set; }
}
