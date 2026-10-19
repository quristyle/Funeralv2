using JSini.Web.Http;
using JSini.Web.Models;

namespace JSini.Web.LifeEnv.Api;

/// <summary>
/// LifeEnvServer(기상) 호출. 게이트웨이의 <c>/life</c> 아래로 나간다. 공통 봉투를 쓰므로 GatewayClient 의 봉투 메서드를 탄다.
/// </summary>
public sealed class LifeEnvClient(GatewayClient gateway)
{
    /// <summary>게이트웨이가 이 서비스로 라우팅하는 접두사.</summary>
    private const string Prefix = "life/weather";

    // ── 실황 ─────────────────────────────────────────────────

    /// <summary>전 지역 최신 실황. 20분 지나면 서버가 기상청을 다시 부른다.</summary>
    public Task<IReadOnlyList<WeatherInfo>> GetLatestWeatherAsync(CancellationToken ct = default)
        => gateway.GetListAsync<WeatherInfo>(Prefix, ct);

    /// <summary>한 지역 실황 (어제 기온 포함).</summary>
    public Task<WeatherInfo?> GetCurrentWeatherAsync(int locationId, CancellationToken ct = default)
        => gateway.GetOneAsync<WeatherInfo>($"{Prefix}/current/{locationId}", ct);

    /// <summary>실측 이력 (지역명 · 일수).</summary>
    public Task<IReadOnlyList<WeatherInfo>> GetHistoryAsync(
        string location, int days, CancellationToken ct = default)
        => gateway.GetListAsync<WeatherInfo>(
            $"{Prefix}/history?location={Uri.EscapeDataString(location)}&days={days}", ct);

    /// <summary>특정 시각(KST)의 일자별 기온.</summary>
    public Task<IReadOnlyList<HourlyTemp>> GetHourlyHistoryAsync(
        int locationId, int hour, int days = 7, CancellationToken ct = default)
        => gateway.GetListAsync<HourlyTemp>(
            $"{Prefix}/history/hourly?locationId={locationId}&hour={hour}&days={days}", ct);

    // ── 예보 ─────────────────────────────────────────────────

    /// <summary>과거 -10h ~ 미래 +10h 타임라인 (실측 + 단기 + 초단기 병합).</summary>
    public Task<IReadOnlyList<WeatherTimelinePoint>> GetForecastAsync(
        int locationId, CancellationToken ct = default)
        => gateway.GetListAsync<WeatherTimelinePoint>($"{Prefix}/forecast/{locationId}", ct);

    /// <summary>주간(오늘~10일) 예보 — 단기 + 중기 병합.</summary>
    public Task<IReadOnlyList<MidTermForecast>> GetMidTermForecastAsync(
        int locationId, CancellationToken ct = default)
        => gateway.GetListAsync<MidTermForecast>($"{Prefix}/mid-term/{locationId}", ct);

    // ── 한 지점(내 위치) ─────────────────────────────────────

    /// <summary>
    /// <b>등록 지역이 아닌 한 지점</b>의 지금 날씨와 사흘 예보. 응답에 어디인가(격자·행정구역 이름)가 함께 와서 화면이 「여기가 맞나」를 되물을 수 있다.
    /// 알림 설정의 「내 위치 날씨」 미리보기도 같은 길·같은 모양(PointWeatherDto)을 쓴다 — 여기서 갈라지지 않게 한다.
    /// </summary>
    /// <param name="lat">위도.</param>
    /// <param name="lon">경도.</param>
    /// <param name="ct">중단 신호.</param>
    /// <param name="weekly">참이면 주간 예보까지 받는다 — 서버가 기상청을 두 번 더 부른다. 현황판만 참으로 부른다.</param>
    public Task<PointWeatherDto?> GetPointWeatherAsync(
        double lat, double lon, bool weekly = false, CancellationToken ct = default)
        => gateway.GetOneAsync<PointWeatherDto>(
            FormattableString.Invariant(
                $"{Prefix}/point?lat={lat}&lon={lon}{(weekly ? "&weekly=true" : "")}"), ct);

    // ── 특보 ─────────────────────────────────────────────────

    /// <summary>특보 목록. <paramref name="all"/> 이면 최근 7일 전체 + 매칭 지역·문장 포함.</summary>
    public Task<IReadOnlyList<WeatherWarning>> GetWarningsAsync(
        bool all = false, CancellationToken ct = default)
        => gateway.GetListAsync<WeatherWarning>(
            all ? $"{Prefix}/warnings?all=true" : $"{Prefix}/warnings", ct);

    /// <summary>특보 + 통보문 + 매칭 지역 + 관련 구역 + 문장 묶음.</summary>
    public Task<WeatherWarningFullDetails?> GetWarningFullDetailsAsync(
        int id, CancellationToken ct = default)
        => gateway.GetOneAsync<WeatherWarningFullDetails>($"{Prefix}/warnings/{id}/full", ct);

