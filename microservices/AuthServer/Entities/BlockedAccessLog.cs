using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace AuthServer.Entities;

/// <summary>차단 한 건 — 권한 밖의 화면을 누가 어떤 주소로 열려 했나. 열린 화면을 적는 menu_usage_logs 와 한 표에 섞지 않는다 — 섞으면 「많이 쓰는 화면」과 「막힌 시도」가 한 숫자로 뭉친다.</summary>
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

    /// <summary>막힌 주소. 사람이 실제로 연 값이라 매개변수가 들어 있다 — 「누구의 것을 보려 했나」가 이 칸에만 남는다.</summary>
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

    /// <summary>왜 막혔나 — NO_PERMISSION(권한 없음) · UNMANAGED(관리 밖). 권한 화면에서 체크 하나면 끝나는 쪽과 메뉴를 만들거나 코드를 고쳐야 하는 쪽을 가르는 것이 이 표의 요점이다.</summary>
    [Required]
    [MaxLength(32)]
    [Column("reason")]
    public string Reason { get; set; } = string.Empty;

    /// <summary>직전에 보던 화면. 「어디서 눌러 들어왔나」다</summary>
    [MaxLength(512)]
    [Column("from_path")]
    public string? FromPath { get; set; }

    /// <summary>접속 주소. 위조할 수 있는 값이라 보여 주기 전용이다 — 이것으로 무엇을 판단하지 않는다.</summary>
    [MaxLength(64)]
    [Column("client_ip")]
    public string? ClientIp { get; set; }
}
