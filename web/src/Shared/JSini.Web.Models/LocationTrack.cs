namespace JSini.Web.Models;

// 이동 경로. AccountLocation 이 「지금 어디 있나」라면 이쪽은 「어디를 지나왔나」다. 표(scom.location_tracks)는 잴 때마다 쌓인다.
// 머문 자리는 저장하지 않고 서버가 점을 묶어 셈한다. 화면이 다시 묶으면 지도·목록·요약이 같은 셈을 하리라는 보장이 없다. 칸 이름은 NotificationServer DTO 와 맞춘다.

/// <summary>기록된 점 하나(잰 그대로, 묶지 않은 날것). 머문 자리가 틀려 보일 때 의심할 수 있는 곳이 이것이다.</summary>
public sealed class LocationTrackPointDto
{
    /// <summary>잰 때. <b>UTC 다</b> — 한국 시각은 보여 주기 직전에 만든다.</summary>
    public DateTime RecordedAt { get; set; }

    /// <summary>위도(10진 도).</summary>
    public double Lat { get; set; }

    /// <summary>경도(10진 도).</summary>
    public double Lon { get; set; }

    /// <summary>브라우저가 말한 오차 반지름(m). 모르면 <c>null</c>.</summary>
    public double? Accuracy { get; set; }

    /// <summary>그때 알아낸 지역 이름. 비는 줄이 흔하다.</summary>
    public string? Place { get; set; }

    /// <summary>직전 좌표에서 300m 넘게 옮긴 점인가.</summary>
    public bool Moved { get; set; }
}

/// <summary>머문 자리 하나(가까이 붙은 점들을 묶은 것). Minutes 는 관측된 처음과 마지막 사이라 실제보다 짧게 나온다. 모자라는 쪽으로 틀리는 것이 뜻이 있다. 다음 관측까지로 치면 노트북을 덮고 잔 시간이 체류가 된다.</summary>
public sealed class LocationStayDto
{
    /// <summary>그날의 몇 번째 자리인가. 1 부터고 지도의 점에 적힌다.</summary>
    public int Seq { get; set; }

    /// <summary>묶인 점들의 가운데. 지도가 이 자리에 점을 찍는다.</summary>
    public double Lat { get; set; }

    /// <summary>묶인 점들의 가운데.</summary>
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
    /// 앞 자리에서 여기까지의 <b>직선</b> 거리(m). 첫 자리는 0 이다 —
    /// 실제로 걸은 거리가 아니다.
    /// </summary>
    public double MovedMeters { get; set; }
}

/// <summary>
/// 하루치 이동 경로 — <b>점 · 머문 자리 · 요약</b> 한 벌.
/// </summary>
public sealed class MyLocationTrackDto
{
    /// <summary>어느 날인가(<c>yyyy-MM-dd</c>). <b>한국 달력 날짜</b>다.</summary>
    public string Date { get; set; } = string.Empty;

    /// <summary>그날 기록된 점 전부. 시간순이다.</summary>
    public List<LocationTrackPointDto> Points { get; set; } = [];

    /// <summary>묶어 낸 머문 자리들. 시간순이고 지도의 선이 이 차례를 잇는다.</summary>
    public List<LocationStayDto> Stays { get; set; } = [];

    /// <summary>머문 자리들을 이은 직선 거리의 합(m). <b>주행 거리가 아니다.</b></summary>
    public double TotalMeters { get; set; }

    /// <summary>그날 가장 오래 머문 자리(분).</summary>
    public int LongestMinutes { get; set; }

    /// <summary>
    /// 묶는 데 쓴 반지름(m). <b>화면이 적어 보여 준다</b> — 「왜 여기와 저기가
    /// 한 자리인가」의 답이 이 숫자 하나다.
    /// </summary>
    public double StayRadiusMeters { get; set; }
}

/// <summary>기록이 있는 날 하나. 날짜 고르개가 빈 날을 피하게 해 준다. 포털을 안 연 날에는 한 줄도 쌓이지 않는다.</summary>
public sealed class LocationTrackDayDto
{
    /// <summary>한국 달력 날짜(<c>yyyy-MM-dd</c>).</summary>
    public string Date { get; set; } = string.Empty;

    /// <summary>그날 쌓인 점의 수.</summary>
    public int Count { get; set; }

    /// <summary>
    /// 고르개에 적히는 한 줄. <b>건수를 함께 적는다</b> — 날짜만 늘어놓으면
    /// 두 줄짜리 날과 하루 종일 켜 둔 날이 똑같아 보인다.
    /// </summary>
    public string Label => $"{Date} ({Count}회)";
}
