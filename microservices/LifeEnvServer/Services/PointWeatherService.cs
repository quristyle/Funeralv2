using LifeEnvServer.Data;
using LifeEnvServer.Dtos;
using LifeEnvServer.Utilities;
using Microsoft.EntityFrameworkCore;

namespace LifeEnvServer.Services;

/// <summary>
/// 위경도 한 쌍으로 그 지점의 지금 날씨와 예보를 만든다. 설정 화면 미리보기와 알림 본문이 같은 글을 보도록 둘 다 여기를 부른다.
/// </summary>
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
    /// 이름과 격자만 돌려준다. 기상청을 부르지 않는다 — 설정 화면이 열릴 때마다 불리므로 여기서 기상청을 부르면 느린 날 지역 이름조차 안 뜬다.
    /// </summary>
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

    /// <summary>
    /// 한 지점의 날씨. 기상청이 답하지 않아도 지역 이름과 격자는 채운다. weekly 가 참이면 주간 예보까지 채운다 — 기상청 왕복이 둘 늘어나므로 알림 본문은 거짓으로 둔다.
    /// </summary>
    public async Task<PointWeatherDto> GetAsync(
        double lat, double lon, bool weekly = false, CancellationToken ct = default)
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

        // 단기예보를 다시 받지 않는다. 주간 예보의 앞 이틀은 위에서 받은 응답에서 갈라 낸다.
        if (weekly) result.Weekly = await BuildWeeklyAsync(forecast, found, ct);

        result.Summary = BuildSummary(result);
        return result;
    }

    /// <summary>
    /// 한 지점의 주간 예보(내일부터 열흘). 앞쪽은 단기예보, 뒤쪽은 중기예보다.
    /// 중기예보는 격자로 못 묻고 시·도·시·군으로 구역코드를 고른다 — 이름을 못 찾으면 주간 예보도 없다. 중기 구역은 시·군 단위다.
    /// </summary>
    private async Task<List<MidTermForecastDto>> BuildWeeklyAsync(
        List<Dictionary<string, string>>? forecast, PlaceName? place, CancellationToken ct)
    {
        var today = Kst.Now.Date;

        // 단기예보에서 오늘을 뺀 나머지 날. 오늘은 실황과 사흘 예보가 이미 말했으므로 같은 말이 두 번 서지 않게 한다.
        List<MidTermForecastDto> shortTerm = forecast is null
            ? []
            : SummarizeHalfDays(forecast, today);

        var (landCode, tempCode) = MidTermRegions.Resolve(place?.Region1, place?.Region2);

        if (landCode is null || tempCode is null)
        {
            return shortTerm;
        }

        var baseDate = MidTermForecastReader.BaseDate(Kst.Now);

        string? landJson, tempJson;

        try
        {
            // 나란히 부른다. 서로 기다릴 이유가 없고 둘 다 있어야 한 줄이 된다.
            var land = _api.GetMidLandForecastAsync(landCode);
            var temp = _api.GetMidTaAsync(tempCode);

            await Task.WhenAll(land, temp).WaitAsync(ct);

            landJson = land.Result;
            tempJson = temp.Result;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // 중기예보를 못 받아도 앞쪽 단기예보는 보여 줄 수 있다. 여기서
            // 던지면 실황까지 통째로 사라진다.
            _logger.LogWarning(ex, "중기예보 조회 실패 (land={Land}, temp={Temp})", landCode, tempCode);
            return shortTerm;
        }

        var seen = shortTerm.Select(d => d.Date).ToHashSet(StringComparer.Ordinal);

        foreach (var day in MidTermForecastReader.Read(landJson, tempJson, baseDate))
        {
            var date = day.Date.ToString("yyyy-MM-dd");
            if (!seen.Add(date)) continue;

            shortTerm.Add(new MidTermForecastDto(
                date,
                WeekLabel(day.Date.ToDateTime(TimeOnly.MinValue), today),
                day.MinTemp,
                day.MaxTemp,
                day.AmSky,
                day.PmSky,
                day.AmPop,
                day.PmPop));
        }

        return [.. shortTerm.OrderBy(d => d.Date, StringComparer.Ordinal)];
    }

    /// <summary>
    /// 단기예보를 오전·오후로 갈라 하루 한 줄로 접는다. 주간 예보 카드가 오전·오후 두 칸을 그리므로 반나절이 단위다.
    /// </summary>
    private static List<MidTermForecastDto> SummarizeHalfDays(
        List<Dictionary<string, string>> forecast, DateTime today)
    {
        var days = new List<MidTermForecastDto>();

        var grouped = forecast
            .Where(f => f.ContainsKey("fcstDate") && f.ContainsKey("fcstTime"))
            .GroupBy(f => f["fcstDate"])
            .OrderBy(g => g.Key, StringComparer.Ordinal);

        foreach (var g in grouped)
        {
            if (!DateTime.TryParseExact(g.Key, "yyyyMMdd", null,
                    System.Globalization.DateTimeStyles.None, out var date))
            {
                continue;
            }

            if (date.Date <= today) continue;

            var rows = g.ToList();

            // 하루가 다 찬 날만 센다. 단기예보 마지막 날은 잘려 와 반나절짜리 하루가 되므로 비워 두고 중기예보가 채운다.
            // 다 찼는지는 TMN·TMX 로 본다 — 하루 한 번씩 실리므로 둘 다 있으면 아침과 오후가 다 온 것이다.
            double? min = FirstValue(rows, "TMN");
            double? max = FirstValue(rows, "TMX");

            if (min is not { } lo || max is not { } hi) continue;

            var am = rows.Where(r => Hour(r) < 12).ToList();
            var pm = rows.Where(r => Hour(r) >= 12).ToList();

            days.Add(new MidTermForecastDto(
                date.ToString("yyyy-MM-dd"),
                WeekLabel(date, today),
                (int)Math.Round(lo),
                (int)Math.Round(hi),
                HalfSky(am),
                HalfSky(pm),
                HalfPop(am),
                HalfPop(pm)));
        }

        return days;
    }

    private static int Hour(Dictionary<string, string> row)
        => row.TryGetValue("fcstTime", out var t) && int.TryParse(t, out var v) ? v / 100 : 0;

    /// <summary>
    /// 반나절을 한 낱말로. <b>비·눈이 하늘 상태를 이긴다</b> —
    /// <see cref="DayCondition"/> 와 같은 규칙이다.
    /// </summary>
    private static string HalfSky(List<Dictionary<string, string>> rows)
    {
        if (rows.Count == 0) return "";

        var ptys = rows
            .Where(r => r.TryGetValue("PTY", out var p) && int.TryParse(p, out _))
            .Select(r => int.Parse(r["PTY"]))
            .Where(p => p > 0)
            .ToList();

        if (ptys.Count > 0)
        {
            if (ptys.Contains(3) || ptys.Contains(7)) return "눈";
            if (ptys.Contains(2) || ptys.Contains(6)) return "비/눈";
            return "비";
        }

        var sky = rows
            .Where(r => r.TryGetValue("SKY", out var s) && int.TryParse(s, out _))
            .Select(r => int.Parse(r["SKY"]))
            .GroupBy(s => s)
            .OrderByDescending(gr => gr.Count())
            .Select(gr => (int?)gr.Key)
            .FirstOrDefault();

        return sky switch
        {
            1 => "맑음",
            3 => "구름많음",
            4 => "흐림",
            _ => "",
        };
    }

    private static int HalfPop(List<Dictionary<string, string>> rows)
        => rows
            .Where(r => r.TryGetValue("POP", out var p) && int.TryParse(p, out _))
            .Select(r => int.Parse(r["POP"]))
            .DefaultIfEmpty(0)
            .Max();

    /// <summary>
    /// 가장 가까운 행정구역 이름을 격자표(ghub.grid_coordinates)에서 찾는다. 역지오코딩은 쓰지 않는다 — 바깥 서비스가 죽으면 날씨 알림이 함께 멎는다.
    /// 위경도가 0 인 행은 뺀다(빠지지 않으면 먼 바다 좌표가 그 빈 줄에 붙는다). 거리는 제곱합으로 비교한다 — 순서만 맞으면 된다.
    /// </summary>
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

            // 읍·면·동이 있는 줄을 먼저 고른다. 시·군·구까지만 적힌 줄이 더 가까울 때 동네 이름이 영영 안 나오기 때문이다.
            var pick = near.FirstOrDefault(g => !string.IsNullOrWhiteSpace(g.Region3)) ?? near[0];

            var region1 = Trimmed(pick.Region1);
            var region2 = Trimmed(pick.Region2);
            var region3 = Trimmed(pick.Region3);

            // 세종시는 1단계와 2단계가 같은 글자라 그대로 이으면 「세종특별자치시 세종특별자치시」가 된다. 두 번째는 뺀다.
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
    /// 읍·면·동 줄을 찾으려고 들춰 보는 줄 수. 늘리면 옆 동네 이름을, 줄이면 구청 자리만 있는 곳에서 동 이름을 놓친다.
    /// </summary>
    private const int NearbyRows = 20;

    private static string? Trimmed(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    /// <summary>
    /// 찾아낸 행정구역 한 줄. <b>이어 붙인 이름과 세 단계를 함께</b> 들고 다닌다 —
    /// 화면은 단계별로 보여 주고 알림 본문은 이어 붙인 것을 쓴다.
    /// </summary>
    private sealed record PlaceName(string Name, string? Region1, string? Region2, string? Region3);

    /// <summary>
    /// 단기예보(시간별)를 하루 단위로 접는다. 알림 한 통에는 시간별 예보가 다 안 실리므로 오늘의 최저·최고와 비 여부만 남긴다.
    /// </summary>
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

                // TMN·TMX 는 하루 한 번만 실린다. 없는 날은 시간별 기온(TMP)에서 뽑는다 — 오늘치는 지난 시각이 빠져 실제 최저보다 높게 나오지만 없는 것보다 낫다.
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
    /// 하루를 한 낱말로. 비·눈이 하늘 상태를 이긴다 — 구름많음이라 적어 두고 비가 오면 알림이 틀린다.
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

    /// <summary>
    /// 주간 예보 칸 이름 — 내일 · 모레 · 3일후. 등록 지역과 같은 말을 쓰려고 LabelFor 와 갈라 둔 것이다.
    /// </summary>
    private static string WeekLabel(DateTime date, DateTime today)
    {
        var diff = (date.Date - today).Days;
        return diff switch
        {
            0 => "오늘",
            1 => "내일",
            2 => "모레",
            _ => $"{diff}일후",
        };
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
    /// 알림 본문. 첫 줄이 지금, 둘째 줄부터가 예보다. 알림창은 접히면 첫 줄만 보이므로 지금 날씨를 맨 앞에 둔다.
    /// </summary>
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
