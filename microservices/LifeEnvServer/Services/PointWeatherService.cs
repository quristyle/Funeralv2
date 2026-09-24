using LifeEnvServer.Data;
using LifeEnvServer.Dtos;
using LifeEnvServer.Utilities;
using Microsoft.EntityFrameworkCore;

namespace LifeEnvServer.Services;

/// <summary>
/// 위경도 한 쌍으로 <b>그 지점</b>의 지금 날씨와 예보를 만든다 — 「내 위치 날씨」의 속.
/// </summary>
/// <remarks>
/// <para>
/// 쓰는 곳이 둘이다 — 설정 화면이 「여기가 맞나」를 미리 보여 줄 때
/// (<c>GET /weather/point</c>)와, 발송기가 알림 본문을 만들 때
/// (<see cref="LocalWeatherNotifyService"/>). <b>둘이 같은 글을 보여야 한다</b> —
/// 미리 본 것과 실제로 온 알림의 말이 다르면 사람은 둘 중 하나를 믿지 못한다.
/// </para>
/// </remarks>
public class PointWeatherService
{
    private readonly LifeEnvDbContext _db;
    private readonly WeatherApiService _api;
    private readonly ILogger<PointWeatherService> _logger;

    public PointWeatherService(
        LifeEnvDbContext db, WeatherApiService api, ILogger<PointWeatherService> logger)
    {
        _db = db;
        _api = api;
        _logger = logger;
    }

    /// <summary>
    /// <b>이름과 격자만</b>. 기상청을 부르지 않는다.
    /// </summary>
    /// <remarks>
    /// 설정 화면이 <b>열릴 때마다</b> 저장해 둔 좌표가 어디인지 보여 주려고 부른다.
    /// 거기서 <see cref="GetAsync"/> 를 부르면 실황·단기예보 두 왕복이 매번 붙고,
    /// 기상청이 느린 날에는 <b>지역 이름조차 안 뜬다</b> — 이름은 우리 표에만
    /// 있으므로 바깥 없이 답한다.
    /// </remarks>
    public async Task<PointPlaceDto> GetPlaceAsync(double lat, double lon, CancellationToken ct = default)
    {
        var (nx, ny) = GridConverter.ToGrid(lat, lon);
        var found = await ResolvePlaceAsync(lat, lon, ct);

        return new PointPlaceDto
        {
            Lat = lat,
            Lon = lon,
            Nx = nx,
            Ny = ny,
            Place = found?.Name,
            Region1 = found?.Region1,
            Region2 = found?.Region2,
            Region3 = found?.Region3,
        };
    }

    /// <summary>한 지점의 날씨. 기상청이 답하지 않아도 지역 이름과 격자는 채워 돌려준다.</summary>
    public async Task<PointWeatherDto> GetAsync(double lat, double lon, CancellationToken ct = default)
    {
        var (nx, ny) = GridConverter.ToGrid(lat, lon);
        var found = await ResolvePlaceAsync(lat, lon, ct);
        var place = found?.Name;

        var result = new PointWeatherDto
        {
            Lat = lat,
            Lon = lon,
            Nx = nx,
            Ny = ny,
            Place = place,
            Region1 = found?.Region1,
            Region2 = found?.Region2,
            Region3 = found?.Region3,
        };

        var now = await _api.GetPointNowcastAsync(nx, ny, place ?? "내 위치");
        if (now != null)
        {
            result.Now = new PointWeatherNowDto
            {
                TemperatureC = now.TemperatureC,
                SensibleTemp = now.SensibleTemp,
                Condition = now.Condition,
                Humidity = now.Humidity,
                WindSpeed = now.WindSpeed,
                Rainfall = now.Rainfall,
                ObservedAt = now.ObservationTime,
            };
        }

        var forecast = await _api.GetVilageForecastAsync(nx, ny);
        if (forecast != null) result.Days = SummarizeDays(forecast);

        result.Summary = BuildSummary(result);
        return result;
    }

