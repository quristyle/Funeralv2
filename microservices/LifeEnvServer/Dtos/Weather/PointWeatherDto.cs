namespace LifeEnvServer.Dtos;

/// <summary>
/// <b>등록된 지역이 아니라 한 지점</b>의 날씨 — 「내 위치 날씨」가 쓰는 모양.
/// </summary>
/// <remarks>
/// <para>
/// 기존 화면들은 회사가 등록해 둔 관측 지역(<c>ghub.weather_locations</c>)을 본다.
/// 이쪽은 브라우저가 준 위경도 한 쌍이고 표에 행이 없다 — 그래서 응답에
/// <b>어디인지</b>(격자·지역 이름)가 함께 실린다. 화면이 「여기가 맞나」를
/// 사람에게 되물을 수 있어야 하기 때문이다.
/// </para>
/// </remarks>
public class PointWeatherDto
{
    public double Lat { get; set; }
    public double Lon { get; set; }

    /// <summary>기상청 격자. 위경도에서 계산한 값이다.</summary>
    public int Nx { get; set; }
    public int Ny { get; set; }

    /// <summary>
    /// 가장 가까운 행정구역 이름(예: <c>울산광역시 남구 삼산동</c>).
    /// 못 찾으면 비어 있다 — 좌표가 바다 위일 수도 있다.
    /// </summary>
    public string? Place { get; set; }

    /// <summary>지금 날씨. 기상청 실황이 없으면 비어 있다.</summary>
    public PointWeatherNowDto? Now { get; set; }

    /// <summary>오늘부터 사흘치 예보. 단기예보가 닿는 데까지만 채운다.</summary>
    public List<PointWeatherDayDto> Days { get; set; } = new();

    /// <summary>
    /// 알림 본문으로 쓸 한 덩이 글. <b>서버가 만든다</b> —
    /// 푸시를 보내는 쪽(NotificationServer)은 기상청 코드값을 모른다.
    /// </summary>
    public string? Summary { get; set; }
}

/// <summary>지금 실황.</summary>
public class PointWeatherNowDto
{
    public double TemperatureC { get; set; }
    public double? SensibleTemp { get; set; }
    public string? Condition { get; set; }
    public int? Humidity { get; set; }
    public double? WindSpeed { get; set; }
    public double? Rainfall { get; set; }
    public DateTimeOffset ObservedAt { get; set; }
}

/// <summary>하루치 예보 요약.</summary>
public class PointWeatherDayDto
{
    /// <summary><c>yyyyMMdd</c> (KST)</summary>
    public string Date { get; set; } = string.Empty;

    /// <summary>사람이 읽는 이름 — <c>오늘</c> · <c>내일</c> · <c>9/26(금)</c></summary>
    public string Label { get; set; } = string.Empty;

    /// <summary>최저·최고 기온. 단기예보의 TMN·TMX 이고, 없으면 그 날 시간별 기온에서 뽑는다.</summary>
    public double? MinC { get; set; }
    public double? MaxC { get; set; }

    /// <summary>그 날 시간별 강수확률(POP) 중 가장 큰 값.</summary>
    public int? RainProbability { get; set; }

    /// <summary>하늘 상태 요약 — <c>맑음</c> · <c>구름많음</c> · <c>흐림</c> · <c>비</c> · <c>눈</c></summary>
    public string? Condition { get; set; }
}
