namespace JSini.Web.LifeEnv.Api;

// ────────────────────────────────────────────────────────────────
// LifeEnvServer(기상) 응답 DTO 모음.
//
// 서버 원본은 microservices/LifeEnvServer 의 EF 엔티티·record 다.
// 프로젝트 참조 대신 읽기 전용 사본을 두는 이유는 ApiEnvelope 와 같다 —
// 프론트가 백엔드의 배포 일정·프레임워크 버전에 묶이지 않게 한다.
// 여기 적은 필드는 Vue 화면(views/life)이 실제로 읽던 것만이다.
// ────────────────────────────────────────────────────────────────

/// <summary>관측 지역 (<c>weather_locations</c>).</summary>
public sealed class WeatherLocation
{
    public int Id { get; set; }

    public string Name { get; set; } = string.Empty;

    /// <summary>기상청 격자 X. 서버 프로퍼티는 <c>NX</c> 지만 JSON 은 <c>nx</c> 다.</summary>
    public int Nx { get; set; }

    /// <summary>기상청 격자 Y.</summary>
    public int Ny { get; set; }

    public string? Region3 { get; set; }

    public string? Description { get; set; }

    /// <summary>중기예보(육상) 구역 코드.</summary>
    public string? MidTermLandCode { get; set; }

    /// <summary>중기예보(기온) 구역 코드.</summary>
    public string? MidTermTempCode { get; set; }

    /// <summary>특보구역 코드 (기상청 REG_ID).</summary>
    public string? WarningAreaCode { get; set; }

    public bool IsActive { get; set; } = true;

    public int SortOrder { get; set; }
}

/// <summary>실황 한 건 (수집 이력, <c>weather_infos</c>).</summary>
public sealed class WeatherInfo
{
    public int Id { get; set; }

    public string Location { get; set; } = string.Empty;

    public int? WeatherLocationId { get; set; }

    /// <summary>관측 시각 (UTC).</summary>
    public DateTimeOffset ObservationTime { get; set; }

    public double TemperatureC { get; set; }

    public string Condition { get; set; } = string.Empty;

    public int? Humidity { get; set; }

    public double? WindSpeed { get; set; }

    public double? Rainfall { get; set; }

    public double? Snowfall { get; set; }

    public double? WindDirection { get; set; }

    /// <summary>강수형태 코드 (0 없음 · 1 비 · 2 비/눈 · 3 눈 · 5 빗방울 · 6 빗방울/눈날림 · 7 눈날림).</summary>
    public int? Pty { get; set; }

    public double? SensibleTemp { get; set; }

    /// <summary>어제 같은 시각 기온. <c>current/{id}</c> 응답에서만 채워진다.</summary>
    public double? YesterdayTemperature { get; set; }
}

/// <summary>
/// 예보 타임라인 한 칸 (과거 실측 + 미래 예보 병합, <c>forecast/{id}</c>).
///
/// 서버 필드는 <c>isForecast</c> 다. Vue 는 있지도 않은 <c>isPast</c> 를 읽어서
/// NOW 표시가 사실상 동작하지 않았다 — 여기서는 서버 필드를 그대로 읽고
/// <see cref="IsPast"/> 로 의도(과거 = 실측)를 되살린다.
/// </summary>
public sealed class WeatherTimelinePoint
{
    /// <summary>KST 날짜 (yyyyMMdd).</summary>
    public string Date { get; set; } = string.Empty;

    /// <summary>KST 시각 (HHmm).</summary>
    public string Time { get; set; } = string.Empty;

    public double Temp { get; set; }

    /// <summary>강수확률(%). 실측 구간에는 없다.</summary>
    public int? Pop { get; set; }

    public double? Rain { get; set; }

    /// <summary>과거 구간은 실황 텍스트, 예보 구간은 SKY 코드 문자열.</summary>
    public string Sky { get; set; } = string.Empty;

    /// <summary>PTY 코드 문자열.</summary>
    public string Pty { get; set; } = string.Empty;

    public double WindSpeed { get; set; }

