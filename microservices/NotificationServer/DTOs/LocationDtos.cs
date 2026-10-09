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

// ============================================================
// 이동 경로 — **지나온 자리**.
//
// 위의 `AccountLocationDto` 가 「지금 어디 있나」라면 아래는 「어디를
// 지나왔나」다. 자료가 사는 표도 다르다(`scom.location_tracks`).
//
// ── 「머문 자리」는 저장된 것이 아니라 셈한 것이다 ───────────
//
// 표에 쌓이는 것은 **점**뿐이다(한 시간에 하나쯤). 사람이 보고 싶은 것은
// 점이 아니라 「어디에 얼마나 있었나」라, 가까이 붙은 점들을 하나로 묶어
// `LocationStayDto` 를 만든다. 묶는 자는 서버에 하나 있다
// (`LocationTrackService.StayRadiusMeters`) — 화면이 묶으면 목록과 지도가
// 서로 다른 셈을 하게 된다.
// ============================================================

/// <summary>
/// 기록된 점 하나 — <b>잰 그대로</b>.
/// </summary>
/// <remarks>
/// 묶지 않은 날것이라 GPS 가 흔든 몇십 미터도 그대로 들어 있다. 화면이 이것을
/// 따로 보여 주는 까닭은 <b>머문 자리의 묶음을 의심할 수 있어야</b> 하기
/// 때문이다 — 「여기 있었다」가 틀려 보일 때 볼 곳이 이것뿐이다.
/// </remarks>
public class LocationTrackPointDto
{
    /// <summary>잰 때. <b>UTC</b> 다 — 한국 시각은 화면이 만든다.</summary>
    public DateTime RecordedAt { get; set; }

    public double Lat { get; set; }
    public double Lon { get; set; }

    /// <summary>브라우저가 말한 오차 반지름(m). 모르면 <c>null</c>.</summary>
    public double? Accuracy { get; set; }

    /// <summary>그때 알아낸 지역 이름. 비는 줄이 흔하다.</summary>
    public string? Place { get; set; }

    /// <summary>직전 좌표에서 300m 넘게 옮긴 점인가.</summary>
    public bool Moved { get; set; }
}

/// <summary>
/// 머문 자리 하나 — <b>가까이 붙은 점들을 묶은 것</b>.
/// </summary>
/// <remarks>
/// <para>
/// <b><see cref="Minutes"/> 는 「관측된 처음과 마지막 사이」다.</b> 실제로
/// 머문 시간보다 <b>짧게</b> 나온다 — 그 자리에 닿은 순간과 떠난 순간은
/// 아무도 재지 않았고, 잰 것은 한 시간 간격의 표본뿐이기 때문이다.
/// </para>
/// <para>
/// 모자라는 쪽으로 틀리게 두는 것이 뜻이 있다. 다음 관측까지를 머문 것으로
/// 치면 <b>노트북을 덮고 잔 열 시간</b>이 그대로 사무실 체류 시간이 된다 —
/// 그 값은 틀린 것을 넘어 사람을 오해하게 만든다. <see cref="Samples"/> 를
/// 함께 주는 것은 그 때문이다. 관측이 한 번뿐인 자리는 0분으로 나오고,
/// 그때 사람이 알아야 하는 것은 「머물지 않았다」가 아니라 「한 번 봤다」다.
/// </para>
/// </remarks>
public class LocationStayDto
{
    /// <summary>그날의 몇 번째 자리인가. 1 부터다 — 지도의 점에 적힌다.</summary>
    public int Seq { get; set; }

    /// <summary>묶인 점들의 가운데. 지도가 이 자리에 점을 찍는다.</summary>
    public double Lat { get; set; }
    public double Lon { get; set; }

    /// <summary>묶음 안에서 처음 찾은 지역 이름. 끝내 못 찾으면 <c>null</c>.</summary>
    public string? Place { get; set; }

    /// <summary>그 자리에서 <b>처음</b> 관측된 때(UTC).</summary>
    public DateTime ArrivedAt { get; set; }

    /// <summary>그 자리에서 <b>마지막으로</b> 관측된 때(UTC).</summary>
    public DateTime LeftAt { get; set; }

    /// <summary>머문 시간(분). 머리말의 까닭으로 <b>실제보다 짧다</b>.</summary>
    public int Minutes { get; set; }

    /// <summary>묶인 점의 수. 0분으로 나온 자리를 읽는 단서다.</summary>
    public int Samples { get; set; }

    /// <summary>
    /// 앞 자리에서 여기까지의 직선 거리(m). 첫 자리는 0 이다.
    /// </summary>
    /// <remarks>
    /// <b>실제로 걸은 거리가 아니다.</b> 두 점 사이를 어떻게 갔는지는 기록에
    /// 없다 — 한 시간에 한 번 보는 눈으로는 알 수 없다.
    /// </remarks>
    public double MovedMeters { get; set; }
}

/// <summary>
/// 하루치 이동 경로 — <b>점 · 머문 자리 · 요약</b> 한 벌.
/// </summary>
/// <remarks>
/// 셋을 한 번에 내려 주는 까닭은 화면이 셋을 <b>동시에</b> 그리기 때문이다
/// (지도 · 목록 · 머리의 요약). 나눠 주면 날짜를 바꿀 때마다 왕복이 셋이 되고,
/// 그 셋이 서로 다른 순간의 자료를 그릴 수 있다.
/// </remarks>
public class MyLocationTrackDto
{
    /// <summary>
    /// 어느 날인가. <b>한국 달력 날짜</b>다(<c>yyyy-MM-dd</c>) — 하루의 경계를
    /// 한국 자정으로 긋는다. UTC 자정으로 끊으면 한국의 하루가 두 쪽으로 갈린다.
    /// </summary>
    public string Date { get; set; } = string.Empty;

    /// <summary>그날 기록된 점 전부. 시간순이다.</summary>
    public List<LocationTrackPointDto> Points { get; set; } = [];

    /// <summary>묶어 낸 머문 자리들. 시간순이고 지도의 선이 이 차례를 잇는다.</summary>
    public List<LocationStayDto> Stays { get; set; } = [];

    /// <summary>
    /// 머문 자리들을 이은 직선 거리의 합(m). <b>실제 주행 거리가 아니다.</b>
    /// </summary>
    public double TotalMeters { get; set; }

    /// <summary>그날 가장 오래 머문 자리(분). 머문 자리가 없으면 0 이다.</summary>
    public int LongestMinutes { get; set; }

    /// <summary>
    /// 묶는 데 쓴 반지름(m). <b>화면이 적어 보여 준다</b> — 「왜 여기와 저기가
    /// 한 자리인가」의 답이 이 숫자 하나다.
    /// </summary>
    public double StayRadiusMeters { get; set; }
}

/// <summary>
/// 기록이 있는 날 하나. 날짜 고르개가 <b>빈 날을 피하게</b> 해 준다.
/// </summary>
/// <remarks>
/// 이것이 없으면 사람은 기록이 있는 날을 <b>하루씩 눌러 가며</b> 찾아야 한다.
/// 포털을 안 연 날에는 한 줄도 안 쌓이므로 빈 날이 드물지 않다.
/// </remarks>
public class LocationTrackDayDto
{
    /// <summary>한국 달력 날짜(<c>yyyy-MM-dd</c>).</summary>
    public string Date { get; set; } = string.Empty;

    /// <summary>그날 쌓인 점의 수.</summary>
    public int Count { get; set; }
}