    /// <summary>오늘 통보문 중 관리지역이 걸린 문장 (제목별 최초·최종).</summary>
    public Task<IReadOnlyList<LocationWarningSentence>> GetWarnings4LocationRangeAsync(
        CancellationToken ct = default)
        => gateway.GetListAsync<LocationWarningSentence>($"{Prefix}/warnings4location-range", ct);

    /// <summary>특보구역 마스터 (기상청 구역 트리).</summary>
    public Task<IReadOnlyList<WeatherWarningZone>> GetWarningZonesAsync(CancellationToken ct = default)
        => gateway.GetListAsync<WeatherWarningZone>($"{Prefix}/warning-zones", ct);

    // ── 지역 관리 ────────────────────────────────────────────

    public Task<IReadOnlyList<WeatherLocation>> GetLocationsAsync(CancellationToken ct = default)
        => gateway.GetListAsync<WeatherLocation>($"{Prefix}/locations", ct);

    public Task CreateLocationAsync(WeatherLocation location, CancellationToken ct = default)
        => gateway.PostAsync($"{Prefix}/locations", location, ct);

    public Task UpdateLocationAsync(int id, WeatherLocation location, CancellationToken ct = default)
        => gateway.PutAsync($"{Prefix}/locations/{id}", location, ct);

    public Task DeleteLocationAsync(int id, CancellationToken ct = default)
        => gateway.DeleteAsync($"{Prefix}/locations/{id}", ct);

    public Task ReorderLocationsAsync(IReadOnlyList<ReorderItem> items, CancellationToken ct = default)
        => gateway.PutAsync($"{Prefix}/locations/reorder", items, ct);

    /// <summary>행정구역명으로 기상청 격자좌표(nx/ny)를 찾는다.</summary>
    public Task<IReadOnlyList<GridCoordinate>> SearchGridAsync(string query, CancellationToken ct = default)
        => gateway.GetListAsync<GridCoordinate>(
            $"{Prefix}/locations/search-grid?query={Uri.EscapeDataString(query)}", ct);

    // ── 기준 관리 ────────────────────────────────────────────

    public Task<IReadOnlyList<WeatherStandard>> GetStandardsAsync(CancellationToken ct = default)
        => gateway.GetListAsync<WeatherStandard>($"{Prefix}/standards", ct);

    public Task CreateStandardAsync(WeatherStandard standard, CancellationToken ct = default)
        => gateway.PostAsync($"{Prefix}/standards", standard, ct);

    public Task UpdateStandardAsync(int id, WeatherStandard standard, CancellationToken ct = default)
        => gateway.PutAsync($"{Prefix}/standards/{id}", standard, ct);

    public Task DeleteStandardAsync(int id, CancellationToken ct = default)
        => gateway.DeleteAsync($"{Prefix}/standards/{id}", ct);

    // ── 대응 요령 ────────────────────────────────────────────

    public Task<IReadOnlyList<WeatherResponseItem>> GetResponsesByStandardAsync(
        int standardId, CancellationToken ct = default)
        => gateway.GetListAsync<WeatherResponseItem>($"{Prefix}/responses/by-standard/{standardId}", ct);

    public Task CreateResponseAsync(WeatherResponseItem response, CancellationToken ct = default)
        => gateway.PostAsync($"{Prefix}/responses", response, ct);

    public Task UpdateResponseAsync(int id, WeatherResponseItem response, CancellationToken ct = default)
        => gateway.PutAsync($"{Prefix}/responses/{id}", response, ct);

    public Task DeleteResponseAsync(int id, CancellationToken ct = default)
        => gateway.DeleteAsync($"{Prefix}/responses/{id}", ct);

    public Task ReorderResponsesAsync(IReadOnlyList<ReorderItem> items, CancellationToken ct = default)
        => gateway.PutAsync($"{Prefix}/responses/reorder", items, ct);

    // ── 이벤트 기록 ──────────────────────────────────────────

    /// <summary>기준 초과 이벤트 목록 (서버 페이징).</summary>
    public Task<WeatherEventPage?> GetEventsAsync(
        int page,
        int pageSize,
        string? startDate = null,
        string? endDate = null,
        int? locationId = null,
        CancellationToken ct = default)
    {
        var query = $"page={page}&pageSize={pageSize}";
        if (!string.IsNullOrEmpty(startDate))
        {
            query += $"&startDate={Uri.EscapeDataString(startDate)}";
        }

        if (!string.IsNullOrEmpty(endDate))
        {
            query += $"&endDate={Uri.EscapeDataString(endDate)}";
        }

        if (locationId is not null)
        {
            query += $"&locationId={locationId}";
        }

        return gateway.GetOneAsync<WeatherEventPage>($"{Prefix}/events?{query}", ct);
    }

    /// <summary>지금 발효 중인 이벤트 (최근 20분).</summary>
    public Task<IReadOnlyList<WeatherEventRecord>> GetCurrentEventsAsync(
        int locationId, CancellationToken ct = default)
        => gateway.GetListAsync<WeatherEventRecord>($"{Prefix}/events/current?locationId={locationId}", ct);

    public Task DeleteEventAsync(int id, CancellationToken ct = default)
        => gateway.DeleteAsync($"{Prefix}/events/{id}", ct);
}