    public double WindDir { get; set; }

    public int? Reh { get; set; }

    public double? Uuu { get; set; }

    public double? Vvv { get; set; }

    public double? Sno { get; set; }

    public bool IsForecast { get; set; }

    /// <summary>실측(이력) 구간인가.</summary>
    public bool IsPast => !IsForecast;
}

/// <summary>주간(중기+단기 병합) 예보 하루치 (<c>mid-term/{id}</c>).</summary>
public sealed class MidTermForecast
{
    /// <summary>yyyy-MM-dd.</summary>
    public string Date { get; set; } = string.Empty;

    /// <summary>"오늘" · "내일" · "3일후" 같은 표시 문구.</summary>
    public string DayDisplay { get; set; } = string.Empty;

    public int MinTemp { get; set; }

    public int MaxTemp { get; set; }

    /// <summary>오전 하늘 상태 텍스트. 하루 단위 예보면 비어 있다.</summary>
    public string AmSky { get; set; } = string.Empty;

    public string PmSky { get; set; } = string.Empty;

    public int AmPop { get; set; }

    public int PmPop { get; set; }
}

/// <summary>판정 기준 (<c>weather_standards</c>).</summary>
public sealed class WeatherStandard
{
    public int Id { get; set; }

    /// <summary>WIND · RAIN · SNOW · HEAT · COLD · T1H · REH.</summary>
    public string Category { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public string ConditionText { get; set; } = string.Empty;

    public double? ThresholdValue { get; set; }

    /// <summary>GE · LE · GT · LT · EQ · BT · NB · DGE · DLE.</summary>
    public string? Operator { get; set; }

    public double? ThresholdValue2 { get; set; }

    public string? Unit { get; set; }

    /// <summary>ALLOW · CAUTION · RESTRICTED · SUSPENDED.</summary>
    public string? WorkStatus { get; set; }

    public int SortOrder { get; set; }

    /// <summary>지속 기간(일).</summary>
    public int? Duration { get; set; }

    /// <summary>한파 복합 조건 — 전일 대비 하강.</summary>
    public double? PrevDayDiff { get; set; }

    /// <summary>한파 복합 조건 — 평년 대비 차이.</summary>
    public double? AvgYearDiff { get; set; }

    /// <summary>알림 재발송 대기(분). 0 은 매번.</summary>
    public int? NotificationInterval { get; set; }

    public bool UseSensibleTemp { get; set; }
}

/// <summary>기준별 대응 요령 (<c>weather_responses</c>).</summary>
public sealed class WeatherResponseItem
{
    public int Id { get; set; }

    public int WeatherStandardId { get; set; }

    public WeatherStandard? WeatherStandard { get; set; }

    public string ActionContent { get; set; } = string.Empty;

    public string? Description { get; set; }

    public int SortOrder { get; set; }
}

/// <summary>기준 초과 이벤트 (<c>weather_event_records</c>).</summary>
public sealed class WeatherEventRecord
{
    public int Id { get; set; }

    public int WeatherInfoId { get; set; }

    public WeatherInfo? WeatherInfo { get; set; }

    public int WeatherStandardId { get; set; }

    public WeatherStandard? WeatherStandard { get; set; }

    /// <summary>발생 시각 (UTC).</summary>
    public DateTimeOffset EventTime { get; set; }

    public double MeasuredValue { get; set; }

    public bool IsNotified { get; set; }
}

/// <summary>이벤트 목록 페이지 (<c>events</c> 응답 — <c>{ items, totalCount, page, pageSize }</c>).</summary>
public sealed class WeatherEventPage
{
    public List<WeatherEventRecord> Items { get; set; } = [];

    public int TotalCount { get; set; }

    public int Page { get; set; }

    public int PageSize { get; set; }
}

/// <summary>기상 특보 (<c>weather_warnings</c>). <c>all=true</c> 조회에는 매칭 지역·문장이 실려 온다.</summary>
public sealed class WeatherWarning
{
    public int Id { get; set; }

    public int StnId { get; set; }

