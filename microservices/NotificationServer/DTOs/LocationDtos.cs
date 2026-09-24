namespace NotificationServer.DTOs;

// ============================================================
// 위치 — **사람이 허락해서** 서버에 남은 좌표.
//
// 이 좌표는 원래 「내 위치 날씨」를 보내려고 받아 둔 것이다
// (`NotificationPreference.WeatherLat`). 여기서는 같은 값을 **지도에 찍어
// 보여 주는** 모양으로 옮긴다 — 표를 새로 만들지 않는다. 사람의 자리를
// 적어 두는 칸이 둘이 되면 어느 쪽이 정본인지 아무도 모르게 된다.
// ============================================================

/// <summary>
/// 위치를 허용한 계정 하나 — <b>어디에 있고, 쪽지가 닿는가</b>.
/// </summary>
/// <remarks>
/// <para>
/// 두 가지를 한 줄에 담는다. 지도가 점을 찍는 데 필요한 것(좌표 · 이름)과,
/// 그 점을 눌렀을 때 <b>곧바로 쪽지를 보낼 수 있는지</b>다. 나눠 내려 주면
/// 화면이 점을 다 찍어 놓고 사람마다 「이 사람에게 보낼 수 있나」를 다시
/// 물어야 하고, 그 왕복이 점의 수만큼 생긴다.
/// </para>
/// <para>
/// <b>좌표가 없는 사람은 이 목록에 없다.</b> 「위치를 허용한 계정」이 이
/// 목록의 뜻이고, 허용하지 않은 사람을 좌표 없이 실어 보내면 지도는 못
/// 그리면서 목록만 길어진다.
/// </para>
/// </remarks>
public class AccountLocationDto
{
    /// <summary>포털 로그인 아이디. <b>쪽지도 이 값 하나로 간다.</b></summary>
    public string LoginId { get; set; } = string.Empty;

    /// <summary>사람이 읽는 이름. 비면 화면이 아이디를 대신 적는다.</summary>
    public string? Name { get; set; }

    /// <summary>회사 · 부서. 같은 이름이 둘일 때 가르는 값이다.</summary>
    public string? Affiliation { get; set; }

    /// <summary>대표 이메일. 없으면 메일로는 두드릴 수 없다.</summary>
    public string? Email { get; set; }

    /// <summary>브라우저 Geolocation 이 준 위도(10진 도).</summary>
    public double Lat { get; set; }

    /// <summary>브라우저 Geolocation 이 준 경도(10진 도).</summary>
    public double Lon { get; set; }

    /// <summary>
    /// 보여 줄 지역 이름(<c>울산광역시 남구 삼산동</c>). <b>표시 전용이다</b> —
    /// 비어 있는 경우가 흔하다(날씨를 한 번도 안 받은 사람).
    /// </summary>
    public string? Place { get; set; }

    /// <summary>
    /// 자리를 마지막으로 <b>잡은</b> 때 — 좌표가 실제로 달라진 때다.
    /// </summary>
    public DateTime? LocatedAt { get; set; }

    /// <summary>
    /// 자리를 마지막으로 <b>확인한</b> 때. 위의 것과 갈래가 다르다 —
    /// 안 움직였어도 포털을 열면 찍힌다(<c>GeoLocator</c> 의 조용한 확인).
    /// </summary>
    /// <remarks>
    /// 지도에서 이 값이 중요한 까닭은 <b>점이 얼마나 묵었는지</b>가 그것으로만
    /// 갈리기 때문이다. 「잡은 때」가 반 년 전이어도 어제 확인했다면 그 사람은
    /// 줄곧 거기 있는 것이고, 둘 다 반 년 전이면 그 점은 믿을 것이 못 된다.
    /// </remarks>
    public DateTime? SyncedAt { get; set; }

    /// <summary>
    /// 「내 위치 날씨」를 켜 두었나. <b>위치 허용 여부와 다르다</b> —
    /// 스위치를 껐어도 좌표는 남아 있다.
    /// </summary>
    public bool WeatherLocalEnabled { get; set; }

    /// <summary>앱 푸시가 실제로 닿는가 — 끄지 않았고 등록한 기기가 있다.</summary>
    public bool PushReachable { get; set; }

    /// <summary>쪽지 메일이 닿는가 — 「쪽지 메일받기」를 켰고 주소가 있다.</summary>
    public bool EmailReachable { get; set; }

    /// <summary>
    /// 쪽지를 받을 길이 있는가. <b>둘 중 하나면 된다</b> — 쪽지 보내기·찾기와
    /// 같은 값으로 가른다(<c>NoteRecipientDto.CanReceive</c>).
    /// </summary>
    public bool CanReceive => PushReachable || EmailReachable;
}
