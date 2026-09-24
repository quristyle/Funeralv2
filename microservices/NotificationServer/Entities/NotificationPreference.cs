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
    /// <b>쪽지를 메일로도 받을지.</b> 기본은 꺼짐이다.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <see cref="EmailEnabled"/> 와 갈래가 다르다. 그쪽은 <b>업무 알림</b>(역할로
    /// 보내는 메일)이고, 이쪽은 <b>사람이 나에게 쓴 글</b>이다. 쪽지는 이미 쪽지함에
    /// 남고 앱 푸시로 두드리므로, 메일까지 받으면 받는 쪽에 <b>지워야 할 것이 하나
    /// 더 생긴다</b> — 그래서 원하는 사람만 켠다.
    /// </para>
    ///
    /// <para>
    /// <b>보내는 사람이 정하지 않는다.</b> 한동안 쪽지 쓰기 화면에 「메일」 체크가
    /// 있었는데, 메일을 하나 더 받을지는 <b>받는 사람의 사정</b>이지 보내는 사람이
    /// 고를 일이 아니다.
    /// </para>
    /// </remarks>
    [Column("note_email_enabled")]
    public bool NoteEmailEnabled { get; set; }

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

    /// <summary>
    /// <b>내 위치 날씨</b> 알림을 받을지. 기본은 꺼짐이다.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <see cref="WeatherEnabled"/> 와 갈래가 다르다. 그쪽은 <b>기상청이 발표한
    /// 특보</b>가 회사가 등록해 둔 지점에 걸렸을 때만 울리는 <b>사건</b> 알림이고,
    /// 이쪽은 <b>내가 서 있는 곳</b>의 지금 날씨와 예보를 <b>사건이 없어도 시각마다</b>
    /// 보내는 것이다. 한 스위치에 얹으면 특보만 받고 싶은 사람이 매일 아침 알림을
    /// 함께 받게 된다.
    /// </para>
    /// <para>
    /// <b>위치 없이는 켜도 소용없다.</b> 브라우저가 위경도를 한 번 줘야
    /// (<see cref="WeatherLat"/>) 보낼 곳이 생긴다 — 화면이 그 순서를 강제한다.
    /// </para>
    /// </remarks>
    [Column("weather_local_enabled")]
    public bool WeatherLocalEnabled { get; set; }

    /// <summary>브라우저 Geolocation 이 준 위도(10진 도).</summary>
    /// <remarks>
    /// <b>기기가 아니라 사람에 붙는다.</b> 구독표(<see cref="PushSubscription"/>)는
    /// 브라우저 하나가 한 줄이라 거기 두면 기기를 바꿀 때마다 위치를 다시 잡아야 한다.
    /// </remarks>
    [Column("weather_lat")]
    public double? WeatherLat { get; set; }

    /// <summary>브라우저 Geolocation 이 준 경도(10진 도).</summary>
    [Column("weather_lon")]
    public double? WeatherLon { get; set; }

    /// <summary>
    /// 보여 줄 지역 이름(예: <c>울산광역시 남구 삼산동</c>). <b>표시 전용이다</b> —
    /// 어느 격자로 물을지는 발송기가 위경도에서 다시 계산한다.
    /// </summary>
    [Column("weather_place")]
    public string? WeatherPlace { get; set; }

    /// <summary>받을 시각들(KST, 쉼표로 나눈 0~23). 비면 <c>7,18</c> 로 본다.</summary>
    [Column("weather_hours")]
    public string? WeatherHours { get; set; }

    /// <summary>위치를 마지막으로 잡은 때.</summary>
    [Column("weather_located_at")]
    public DateTime? WeatherLocatedAt { get; set; }

    /// <summary>
    /// 마지막으로 보낸 때. <b>같은 시각 칸에 두 번 보내지 않으려고 본다</b> —
    /// 발송기가 5분마다 도는데 이 값이 없으면 한 시간에 열두 번 간다.
    /// </summary>
    [Column("weather_local_sent_at")]
    public DateTime? WeatherLocalSentAt { get; set; }
}