    /// <summary>발표 시각 (yyyyMMddHHmm, KST 문자열).</summary>
    public string TmFc { get; set; } = string.Empty;

    public int TmSeq { get; set; }

    public string Title { get; set; } = string.Empty;

    public string Content { get; set; } = string.Empty;

    public string? Other { get; set; }

    public string? WarningNum { get; set; }

    /// <summary>발령 · 해제 · 변경 등.</summary>
    public string? Command { get; set; }

    public List<WeatherLocation> MatchedLocations { get; set; } = [];

    public List<WeatherWarningSentence> Sentences { get; set; } = [];
}

/// <summary>통보문 문장 (<c>weather_warning_msg_sentences</c>).</summary>
public sealed class WeatherWarningSentence
{
    public int Id { get; set; }

    public int Sequence { get; set; }

    public string? Title { get; set; }

    public string Content { get; set; } = string.Empty;
}

/// <summary>
/// 오늘 통보문 중 관리지역이 걸린 문장 (<c>warnings4location-range</c> 응답).
/// 서버가 익명 타입으로 내려 주는 모양을 그대로 받는다.
/// </summary>
public sealed class LocationWarningSentence
{
    public int Id { get; set; }

    public string? Title { get; set; }

    public string Content { get; set; } = string.Empty;

    public int Sequence { get; set; }

    public string? Command { get; set; }

    public LocationWarningSentenceMsg? WeatherWarningMsg { get; set; }
}

/// <summary>위 문장이 딸린 통보문의 발표 시각만 담는 축소형.</summary>
public sealed class LocationWarningSentenceMsg
{
    public string TmFc { get; set; } = string.Empty;
}

/// <summary>통보문 (<c>weather_warning_msgs</c>).</summary>
public sealed class WeatherWarningMsg
{
    public int Id { get; set; }

    public string TmFc { get; set; } = string.Empty;

    public int StnId { get; set; }

    public int TmSeq { get; set; }

    /// <summary>제목.</summary>
    public string? T1 { get; set; }

    /// <summary>발표 내용.</summary>
    public string? T2 { get; set; }

    /// <summary>특보 발효 현황 요약.</summary>
    public string? T6 { get; set; }

    /// <summary>예비특보 발효 현황 요약.</summary>
    public string? T7 { get; set; }
}

/// <summary>특보구역 마스터 (<c>weather_warning_zones</c>).</summary>
public sealed class WeatherWarningZone
{
    public string RegId { get; set; } = string.Empty;

    /// <summary>구역명 약어.</summary>
    public string? RegKo { get; set; }

    /// <summary>구역명 전체 (공백으로 나뉜 키워드로 문장 매칭에 쓴다).</summary>
    public string? RegName { get; set; }
}

/// <summary>특보 통합 상세 (<c>warnings/{id}/full</c>).</summary>
public sealed class WeatherWarningFullDetails
{
    public WeatherWarning? Warning { get; set; }

    public WeatherWarningMsg? Msg { get; set; }

    public List<WeatherLocation> MatchedLocations { get; set; } = [];

    public List<WeatherWarningZone> RelatedZones { get; set; } = [];

    public List<WeatherWarningSentence> Sentences { get; set; } = [];
}

/// <summary>격자좌표 검색 결과 (<c>locations/search-grid</c>).</summary>
public sealed class GridCoordinate
{
    public string AdministrativeCode { get; set; } = string.Empty;

    public string? Region1 { get; set; }

    public string? Region2 { get; set; }

    public string? Region3 { get; set; }

    public int Nx { get; set; }

    public int Ny { get; set; }
}

/// <summary>특정 시각(KST)의 일자별 기온 (<c>history/hourly</c> 응답).</summary>
public sealed class HourlyTemp
{
    /// <summary>yyyy-MM-dd.</summary>
    public string Date { get; set; } = string.Empty;

    public double Temp { get; set; }
}

/// <summary>순서 변경 요청 한 건 (<c>locations/reorder</c> · <c>responses/reorder</c>).</summary>
public sealed record ReorderItem(int Id, int SortOrder);
