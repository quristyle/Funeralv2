namespace NotificationServer.DTOs;

// 위치 DTO — 허락해 서버에 남은 좌표. 내 위치 날씨 좌표(NotificationPreference.WeatherLat)와 같은 값을 지도용 모양으로 옮긴 것이다.

/// <summary>위치를 허용한 계정 하나(어디에 있고 쪽지가 닿는가). 좌표가 없는 사람은 목록에 없다.</summary>
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

    /// <summary>보여 줄 지역 이름(표시 전용). 날씨를 한 번도 안 받은 사람은 비어 있는 경우가 흔하다.</summary>
    public string? Place { get; set; }

    /// <summary>자리를 마지막으로 잡은 때 — 좌표가 실제로 달라진 때다.</summary>
    public DateTime? LocatedAt { get; set; }

    /// <summary>자리를 마지막으로 확인한 때. 안 움직였어도 포털을 열면 찍힌다. 지도에서 점이 얼마나 묵었는지는 이 값으로만 갈린다.</summary>
    public DateTime? SyncedAt { get; set; }

    /// <summary>내 위치 날씨를 켜 두었나. 위치 허용 여부와 다르다 — 스위치를 껐어도 좌표는 남는다.</summary>
    public bool WeatherLocalEnabled { get; set; }

    /// <summary>앱 푸시가 실제로 닿는가 — 끄지 않았고 등록한 기기가 있다.</summary>
    public bool PushReachable { get; set; }

    /// <summary>쪽지 메일이 닿는가 — 「쪽지 메일받기」를 켰고 주소가 있다.</summary>
    public bool EmailReachable { get; set; }

    /// <summary>쪽지를 받을 길이 있는가. 앱 푸시나 쪽지 메일 둘 중 하나면 된다(쪽지 보내기·찾기와 같은 값).</summary>
    public bool CanReceive => PushReachable || EmailReachable;
}

// 이동 경로 — 지나온 자리. 점은 저장하고 머문 자리는 LocationTrackService 가 셈한다.

/// <summary>기록된 점 하나 — 잰 그대로다. 묶지 않은 날것이라 흔들린 몇십 미터도 들어 있다. 머문 자리 묶음을 의심할 때 볼 곳이다.</summary>
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

/// <summary>머문 자리 하나 — 가까이 붙은 점들을 묶은 것. Minutes 는 관측된 처음과 마지막 사이라 실제보다 짧게 나온다.
/// 뒤 관측까지 세면 노트북을 덮고 잔 시간이 체류가 되므로 그쪽으로 틀리게 두며, Samples 를 함께 줘서 한 번만 본 자리를 구별한다.</summary>
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

    /// <summary>앞 자리에서 여기까지의 직선 거리(m). 첫 자리는 0 이다. 실제로 걸은 거리가 아니다.</summary>
    public double MovedMeters { get; set; }
}

/// <summary>하루치 이동 경로 — 점 · 머문 자리 · 요약 한 벌. 셋을 한 번에 내려 주는 것은 화면이 셋을 동시에 그리기 때문이다.</summary>
public class MyLocationTrackDto
{
    /// <summary>어느 날인가. 한국 달력 날짜(yyyy-MM-dd)다 — 하루의 경계는 한국 자정이다.</summary>
    public string Date { get; set; } = string.Empty;

    /// <summary>그날 기록된 점 전부. 시간순이다.</summary>
    public List<LocationTrackPointDto> Points { get; set; } = [];

    /// <summary>묶어 낸 머문 자리들. 시간순이고 지도의 선이 이 차례를 잇는다.</summary>
    public List<LocationStayDto> Stays { get; set; } = [];

    /// <summary>머문 자리들을 이은 직선 거리의 합(m). 실제 주행 거리가 아니다.</summary>
    public double TotalMeters { get; set; }

    /// <summary>그날 가장 오래 머문 자리(분). 머문 자리가 없으면 0 이다.</summary>
    public int LongestMinutes { get; set; }

    /// <summary>묶는 데 쓴 반지름(m). 화면이 적어 보여 준다.</summary>
    public double StayRadiusMeters { get; set; }
}

/// <summary>기록이 있는 날 하나. 날짜 고르개가 빈 날을 피하게 해 준다.</summary>
public class LocationTrackDayDto
{
    /// <summary>한국 달력 날짜(<c>yyyy-MM-dd</c>).</summary>
    public string Date { get; set; } = string.Empty;

    /// <summary>그날 쌓인 점의 수.</summary>
    public int Count { get; set; }
}
