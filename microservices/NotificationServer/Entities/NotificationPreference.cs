using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using JSini.Shared.Domain;

namespace NotificationServer.Entities;

/// <summary>사용자 한 명의 알림 수신 설정(사람 하나 = 행 하나). 구독은 기기의 것이고 이 표는 사람의 뜻이라 기기를 지워도 남는다.
/// 행이 없으면 켜짐이다(날씨 알림만 예외로 꺼짐). 포털 계정 주인은 ("jsini", 로그인 아이디)다.</summary>
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

    /// <summary>브라우저 푸시를 받을지. 끄면 구독은 남기고 발송만 멈춘다(구독을 지우면 다시 켤 때 권한을 다시 받아야 한다).</summary>
    [Column("push_enabled")]
    public bool PushEnabled { get; set; } = true;

    /// <summary>이메일 알림을 받을지. 역할로 보내는 메일(toRole)에만 걸리고, 주소를 직접 적어 보내는 업무 메일은 끄지 않는다.</summary>
    [Column("email_enabled")]
    public bool EmailEnabled { get; set; } = true;

    /// <summary>쪽지를 메일로도 받을지(기본 꺼짐). 쪽지는 이미 쪽지함과 앱 푸시로 닿으므로 메일은 원하는 사람만 받는다. 보내는 사람이 정하지 않는다.</summary>
    [Column("note_email_enabled")]
    public bool NoteEmailEnabled { get; set; }

    /// <summary>내 글에 달린 댓글을 앱 푸시로 받을지(기본 켜짐). PushEnabled 아래의 갈래라 이것만 끄면 댓글 알림만 멎는다. 판정은 발송 직전 PushSender 가 한다.</summary>
    [Column("comment_push_enabled")]
    public bool CommentPushEnabled { get; set; } = true;

    /// <summary>내 글에 달린 댓글을 이메일로 받을지(기본 켜짐). 역할 메일 스위치로는 가려지지 않는다. 쪽지 메일과 기본값이 반대인 것은 댓글이 요청글을 열어야만 보여서다.</summary>
    [Column("comment_email_enabled")]
    public bool CommentEmailEnabled { get; set; } = true;

    /// <summary>날씨(기상 특보 · 임계치) 알림을 받을지(기본 꺼짐). 발송 경로는 아직 없고, 이 값은 그 발송이 붙을 때 답이 되도록 미리 받아 둔다.</summary>
    [Column("weather_enabled")]
    public bool WeatherEnabled { get; set; }

    /// <summary>내 위치 날씨 알림을 받을지(기본 꺼짐). 특보와 달리 사건이 없어도 시각마다 보내므로 스위치를 따로 둔다. 위치 없이는 켜도 소용없다.</summary>
    [Column("weather_local_enabled")]
    public bool WeatherLocalEnabled { get; set; }

    /// <summary>브라우저 Geolocation 이 준 위도(10진 도). 기기가 아니라 사람에 붙는다.</summary>
    [Column("weather_lat")]
    public double? WeatherLat { get; set; }

    /// <summary>브라우저 Geolocation 이 준 경도(10진 도).</summary>
    [Column("weather_lon")]
    public double? WeatherLon { get; set; }

    /// <summary>보여 줄 지역 이름. 표시 전용이고, 격자는 발송기가 위경도에서 다시 계산한다.</summary>
    [Column("weather_place")]
    public string? WeatherPlace { get; set; }

    /// <summary>받을 시각들(KST, 쉼표로 나눈 0~23). 비면 <c>7,18</c> 로 본다.</summary>
    [Column("weather_hours")]
    public string? WeatherHours { get; set; }

    /// <summary>위치를 마지막으로 <b>잡은</b> 때 — 좌표가 실제로 달라진 때다.</summary>
    [Column("weather_located_at")]
    public DateTime? WeatherLocatedAt { get; set; }

    /// <summary>위치를 마지막으로 확인한 때. 자리가 안 바뀌었으면 WeatherLocatedAt 은 그대로 두고 이 칸만 찍는다 — 옛 동네 날씨와 확인 시각을 갈라 말하려고.</summary>
    [Column("weather_synced_at")]
    public DateTime? WeatherSyncedAt { get; set; }

    /// <summary>마지막으로 보낸 때. 같은 시각 칸에 두 번 보내지 않으려고 본다 — 발송기가 5분마다 돌아 이 값이 없으면 한 시간에 열두 번 간다.</summary>
    [Column("weather_local_sent_at")]
    public DateTime? WeatherLocalSentAt { get; set; }
}
