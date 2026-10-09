using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using JSini.Shared.Domain;

namespace NotificationServer.Entities;

/// <summary>
/// 사람 하나가 <b>지나온 자리</b> 한 점. 한 번 잴 때마다 한 행이다.
/// </summary>
/// <remarks>
/// <para>
/// <b><see cref="NotificationPreference"/> 의 좌표와 무엇이 다른가.</b> 저쪽은
/// 「<i>지금</i> 어디 있나」 한 줄이라 덮어쓴다 — 「내 위치 날씨」가 그 값
/// 하나만 있으면 되기 때문이다. 이쪽은 <b>덮지 않는다</b>. 그래서 「지금 어디」를
/// 묻는 자리는 여전히 저쪽이고(정본이 둘이 되면 안 된다), 이 표는 곁에 쌓인다.
/// </para>
///
/// <para>
/// [한 줄이 생기는 조건은 하나다]
/// </para>
///
/// <para>
/// 브라우저가 <b>방금 재어</b> 보낸 좌표일 때만 쌓는다
/// (<c>UpdateNotificationPreferenceDto.WeatherLocated</c> 가 참). 설정 화면은
/// 스위치 하나를 눌러도 설정 전체를 보내므로, 좌표가 실려 있다는 것만으로
/// 쌓으면 <b>가만히 앉아 스위치를 만지는 동안 기록이 는다</b>.
/// </para>
///
/// <para>
/// 그래서 <b>위치를 허용하지 않은 사람은 한 행도 생기지 않는다.</b> 재는 쪽이
/// (<c>GeoLocator</c>) 권한이 허용된 브라우저에서만 돌기 때문이고, 그것이 이
/// 기록의 동의 경계다 — 끄는 길은 브라우저의 위치 권한을 거두는 것이다.
/// </para>
/// </remarks>
[Table("location_tracks", Schema = "scom")]
public class LocationTrack : BaseEntity<string>
{
    public LocationTrack()
    {
        Id = Guid.NewGuid().ToString();
    }

    /// <summary>주인의 종류. 포털 계정은 <c>jsini</c> 다.</summary>
    [Required]
    [Column("owner_type")]
    public string OwnerType { get; set; } = string.Empty;

    /// <summary>주인 식별자. 포털이면 로그인 아이디다.</summary>
    [Required]
    [Column("owner_key")]
    public string OwnerKey { get; set; } = string.Empty;

    /// <summary>브라우저 Geolocation 이 준 위도(10진 도).</summary>
    [Column("lat")]
    public double Lat { get; set; }

    /// <summary>브라우저 Geolocation 이 준 경도(10진 도).</summary>
    [Column("lon")]
    public double Lon { get; set; }

    /// <summary>
    /// 브라우저가 말한 오차 반지름(m). <b>모르는 경우가 흔하다</b> — 좌표만
    /// 싣는 길(<c>LocationUpdateDto</c>)에는 이 칸이 없다.
    /// </summary>
    [Column("accuracy")]
    public double? Accuracy { get; set; }

    /// <summary>
    /// 그때 알아낸 지역 이름. <b>비는 줄이 흔하다</b> — 이름은 날씨를 받아 올
    /// 때 함께 오는데, 자리가 안 바뀌면 기상청을 부르지 않는다.
    /// </summary>
    [Column("place")]
    public string? Place { get; set; }

    /// <summary>
    /// 직전 좌표에서 <b>자리가 바뀐</b> 점인가(300m —
    /// <c>GeoLocator.MoveThresholdMeters</c>).
    /// </summary>
    /// <remarks>
    /// 화면은 머문 자리를 제 자로 다시 묶지만(<c>LocationTrackService</c> 의
    /// 200m), 그 묶음이 맞는지 견줄 단서가 하나는 있어야 한다. 셈해서 버리면
    /// 나중에 「왜 여기서 갈라졌나」를 물을 곳이 없다.
    /// </remarks>
    [Column("moved")]
    public bool Moved { get; set; }

    /// <summary>잰 때. <b>UTC</b> 다.</summary>
    [Column("recorded_at")]
    public DateTime RecordedAt { get; set; } = DateTime.UtcNow;
}
