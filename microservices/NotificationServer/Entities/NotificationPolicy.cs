using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using JSini.Shared.Domain;

namespace NotificationServer.Entities;

/// <summary>
/// 「이 <b>역할</b>은 이 <b>이벤트</b>를 이 <b>길</b>로 받는다」 한 줄.
/// </summary>
/// <remarks>
/// <para>
/// [<see cref="NotificationPreference"/> 와 갈래가 다르다]
/// </para>
///
/// <para>
/// 그쪽은 <b>사람 하나의 뜻</b>이다 — 「나는 안 받겠다」. 이쪽은 <b>회사의
/// 규칙</b>이다 — 「배포 알림은 시스템관리자가 받는다」. 둘은 순서대로 걸리고
/// <b>사람의 뜻이 나중</b>이다: 정책이 보내라고 해도 본인이 껐으면 안 간다.
/// 반대는 성립하지 않는다 — 본인이 켜 두어도 정책에 없으면 안 간다.
/// </para>
///
/// <para>
/// [역할마다 길을 따로 켠다]
/// </para>
///
/// <para>
/// 푸시와 메일을 한 스위치로 묶지 않는다. 실제로 갈리는 자리가 있다 — 헬프데스크
/// 요청은 담당 역할에게 <b>푸시로</b> 두드리고 관리 역할에게는 <b>메일로</b>
/// 남기는 식이다. 묶어 두면 그 구분을 역할을 둘로 쪼개서 흉내 내야 한다.
/// </para>
///
/// <para>
/// [한 쌍에 한 줄이다]
/// </para>
///
/// <para>
/// (이벤트, 역할)이 유일하다. 두 줄이 생기면 어느 쪽이 참인지 알 수 없고,
/// 그 틀림은 <b>껐는데 간다</b> 쪽이라 특히 나쁘다.
/// </para>
/// </remarks>
[Table("notification_policies", Schema = "scom")]
public class NotificationPolicy : BaseEntity<string>
{
    public NotificationPolicy()
    {
        Id = Guid.NewGuid().ToString();
    }

    /// <summary>이벤트 코드. <see cref="NotificationEventRow"/> 의 식별자다.</summary>
    [Required]
    [MaxLength(64)]
    [Column("event_code")]
    public string EventCode { get; set; } = string.Empty;

    /// <summary>역할 식별자(<c>scom.roles.id</c>). 예: <c>SYSTEM_ADMINISTRATOR</c>.</summary>
    [Required]
    [MaxLength(64)]
    [Column("role_id")]
    public string RoleId { get; set; } = string.Empty;

    /// <summary>앱 푸시로 받나.</summary>
    [Column("push_enabled")]
    public bool PushEnabled { get; set; } = true;

    /// <summary>이메일로 받나.</summary>
    [Column("email_enabled")]
    public bool EmailEnabled { get; set; }

    /// <summary>
    /// 이 줄을 쓰나. <b>지우는 것과 뜻이 다르다</b> — 끄면 「이 역할은 당분간
    /// 안 받는다」이고, 지우면 「이 역할은 이 이벤트와 상관이 없다」다.
    /// 이벤트에 <b>켜진 줄이 하나도 없으면 제한이 없는 것</b>으로 보므로
    /// (그 판정은 <c>NotificationPolicyService</c> 에 있다) 마지막 한 줄을 끄는
    /// 것과 지우는 것의 결과는 같다.
    /// </summary>
    [Column("is_active")]
    public bool IsActive { get; set; } = true;
}
