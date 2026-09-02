using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using JSini.Shared.Domain;

namespace NotificationServer.Entities;

/// <summary>
/// 사용자 한 명의 알림 수신 설정 (사람 하나 = 행 하나)
/// </summary>
/// <remarks>
/// <b><see cref="PushSubscription"/> 과 무엇이 다른가.</b> 구독은 <i>기기</i> 다 —
/// 브라우저마다 한 행이고, 브라우저를 지우면 사라진다. 이 표는 <i>사람의 뜻</i> 이다 —
/// 기기를 다 지워도 남고, 새 기기로 구독하면 그 뜻이 그대로 적용된다.
///
/// <para>
/// 주인은 구독 표와 같은 문자열 한 쌍이다(<c>ownerType</c> + <c>ownerKey</c>).
/// 포털 계정은 <c>("jsini", 로그인 아이디)</c> 다 — 게이트웨이가 주는
/// <c>X-User-Id</c> 가 <c>scom.accounts.user_id</c> 이기 때문이다.
/// </para>
///
/// <para>
/// <b>행이 없으면 "켜짐" 이다.</b> 기본값을 꺼짐으로 두면 설정 화면을 한 번도 열지
/// 않은 사람이 알림을 못 받게 되고, 이 표가 생기기 전과 동작이 달라진다.
/// 날씨만 예외로 꺼짐이 기본이다 — 업무 알림이 아니라 곁들이는 알림이라
/// 원하는 사람만 받는 편이 맞다.
/// </para>
/// </remarks>
[Table("notification_preferences", Schema = "scom")]
public class NotificationPreference : BaseEntity<string>
{
    public NotificationPreference()
    {
        Id = Guid.NewGuid().ToString();
    }

    /// <summary>주인의 종류. 예: <c>jsini</c> · <c>helpdesk-admin</c></summary>
    [Required]
    [Column("owner_type")]
    public string OwnerType { get; set; } = string.Empty;

    /// <summary>주인 식별자. 포털이면 로그인 아이디다.</summary>
    [Required]
    [Column("owner_key")]
    public string OwnerKey { get; set; } = string.Empty;

    /// <summary>
    /// 브라우저 푸시를 받을지. 끄면 구독이 남아 있어도 보내지 않는다
    /// (<c>PushSender</c> 가 발송 직전에 본다).
    /// </summary>
    /// <remarks>
    /// 구독을 지우는 것과 다르다. 구독을 지우면 다시 켤 때 브라우저 권한부터
    /// 다시 받아야 한다. 이 스위치는 그대로 두고 발송만 멈춘다.
    /// </remarks>
    [Column("push_enabled")]
    public bool PushEnabled { get; set; } = true;

    /// <summary>
    /// 이메일 알림을 받을지. <b>역할로 보내는 메일</b>(<c>toRole</c>)에만 걸린다 —
    /// 주소를 직접 적어 보내는 메일(문의 접수 회신 등)은 업무 메일이라 끄지 않는다.
    /// </summary>
    [Column("email_enabled")]
    public bool EmailEnabled { get; set; } = true;

    /// <summary>
    /// 날씨(기상 특보 · 임계치) 알림을 받을지. <b>기본은 꺼짐이다.</b>
    /// </summary>
    /// <remarks>
    /// 판정은 LifeEnvServer 가 이미 돌리고 있지만 <b>발송 경로는 아직 없다</b>
    /// (결정 D-G1, docs/analysis/38-ghub-migration.md). 이 값은 그 결정이 붙을 때
    /// "누구에게" 의 답이 되도록 지금 받아 둔다.
    /// </remarks>
    [Column("weather_enabled")]
    public bool WeatherEnabled { get; set; }
}