    /// <summary>
    /// 가장 가까운 행정구역 이름. 격자표(<c>ghub.grid_coordinates</c>)에서 찾는다.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>역지오코딩을 사지 않는다.</b> 이미 3800 곳의 읍면동 중심 좌표를 들고 있고,
    /// 알림에 필요한 것은 「어느 동네인가」 한 줄뿐이다. 바깥 서비스를 하나 더
    /// 물리면 그쪽이 죽었을 때 날씨 알림이 함께 멎는다.
    /// </para>
    /// <para>
    /// <b>위경도가 <c>0</c> 인 행을 뺀다.</b> 표에 그런 줄이 둘 있는데, 빼지 않으면
    /// 먼 남해상의 좌표를 물었을 때 그 빈 줄이 「가장 가까운 곳」으로 뽑힌다.
    /// </para>
    /// <para>
    /// 거리는 <b>제곱합 그대로</b> 비교한다. 위도 1도와 경도 1도의 실제 길이가 다르지만
    /// (한반도에서 경도 쪽이 약 0.8배), 가장 가까운 하나를 고르는 데는 순서만 맞으면
    /// 되고 후보들이 다 가까이 모여 있어 그 비율이 순서를 바꾸지 않는다.
    /// </para>
    /// </remarks>
    private async Task<PlaceName?> ResolvePlaceAsync(double lat, double lon, CancellationToken ct)
    {
        try
        {
            // 0.5도 상자 안만 본다. 3800 행을 전부 끌어오지 않으려는 것이고,
            // 상자가 비면(바다 한가운데) 이름 없이 간다.
            const double box = 0.5;

            var near = await _db.GridCoordinates
                .Where(g => g.LatitudeSecond100 != null && g.LongitudeSecond100 != null
                            && g.LatitudeSecond100 != 0 && g.LongitudeSecond100 != 0
                            && g.LatitudeSecond100 >= (decimal)(lat - box)
                            && g.LatitudeSecond100 <= (decimal)(lat + box)
                            && g.LongitudeSecond100 >= (decimal)(lon - box)
                            && g.LongitudeSecond100 <= (decimal)(lon + box))
                .Select(g => new
                {
                    g.Region1,
                    g.Region2,
                    g.Region3,
                    Lat = g.LatitudeSecond100!.Value,
                    Lon = g.LongitudeSecond100!.Value,
                })
                .OrderBy(g => (g.Lat - (decimal)lat) * (g.Lat - (decimal)lat)
                              + (g.Lon - (decimal)lon) * (g.Lon - (decimal)lon))
                .Take(NearbyRows)
                .ToListAsync(ct);

            if (near.Count == 0) return null;

            // **읍·면·동이 있는 줄을 먼저 고른다.** 표에는 시·군·구까지만 적힌
            // 줄(구청 자리)이 섞여 있고, 그 줄이 하필 제일 가까운 경우가 있다 —
            // 그러면 「울산광역시 동구」에서 끝나 동네 이름이 영영 안 나온다.
            // 실제로 그랬다(울산 동구 35.5086,129.4215).
            var pick = near.FirstOrDefault(g => !string.IsNullOrWhiteSpace(g.Region3)) ?? near[0];

            var region1 = Trimmed(pick.Region1);
            var region2 = Trimmed(pick.Region2);
            var region3 = Trimmed(pick.Region3);

            // **세종시는 1단계와 2단계가 같은 글자다**(표에 그렇게 들어 있다).
            // 그대로 이으면 「세종특별자치시 세종특별자치시 보람동」이 되고,
            // 시·도 칸과 시·군·구 칸에 같은 이름이 두 번 적힌다.
            if (string.Equals(region1, region2, StringComparison.Ordinal))
            {
                region2 = null;
            }

            var parts = new[] { region1, region2, region3 }
                .Where(p => !string.IsNullOrWhiteSpace(p))
                .ToArray();

            var name = string.Join(" ", parts);
            if (string.IsNullOrWhiteSpace(name)) return null;

            return new PlaceName(name, region1, region2, region3);
        }
        catch (Exception ex)
        {
            // 이름을 못 찾아도 날씨는 보낼 수 있다. 여기서 던지면 알림이 통째로 멎는다.
            _logger.LogWarning(ex, "지역 이름 조회 실패 (lat={Lat}, lon={Lon})", lat, lon);
            return null;
        }
    }

    /// <summary>
    /// 읍·면·동이 있는 줄을 찾으려고 <b>몇 줄까지 들춰 보나</b>.
    /// </summary>
    /// <remarks>
    /// 도심에서는 이 스무 줄이 반경 2km 안에 다 들어오고, 시골에서도 같은 군을
    /// 벗어나지 않는다. 더 늘리면 <b>옆 동네 이름</b>을 끌어올 수 있고, 줄이면
    /// 구청 자리만 있는 곳에서 동 이름을 못 찾는다.
    /// </remarks>
    private const int NearbyRows = 20;

