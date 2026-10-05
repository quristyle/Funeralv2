using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using JSini.Shared.Domain;

namespace AuthServer.Entities;

/// <summary>
/// 포털 프론트가 잡은 미처리 예외 한 건. 오류 화면이 보여 준 <b>추적 번호로 찾는다</b>.
/// </summary>
/// <remarks>
/// <para>
/// [왜 표로 남기나 — 컨테이너 로그로는 못 찾는다]
/// </para>
///
/// <para>
/// 사용자가 보는 오류 화면(<c>Error.razor</c>)은 추적 번호를 적어 준다
/// (<c>00-{32자리}-{16자리}-00</c>, W3C traceparent). 그런데 그 번호로
/// <c>docker logs</c> 를 뒤져도 <b>한 줄도 안 나온다</b> — .NET 콘솔 로거는
/// 기본값이 <c>IncludeScopes=false</c> 라 추적 번호를 찍지 않는다. 그래서
/// 신고를 받아도 「몇 시쯤 났다」로 시각을 좇는 수밖에 없었고, 운영 로그는
/// 10MB 3개로 돌려 쓰므로 바쁜 날에는 그 전에 밀려 나간다.
/// </para>
///
/// <para>
/// 번호를 열쇠로 들고 있는 표가 있으면 그 두 문제가 함께 없어진다.
/// </para>
///
/// <para>
/// [중복이 생길 수 있다]
/// </para>
///
/// <para>
/// 한 요청에서 예외가 두 번 날 수 있고(본 요청과 오류 화면 재실행), 그때
/// <see cref="TraceId"/> 가 같은 줄이 둘 생긴다. 열쇠로 쓰되 <b>고유 제약을
/// 걸지 않는 이유</b>가 그것이다 — 둘째 줄을 버리면 진짜 원인이 버려질 수 있다.
/// 조회 화면은 같은 번호의 줄을 모두 보여 준다.
/// </para>
/// </remarks>
[Table("portal_error_logs", Schema = "scom")]
public class PortalErrorLog : BaseEntity<string>
{
    public PortalErrorLog()
    {
        Id = Guid.NewGuid().ToString();
    }

    /// <summary>
    /// 추적 번호의 가운데 32자리(W3C trace-id). <b>조회 열쇠다.</b>
    ///
    /// <para>
    /// 사용자는 번호를 통째로 읽어 주고, 받아 적는 사람은 중간만 옮겨 적기도 한다.
    /// 그래서 전체(<see cref="Traceparent"/>)와 가운데를 따로 들고, 조회는
    /// 가운데로 한다 — 어느 쪽을 쳐도 찾히게 하려면 기준이 하나여야 한다.
    /// </para>
    /// </summary>
    [Required]
    [MaxLength(64)]
    [Column("trace_id")]
    public string TraceId { get; set; } = string.Empty;

    /// <summary>화면에 적힌 번호 그대로. 사용자가 불러 준 값과 눈으로 맞춰 보는 자리다.</summary>
    [MaxLength(128)]
    [Column("traceparent")]
    public string? Traceparent { get; set; }

    /// <summary>
    /// 오류가 난 때(UTC). <see cref="BaseEntity{TKey}.CreatedAt"/> 와 따로 두는 이유는
    /// <b>보고가 늦을 수 있기</b> 때문이다 — 포털이 큐에 쌓아 두었다가 보내고,
    /// 게이트웨이가 죽어 있으면 되살아난 뒤에 보낸다.
    /// </summary>
    [Column("occurred_at")]
    public DateTime OccurredAt { get; set; } = DateTime.UtcNow;

    /// <summary>어느 프론트인가 (<c>portal</c> · <c>site</c>). 지금은 포털뿐이다.</summary>
    [MaxLength(32)]
    [Column("source")]
    public string Source { get; set; } = "portal";

    /// <summary>터진 화면 경로 (<c>/funeral/room-status</c>).</summary>
    [MaxLength(512)]
    [Column("path")]
    public string? Path { get; set; }

    /// <summary>질의 문자열. 경로만으로는 재현이 안 되는 화면이 많다.</summary>
    [MaxLength(1024)]
    [Column("query_string")]
    public string? QueryString { get; set; }

    [MaxLength(16)]
    [Column("method")]
    public string? Method { get; set; }

    /// <summary>로그인한 사람의 아이디. 로그인 전이면 비어 있다.</summary>
    [MaxLength(128)]
    [Column("user_id")]
    public string? UserId { get; set; }

    /// <summary>접속 IP. 포털이 nginx·게이트웨이 뒤라 X-Forwarded-For 의 첫 값이다.</summary>
    [MaxLength(64)]
    [Column("ip")]
    public string? Ip { get; set; }

    /// <summary>
    /// 브라우저·기기. <b>이 기능을 만든 까닭이 여기 있다</b> — 모바일에서만 나는
    /// 오류를 PC 로 재현하려다 놓치는 일이 있다.
    /// </summary>
    [MaxLength(512)]
    [Column("user_agent")]
    public string? UserAgent { get; set; }

    /// <summary>예외 타입 이름 (<c>System.NullReferenceException</c>). 거르기와 묶어 세기에 쓴다.</summary>
    [MaxLength(256)]
    [Column("exception_type")]
    public string? ExceptionType { get; set; }

    /// <summary>예외 메시지 한 줄.</summary>
    [MaxLength(2048)]
    [Column("message")]
    public string? Message { get; set; }

    /// <summary>
    /// 스택 추적. <b>안쪽 예외(InnerException)까지 펼쳐서 담는다</b> —
    /// 바깥 메시지가 "An error occurred while…" 뿐이고 진짜 까닭은 안쪽에만
    /// 있는 경우가 흔하다.
    /// </summary>
    [Column("detail")]
    public string? Detail { get; set; }
}
