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
    /// <param name="lat">위도.</param>
    /// <param name="lon">경도.</param>
    /// <param name="ct">중단 신호.</param>
    /// <param name="weekly">
    /// 참이면 <b>주간 예보</b>(<see cref="PointWeatherDto.Weekly"/>)까지 채운다 —
    /// 기상청 왕복이 둘 늘어난다(중기 육상 · 중기 기온). 기본이 거짓인 까닭은
    /// 이 응답을 쓰는 세 자리 중 둘(알림 본문 · 설정 화면 미리보기)이 그것을
    /// 쓰지 않기 때문이다. 늘 채우면 알림이 그만큼 늦게 나간다.
    /// </param>
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

        // **단기예보를 다시 받지 않는다.** 주간 예보의 앞쪽 이틀은 위에서 이미
        // 받아 둔 그 응답에서 갈라 낸다 — 등록 지역의 `mid-term/{id}` 도 같은
        // 식으로 단기와 중기를 이어 붙인다.
        if (weekly) result.Weekly = await BuildWeeklyAsync(forecast, found, ct);

        result.Summary = BuildSummary(result);
        return result;
    }

    /// <summary>
    /// 한 지점의 <b>주간 예보</b> — 내일부터 열흘. 앞쪽은 단기예보, 뒤쪽은 중기예보다.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>중기예보는 격자로 못 묻는다.</b> 기상청이 따로 매긴 구역코드 하나로만
    /// 묻는데, 등록 지역은 그 코드를 사람이 적어 넣어 두었고 한 지점에는 적을
    /// 자리가 없다. 그래서 좌표에서 찾아낸 시·도·시·군으로 코드를 고른다
    /// (<see cref="MidTermRegions"/>). <b>이름을 못 찾았으면 주간 예보도 없다</b> —
    /// 바다 한가운데의 좌표가 그렇고, 그때는 빈 목록이 맞다.
    /// </para>
    /// <para>
    /// <b>중기예보 구역은 시·군 단위다.</b> 단기예보의 5km 격자보다 훨씬 성기고,
    /// 하늘 상태·강수확률은 아예 도(道) 단위다. 화면이 그 사실을 한 줄로 말해야
    /// 사람이 이 숫자를 격자 예보와 같은 것으로 읽지 않는다.
    /// </para>
    /// </remarks>
    private async Task<List<MidTermForecastDto>> BuildWeeklyAsync(
        List<Dictionary<string, string>>? forecast, PlaceName? place, CancellationToken ct)
    {
        var today = Kst.Now.Date;

        // 단기예보에서 **오늘을 뺀** 나머지 날. 오늘은 위의 실황과 사흘 예보가
        // 이미 말했고, 주간 예보 줄의 첫 칸이 「오늘」이면 같은 말이 두 번 선다.
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
    /// 단기예보를 <b>오전·오후로 갈라</b> 하루 한 줄로 접는다 — 중기예보와 같은 모양.
    /// </summary>
    /// <remarks>
    /// 위 <see cref="SummarizeDays"/> 와 접는 단위가 다르다. 저쪽은 알림 한 줄에
    /// 실을 「오늘 몇 도에 비가 오나」라 하루를 통째로 접고, 이쪽은 주간 예보
    /// 카드가 <b>오전 / 오후</b> 두 칸을 그리므로 반나절이 단위다.
    /// </remarks>
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

            // **하루가 다 찬 날만 센다.** 단기예보의 마지막 날은 잘려 온다 —
            // 오전 몇 시간만 실려 오고(기상청이 거기까지만 내거나 numOfRows 에서
            // 잘리거나), 그 몇 시간으로 접으면 「최고 21℃ · 맑음」 같은 <b>반나절
            // 짜리 하루</b>가 나온다. 그 날은 중기예보가 제대로 말해 주므로 여기서
            // 비워 둔다(아래에서 중기가 그 자리를 채운다).
            //
            // 다 찼는지는 <b>TMN·TMX 로 본다</b>. 기상청이 그 둘을 하루에 한 번씩
            // (06시·15시 칸) 싣기 때문에, 둘이 다 있으면 아침과 오후가 다 온 것이다.
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

    /// <summary>
    /// 주간 예보 칸의 이름 — <c>내일</c> · <c>모레</c> · <c>3일후</c>.
    /// </summary>
    /// <remarks>
    /// 위 <see cref="LabelFor"/> 와 갈라 둔 까닭은 <b>등록 지역과 같은 말을 쓰기
    /// 위해서</b>다. 등록 지역의 주간 예보는 사흘 뒤부터 「3일후」로 적는데
    /// (<c>mid-term/{id}</c>), 내 위치만 「10/1」로 적으면 같은 화면의 위아래가
    /// 다른 말을 한다. 사흘 예보 줄은 날짜를 그대로 보여 주므로 저쪽이 <c>M/d</c> 다.
    /// </remarks>
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