    private static string? Trimmed(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    /// <summary>
    /// 찾아낸 행정구역 한 줄. <b>이어 붙인 이름과 세 단계를 함께</b> 들고 다닌다 —
    /// 화면은 단계별로 보여 주고 알림 본문은 이어 붙인 것을 쓴다.
    /// </summary>
    private sealed record PlaceName(string Name, string? Region1, string? Region2, string? Region3);

    /// <summary>
    /// 단기예보(시간별)를 <b>하루 단위로</b> 접는다.
    /// </summary>
    /// <remarks>
    /// 알림 한 통에 시간별 예보를 다 실을 수 없다 — 안드로이드는 두세 줄에서 자른다.
    /// 사람이 아침에 알고 싶은 것은 <b>오늘 얼마나 춥고 더운가</b>와 <b>비가 오는가</b>
    /// 둘이라, 그 둘만 남긴다.
    /// </remarks>
    private static List<PointWeatherDayDto> SummarizeDays(List<Dictionary<string, string>> forecast)
    {
        var today = Kst.Now.Date;

        return forecast
            .Where(f => f.ContainsKey("fcstDate"))
            .GroupBy(f => f["fcstDate"])
            .OrderBy(g => g.Key)
            .Take(3)
            .Select(g =>
            {
                var rows = g.ToList();

                // TMN·TMX 는 하루에 한 번만 실린다(각각 06시·15시 칸). 없는 날은
                // 시간별 기온(TMP)에서 뽑는다 — 오늘치는 이미 지난 시각이 빠져 있어
                // 실제 최저보다 높게 나오지만, 없는 것보다 낫다.
                var temps = rows
                    .Where(r => r.ContainsKey("TMP") && double.TryParse(r["TMP"], out _))
                    .Select(r => double.Parse(r["TMP"]))
                    .ToList();

                double? min = FirstValue(rows, "TMN") ?? (temps.Count > 0 ? temps.Min() : null);
                double? max = FirstValue(rows, "TMX") ?? (temps.Count > 0 ? temps.Max() : null);

                var pops = rows
                    .Where(r => r.ContainsKey("POP") && int.TryParse(r["POP"], out _))
                    .Select(r => int.Parse(r["POP"]))
                    .ToList();

                DateTime.TryParseExact(g.Key, "yyyyMMdd", null,
                    System.Globalization.DateTimeStyles.None, out var date);

                return new PointWeatherDayDto
                {
                    Date = g.Key,
                    Label = LabelFor(date, today),
                    MinC = min,
                    MaxC = max,
                    RainProbability = pops.Count > 0 ? pops.Max() : null,
                    Condition = DayCondition(rows),
                };
            })
            .ToList();
    }

    private static double? FirstValue(List<Dictionary<string, string>> rows, string category)
    {
        foreach (var row in rows)
        {
            if (row.TryGetValue(category, out var raw) && double.TryParse(raw, out var v)) return v;
        }
        return null;
    }

    /// <summary>
    /// 하루를 한 낱말로. <b>비·눈이 하늘 상태를 이긴다</b> — 「구름많음」이라 적어 두고
    /// 비가 오면 그 알림은 틀린 것이 된다.
    /// </summary>
    private static string? DayCondition(List<Dictionary<string, string>> rows)
    {
        var ptys = rows
            .Where(r => r.ContainsKey("PTY") && int.TryParse(r["PTY"], out _))
            .Select(r => int.Parse(r["PTY"]))
            .Where(p => p > 0)
            .ToList();

        if (ptys.Count > 0)
        {
            if (ptys.Contains(3) || ptys.Contains(7)) return "눈";
            if (ptys.Contains(2) || ptys.Contains(6)) return "비/눈";
            return "비";
        }

        var skies = rows
            .Where(r => r.ContainsKey("SKY") && int.TryParse(r["SKY"], out _))
            .Select(r => int.Parse(r["SKY"]))
            .ToList();

        if (skies.Count == 0) return null;

        // 흐린 시간이 절반을 넘으면 흐린 날이다. 낮 한때 갠 것을 「맑음」이라 하면
        // 우산을 안 챙긴다.
        var cloudy = skies.Count(s => s >= 4);
        var partly = skies.Count(s => s == 3);

        if (cloudy * 2 > skies.Count) return "흐림";
        if ((cloudy + partly) * 2 > skies.Count) return "구름많음";
        return "맑음";
    }

    private static string LabelFor(DateTime date, DateTime today)
    {
        var diff = (date.Date - today).Days;
        return diff switch
        {
            0 => "오늘",
            1 => "내일",
            2 => "모레",
            _ => date.ToString("M/d"),
        };
    }

    /// <summary>
    /// 알림 본문. <b>첫 줄이 지금, 둘째 줄부터가 예보다.</b>
    /// </summary>
    /// <remarks>
    /// 알림창은 접혀 있을 때 한 줄만 보인다. 그래서 「지금 몇 도에 무슨 날씨인가」를
    /// 맨 앞에 둔다 — 펼치지 않고도 답이 되는 것이 그것 하나뿐이다.
    /// </remarks>
    private static string BuildSummary(PointWeatherDto point)
    {
        var lines = new List<string>();

        if (point.Now is { } now)
        {
            var head = $"지금 {now.TemperatureC:0.#}℃";
            if (!string.IsNullOrWhiteSpace(now.Condition)) head += $" {now.Condition}";
            if (now.SensibleTemp is { } st && Math.Abs(st - now.TemperatureC) >= 1)
                head += $" (체감 {st:0.#}℃)";
            if (now.Humidity is { } h) head += $" · 습도 {h}%";
            if (now.Rainfall is { } r && r > 0) head += $" · 강수 {r:0.#}mm";
            lines.Add(head);
        }

        foreach (var day in point.Days.Take(2))
        {
            var parts = new List<string>();
            if (day.MinC is { } lo && day.MaxC is { } hi) parts.Add($"{lo:0}/{hi:0}℃");
            else if (day.MaxC is { } only) parts.Add($"최고 {only:0}℃");
            if (!string.IsNullOrWhiteSpace(day.Condition)) parts.Add(day.Condition!);
            if (day.RainProbability is { } pop && pop > 0) parts.Add($"강수확률 {pop}%");

            if (parts.Count > 0) lines.Add($"{day.Label} {string.Join(" · ", parts)}");
        }

        return lines.Count > 0
            ? string.Join("\n", lines)
            : "기상청 자료를 받지 못했습니다.";
    }
}
