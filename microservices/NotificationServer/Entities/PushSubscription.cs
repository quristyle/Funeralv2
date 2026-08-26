using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using JSini.Shared.Domain;

namespace NotificationServer.Entities;

/// <summary>
/// Web Push 구독 한 건 (브라우저 하나 = 행 하나)
/// </summary>
/// <remarks>
/// <b>주인을 문자열 한 쌍으로 잡은 것이 헬프데스크 것과 가장 다른 점이다.</b>
///
/// <para>
/// 헬프데스크의 구독 표는 <c>(int UserId, string UserType)</c> 였고 <c>Admin</c>·<c>Customer</c>
/// 테이블에 외래키로 묶여 있었다. 그 구조는 헬프데스크 밖에서 쓸 수 없다 — 포털 계정은
/// 아이디가 문자열이고, 장례식장은 또 다른 신원 체계를 쓴다.
/// </para>
///
/// <para>
/// 그래서 이 서비스는 <see cref="OwnerType"/> + <see cref="OwnerKey"/> 로만 주인을 안다.
/// 누구에게 보낼지는 <b>부르는 쪽이 정한다</b> — 헬프데스크는 자기 DB 에서 "이 팀의
/// 관리자들" 을 골라 그 주인 키 목록을 넘기면 된다. 이 서비스는 팀도 회사도 모른다.
/// </para>
///
/// <code>
///   ownerType = "jsini"            ownerKey = "quristyle"      (포털 로그인 아이디)
///   ownerType = "helpdesk-admin"   ownerKey = "5"              (헬프데스크 Admin.Id)
///   ownerType = "helpdesk-customer" ownerKey = "12"
/// </code>
/// </remarks>
[Table("push_subscriptions", Schema = "scom")]
public class PushSubscription : BaseEntity<string>
{
    public PushSubscription()
    {
        Id = Guid.NewGuid().ToString();
    }

    /// <summary>
    /// 푸시 서비스가 준 발송 주소. 브라우저·기기마다 다르고 이것이 실질적인 신원이다.
    /// </summary>
    /// <remarks>
    /// 유일해야 한다. 같은 브라우저가 다시 구독하면 같은 endpoint 가 오므로
    /// 새로 만들지 않고 갱신한다 (그러지 않으면 같은 기기에 여러 번 보낸다).
    /// </remarks>
    [Required]
    [Column("endpoint")]
    public string Endpoint { get; set; } = string.Empty;

    /// <summary>암호화 키 (브라우저가 준 값)</summary>
    [Required]
    [Column("p256dh")]
    public string P256dh { get; set; } = string.Empty;

    /// <summary>인증 비밀 (브라우저가 준 값)</summary>
    [Required]
    [Column("auth")]
    public string Auth { get; set; } = string.Empty;

    /// <summary>주인의 종류. 예: <c>jsini</c> · <c>helpdesk-admin</c> · <c>helpdesk-customer</c></summary>
    [Required]
    [Column("owner_type")]
    public string OwnerType { get; set; } = string.Empty;

    /// <summary>주인 식별자. 종류에 따라 로그인 아이디이거나 숫자 문자열이다.</summary>
    [Required]
    [Column("owner_key")]
    public string OwnerKey { get; set; } = string.Empty;

    /// <summary>어느 시스템에서 구독했나 (참고용). 예: <c>portal</c> · <c>helpdesk</c></summary>
    [Column("source")]
    public string? Source { get; set; }

    /// <summary>구독을 만든 브라우저 정보 (참고용). 문제를 쫓을 때 쓴다.</summary>
    [Column("user_agent")]
    public string? UserAgent { get; set; }

    /// <summary>마지막으로 발송에 성공한 시각.</summary>
    [Column("last_sent_at")]
    public DateTime? LastSentAt { get; set; }

    /// <summary>
    /// 연달아 실패한 횟수. 푸시 서비스가 404/410 을 주면 그 구독은 죽은 것이다.
    /// </summary>
    /// <remarks>
    /// 죽은 구독을 그냥 두면 발송마다 실패가 쌓이고 로그가 지저분해진다.
    /// 404/410 은 즉시 지우고, 그 밖의 실패는 세어 둔다.
    /// </remarks>
    [Column("failure_count")]
    public int FailureCount { get; set; }
}
