using LifeEnvServer.Data;
using LifeEnvServer.Dtos;
using LifeEnvServer.Models;
using LifeEnvServer.Services;
using LifeEnvServer.Utilities;
using JSini.Shared.Infrastructure.Filters;
using Microsoft.EntityFrameworkCore;

namespace LifeEnvServer.Endpoints;

/// <summary>
/// 날씨 관련 엔드포인트 (GHUB skgRestApi 이식)
///
/// 원본과 다른 점:
/// - 게이트웨이가 /api/ghub 접두사를 떼고 넘겨 주므로 그룹은 /weather 다.
/// - 인증·감사 필터를 걷어냈다 — 인증은 게이트웨이가 끝냈고(X-User-*), 응답 봉투는
///   공용 <see cref="ApiResponseFilter"/>(AddApiResponseWrapper) 하나만 씌운다.
/// - 시간 규칙: 저장·비교는 UTC(DateTimeOffset), 기상청 발표 문자열을 다룰 때만 Kst 변환.
///   (원본의 DateTime.Now / AddHours(±9) 보정을 전부 <see cref="Kst"/> 로 바꿨다)
/// - 이식하지 않은 원본 엔드포인트: POST /(WeatherInfo 직접 등록 — 호출자 없음),
///   warnings/details · reports · breaking-news · preliminary-warnings(영구 빈 테이블),
///   warnings/debug-fetch(API 키 노출 디버그).
/// </summary>
public static class WeatherEndpoints {
  /// <summary>
  /// 날씨 정보 엔드포인트를 매핑합니다.
  /// </summary>
  /// <param name="app">엔드포인트 라우트 빌더</param>
  public static void MapWeatherEndpoints(this IEndpointRouteBuilder app) {
    var group = app.MapGroup("/weather")
        .WithTags("Weather")
        .AddApiResponseWrapper();

    // 1. 관측 지역 관리 (CRUD)
    var locationGroup = group.MapGroup("/locations");

    locationGroup.MapGet("/", async (LifeEnvDbContext db) => {
      return Results.Ok(await db.WeatherLocations
        .Where(l => !l.IsDeleted)
        .OrderBy(l => l.SortOrder)
        .ToListAsync());
    })
    .WithName("GetWeatherLocations")
    .WithSummary("날씨 관측 지역 목록 조회");

    locationGroup.MapPost("/", async (LifeEnvDbContext db, WeatherLocation location, UserContext? user) => {
      location.CreatedAt = DateTimeOffset.UtcNow;
      location.CreatedBy = user?.UserId ?? "";
      db.WeatherLocations.Add(location);
      await db.SaveChangesAsync();
      return Results.Created($"/weather/locations/{location.Id}", location);
    })
    .WithName("CreateWeatherLocation");

    locationGroup.MapPut("/{id:int}", async (LifeEnvDbContext db, int id, WeatherLocation updated, UserContext? user) => {
      var location = await db.WeatherLocations.FindAsync(id);
      if (location == null || location.IsDeleted) return Results.NotFound();

      location.Name = updated.Name;
      location.NX = updated.NX;
      location.NY = updated.NY;
      location.Region3 = updated.Region3;
      location.Description = updated.Description;
      location.IsActive = updated.IsActive;
      location.SortOrder = updated.SortOrder;
      location.MidTermLandCode = updated.MidTermLandCode;
      location.MidTermTempCode = updated.MidTermTempCode;
      location.WarningAreaCode = updated.WarningAreaCode;
      location.ModifiedAt = DateTimeOffset.UtcNow;
      location.ModifiedBy = user?.UserId ?? "";

      await db.SaveChangesAsync();
      return Results.Ok(location);
    });

    locationGroup.MapDelete("/{id:int}", async (LifeEnvDbContext db, int id, UserContext? user) => {
      var location = await db.WeatherLocations.FindAsync(id);
      if (location == null || location.IsDeleted) return Results.NotFound();

      location.IsDeleted = true;
      location.ModifiedAt = DateTimeOffset.UtcNow;
      location.ModifiedBy = user?.UserId ?? "";

      await db.SaveChangesAsync();
      return Results.NoContent();
    });

    // 관측 지역 순서 변경
    locationGroup.MapPut("/reorder", async (LifeEnvDbContext db, List<WeatherLocationReorderRequest> requests, UserContext? user) => {
      var ids = requests.Select(r => r.Id).ToList();
      var locations = await db.WeatherLocations.Where(l => ids.Contains(l.Id)).ToListAsync();

      foreach (var req in requests) {
        var loc = locations.FirstOrDefault(l => l.Id == req.Id);
        if (loc != null) {
          loc.SortOrder = req.SortOrder;
          loc.ModifiedAt = DateTimeOffset.UtcNow;
          loc.ModifiedBy = user?.UserId ?? "";
        }
      }

      await db.SaveChangesAsync();
      return Results.Ok();
    })
    .WithName("ReorderWeatherLocations")
    .WithSummary("관측 지역 순서 변경");

    // 1.1 좌표 검색
    locationGroup.MapGet("/search-grid", (string? query, WeatherApiService weatherApi) => {
      return Results.Ok(weatherApi.SearchGridCoordinates(query ?? ""));
    })
    .WithName("SearchGridCoordinates")
    .WithSummary("격자 좌표 검색 (시도/시군구 기준)");

    // 2. 실시간 날씨 조회 (대시보드용)
    group.MapGet("/", async (LifeEnvDbContext db, WeatherApiService weatherApi) => {
      var locations = await db.WeatherLocations
        .Where(l => l.IsActive && !l.IsDeleted)
        .OrderBy(l => l.SortOrder)
        .ToListAsync();

      var list = new List<WeatherInfo>();
      var now = DateTimeOffset.UtcNow;

      foreach (var loc in locations) {
        // 1. DB에서 최신 데이터 조회
        var latestInfo = await db.WeatherInfos
            .Where(w => w.WeatherLocationId == loc.Id)
            .OrderByDescending(w => w.ObservationTime)
            .FirstOrDefaultAsync();

        bool needFetch = true;

        if (latestInfo != null) {
            // 20분 차이 체크
            if ((now - latestInfo.ObservationTime).TotalMinutes < 20) {
                list.Add(latestInfo);
                needFetch = false;
            }
        }

        // 2. 오래되었거나 없으면 API 호출
        if (needFetch) {
            var weather = await weatherApi.GetRealTimeWeatherAsync(loc);
            if (weather != null) {
                weather.WeatherLocationId = loc.Id;
                // 수집은 WeatherCollectionService 몫이지만, 여기서 새로 받아 왔으면
                // DB 상태도 함께 갱신해 둔다 (출처 표기).
                weather.CreatedBy = "System_API_Fallback";
                db.WeatherInfos.Add(weather);
                list.Add(weather);
            } else if (latestInfo != null) {
                // API 실패 시 오래된 데이터라도 반환 (안정성 위해)
                list.Add(latestInfo);
            }
        }
      }

      await db.SaveChangesAsync(); // 새로 받아 온 데이터 저장

      return Results.Ok(list);
    })
    .WithName("GetWeatherInfos")
    .WithSummary("최신 날씨 정보 조회 (DB 우선, 20분 경과 시 API 갱신)");

    // 2.1 특정 지역 실시간 날씨 조회 (단건)
    group.MapGet("/current/{locationId:int}", async (LifeEnvDbContext db, WeatherApiService weatherApi, int locationId) => {
      var loc = await db.WeatherLocations.FindAsync(locationId);
      if (loc == null || loc.IsDeleted) return Results.NotFound("Location not found");

      var now = DateTimeOffset.UtcNow;

      // 어제 동시간대(±40분) 기온을 DB 에서 찾아 주는 지역 함수
      async Task<double?> FindYesterdayTempAsync() {
          var yesterday = now.AddDays(-1);
          var from = yesterday.AddMinutes(-40);
          var to = yesterday.AddMinutes(40);

          var candidates = await db.WeatherInfos
              .Where(w => w.WeatherLocationId == locationId && w.ObservationTime >= from && w.ObservationTime <= to)
              .Select(w => new { w.ObservationTime, w.TemperatureC })
              .ToListAsync();

          // 시각 차이의 절대값은 DB 로 번역이 안 될 수 있어 메모리에서 가장 가까운 것을 고른다
          var bestMatch = candidates.OrderBy(w => Math.Abs((w.ObservationTime - yesterday).TotalMinutes)).FirstOrDefault();
          return bestMatch?.TemperatureC;
      }

      // 1. DB에서 최신 데이터 조회
      var latestInfo = await db.WeatherInfos
          .Where(w => w.WeatherLocationId == locationId)
          .OrderByDescending(w => w.ObservationTime)
          .FirstOrDefaultAsync();

      // 2. 유효성 체크 (20분 이내)
      if (latestInfo != null && (now - latestInfo.ObservationTime).TotalMinutes < 20) {
          // 어제 동시간대 기온 조회 (DB 값에 이미 있을 수 있지만, 만약 null이면 채워줌)
          if (latestInfo.YesterdayTemperature == null) {
              latestInfo.YesterdayTemperature = await FindYesterdayTempAsync();
          }
          return Results.Ok(latestInfo);
      }

      // 3. API 호출 (갱신 필요)
      var weather = await weatherApi.GetRealTimeWeatherAsync(loc);
      if (weather != null) {
          weather.WeatherLocationId = loc.Id;
          weather.CreatedBy = "System_API_Fallback"; // 출처 표기

          // WeatherApiService 내에서 YesterdayTemperature를 이미 채우려 시도하지만,
          // NX/NY 기준이며 LocationId 기준이 아닐 수 있고, 시점 차이가 있을 수 있음.
          // 확실하게 하기 위해 여기서도 비어있으면 채움.
          weather.YesterdayTemperature ??= await FindYesterdayTempAsync();

          db.WeatherInfos.Add(weather);
          await db.SaveChangesAsync();

          return Results.Ok(weather);
      }

      // 4. API 실패 시 기존 데이터라도 반환
      if (latestInfo != null) {
          return Results.Ok(latestInfo);
      }

      return Results.NotFound("Weather data unavailable");
    })
    .WithName("GetCurrentWeather")
    .WithSummary("특정 지역 실시간 날씨 조회 (DB 우선, 20분 경과 시 API 갱신)");

    // 3. 날씨 이력 조회 (차트용)
    group.MapGet("/history", async (LifeEnvDbContext db, string? location, int? days) => {
      var query = db.WeatherInfos.AsQueryable();

      if (!string.IsNullOrEmpty(location)) {
        query = query.Where(w => w.Location == location);
      }

      var startDate = DateTimeOffset.UtcNow.AddDays(-(days ?? 1)); // 기본 1일
      query = query.Where(w => w.ObservationTime >= startDate);

      var history = await query
          .OrderBy(w => w.ObservationTime)
          .ToListAsync();

      return Results.Ok(history);
    })
    .WithName("GetWeatherHistory")
    .WithSummary("날씨 이력 조회 (기간별)");

    // 3.1 특정 시간대 과거 기온 조회 (차트용)
    group.MapGet("/history/hourly", async (LifeEnvDbContext db, int locationId, int hour, int? days) => {
        var limitDays = days ?? 7;
        var startDate = DateTimeOffset.UtcNow.AddDays(-(limitDays + 1)); // 넉넉하게 하루 더 조회

        var query = db.WeatherInfos
            .Where(w => w.WeatherLocationId == locationId && w.ObservationTime >= startDate)
            .OrderBy(w => w.ObservationTime);

        // 메모리 내 필터링 (데이터 양이 적음)
        var allData = await query.Select(w => new { w.ObservationTime, w.TemperatureC }).ToListAsync();

        var filtered = allData
            .Select(w => new {
                LocalTime = Kst.FromUtc(w.ObservationTime),
                w.TemperatureC
            })
            .Where(x => x.LocalTime.Hour == hour)
            // 하루에 같은 시간 데이터가 중복될 경우(거의 없겠지만) 평균 사용
            .GroupBy(x => x.LocalTime.Date)
            .Select(g => new {
                Date = g.Key.ToString("yyyy-MM-dd"),
                Temp = Math.Round(g.Average(x => x.TemperatureC), 1)
            })
            .OrderBy(x => x.Date)
            .ToList();

        return Results.Ok(filtered);
    })
    .WithName("GetHourlyWeatherHistory")
    .WithSummary("특정 시간대(KST)의 과거 기온 이력 조회");

    // 4. 단기 예보 조회 (이력 + 예보 병합)
    group.MapGet("/forecast/{locationId:int}", async (LifeEnvDbContext db, int locationId) => {
      var loc = await db.WeatherLocations.FindAsync(locationId);
      if (loc == null) return Results.NotFound("Location not found");

      // 원본은 TimeUtil.DatetimeOfDay(DateTime.Now) — KST 오늘 0시와 같다
      var currentHourKst = Kst.Now.Date;

      // KST 기준 21시간 창: [-10h ... 기준 ... +10h]
      // (원본도 상한(+10h)은 선언만 하고 강제하지 않았다 — 하한만 쓴다)
      var startKst = currentHourKst.AddHours(-10);

      // 1. 이력 조회 — KST 벽시계를 UTC 로 환산해 DB(UTC)와 비교한다
      var utcThreshold = Kst.ToUtc(startKst);

      var historyEntities = await db.WeatherInfos
          .Where(w => w.WeatherLocationId == locationId && w.ObservationTime >= utcThreshold)
          .OrderBy(w => w.ObservationTime)
          .ToListAsync();

      // 이력을 시간당 1건으로 (각 시간대의 마지막 관측)
      var hourlyHistory = historyEntities
          .GroupBy(h => new {
            Date = Kst.FromUtc(h.ObservationTime).Date,
            Hour = Kst.FromUtc(h.ObservationTime).Hour
          })
          .Select(g => g.Last()) // 각 그룹의 최신 것
          .OrderBy(h => h.ObservationTime)
          .ToList();

      var timeline = new List<WeatherTimelineDto>();

      // 이력 → 타임라인
      foreach (var h in hourlyHistory) {
        var kstTime = Kst.FromUtc(h.ObservationTime);
        // 정시로 정규화
        var kstHour = new DateTime(kstTime.Year, kstTime.Month, kstTime.Day, kstTime.Hour, 0, 0);

        timeline.Add(new WeatherTimelineDto(
            kstHour.ToString("yyyyMMdd"),
            kstHour.ToString("HH") + "00",
            h.TemperatureC,
            null, // 이력에는 강수확률이 없다
            h.Rainfall ?? 0,
            h.Condition, // 텍스트
            h.PTY?.ToString() ?? "0", // PTY 코드
            h.WindSpeed ?? 0,
            h.WindDirection ?? 0,
            h.Humidity,
            h.UUU,
            h.VVV,
            h.Snowfall,
            false // 이력
        ));
      }

      // 2. DB 에서 예보 조회 (단기 + 초단기)

      var forecastMap = new Dictionary<string, WeatherTimelineDto>();

      // "1.0mm", "강수없음" 같은 문자열 수치 파싱 도우미
      double ParseStrVal(string? val) {
          if (string.IsNullOrEmpty(val)) return 0;
          if (val == "강수없음" || val == "적설없음" || val == "null") return 0;
          string num = val.Replace("mm", "").Replace("cm", "").Trim();
          if (double.TryParse(num, out double d)) return d;
          return 0;
      }

      // 2-1. 단기 예보 (Short Term) - 최신본 조회
      var latestShort = await db.WeatherShortTermForecasts
          .Where(f => f.WeatherLocationId == locationId)
          .OrderByDescending(f => f.BaseDate).ThenByDescending(f => f.BaseTime)
          .Select(f => new { f.BaseDate, f.BaseTime })
          .FirstOrDefaultAsync();

      if (latestShort != null) {
          var shortForecasts = await db.WeatherShortTermForecasts
              .Where(f => f.WeatherLocationId == locationId &&
                          f.BaseDate == latestShort.BaseDate &&
                          f.BaseTime == latestShort.BaseTime)
              .ToListAsync();

          foreach (var s in shortForecasts) {
              string key = s.FcstDate + s.FcstTime;
              forecastMap[key] = new WeatherTimelineDto(
                  s.FcstDate,
                  s.FcstTime,
                  s.TMP ?? 0,
                  s.POP,
                  ParseStrVal(s.PCP),
                  s.SKY?.ToString() ?? "1",
                  s.PTY?.ToString() ?? "0",
                  s.WSD ?? 0,
                  s.VEC ?? 0,
                  s.REH ?? 0,
                  s.UUU ?? 0,
                  s.VVV ?? 0,
                  ParseStrVal(s.SNO),
                  true // 예보
              );
          }
      }

      // 2-2. 초단기 예보 (Ultra Short Term) - 최신본 조회 (덮어쓰기)
      var latestUltra = await db.WeatherUltraSrtForecasts
          .Where(f => f.WeatherLocationId == locationId)
          .OrderByDescending(f => f.BaseDate).ThenByDescending(f => f.BaseTime)
          .Select(f => new { f.BaseDate, f.BaseTime })
          .FirstOrDefaultAsync();

      if (latestUltra != null) {
          var ultraForecasts = await db.WeatherUltraSrtForecasts
              .Where(f => f.WeatherLocationId == locationId &&
                          f.BaseDate == latestUltra.BaseDate &&
                          f.BaseTime == latestUltra.BaseTime)
              .ToListAsync();

          foreach (var u in ultraForecasts) {
              string key = u.FcstDate + u.FcstTime;
              int? pop = null;
              double sno = 0;

              // 기존 단기예보 데이터가 있으면 POP, SNO 유지
              if (forecastMap.ContainsKey(key)) {
                  pop = forecastMap[key].Pop;
                  // WeatherTimelineDto의 적설량 필드는 'Sno' 입니다.
                  sno = forecastMap[key].Sno ?? 0;
              }

              forecastMap[key] = new WeatherTimelineDto(
                  u.FcstDate,
                  u.FcstTime,
                  u.T1H ?? 0,
                  pop, // POP 유지 (초단기엔 없음)
                  ParseStrVal(u.RN1),
                  u.SKY?.ToString() ?? "1",
                  u.PTY?.ToString() ?? "0",
                  u.WSD ?? 0,
                  u.VEC ?? 0,
                  u.REH ?? 0,
                  u.UUU ?? 0,
                  u.VVV ?? 0,
                  sno, // SNO 유지 (초단기엔 없음)
                  true
              );
          }
      }

      // 3. 예보 → 타임라인 병합
      var sortedForecasts = forecastMap.Values.OrderBy(x => x.Date).ThenBy(x => x.Time).ToList();

      foreach (var f in sortedForecasts) {
          var fcstDateTime = DateTime.ParseExact(f.Date + f.Time, "yyyyMMddHHmm", null);
          // 예보 KST 시각을 UTC 로 환산해 이력 ObservationTime 과 비교
          var fcstUtc = Kst.ToUtc(fcstDateTime);

          // 미래(이력에 없는 시각)만 추가
          bool isFuture = true;
          if (historyEntities.Count > 0) {
            var lastHistory = historyEntities.Last().ObservationTime;
            // 마지막 이력보다 과거의 예보는 제외
            if (fcstUtc <= lastHistory) isFuture = false;
          }

          // 같은 시각 슬롯이 이미 이력으로 들어가 있으면 제외
          if (timeline.Any(t => t.Date == f.Date && t.Time == f.Time)) isFuture = false;

          if (isFuture) {
            timeline.Add(f);
          }
      }

      return Results.Ok(timeline);
    })
    .WithName("GetWeatherForecast")
    .WithSummary("단기 예보 조회 (이력 + 예보 병합 - DB기반)");

    // 5. 중기 예보 조회 (주간 날씨 - DB 조회)
    group.MapGet("/mid-term/{locationId:int}", async (LifeEnvDbContext db, int locationId) => {
      var loc = await db.WeatherLocations.FindAsync(locationId);
      if (loc == null) return Results.NotFound("Location not found");

      // 최신 BaseDate 계산 (0600, 1800) — 기상청 발표 차수는 KST 기준
      var nowKst = Kst.Now;
      string baseDate = "";
      if (nowKst.Hour < 6) baseDate = nowKst.AddDays(-1).ToString("yyyyMMdd") + "1800";
      else if (nowKst.Hour < 18) baseDate = nowKst.ToString("yyyyMMdd") + "0600";
      else baseDate = nowKst.ToString("yyyyMMdd") + "1800";

      var forecasts = await db.WeatherMidTermForecasts
          .Where(f => f.WeatherLocationId == locationId && f.BaseDate == baseDate)
          .OrderBy(f => f.ForecastDate)
          .ToListAsync();

      // 만약 최신 데이터가 아직 수집되지 않았다면, 이전 차수 데이터 조회 (12시간 전)
      if (forecasts.Count == 0) {
          var prevBaseDate = DateTime.ParseExact(baseDate, "yyyyMMddHHmm", null).AddHours(-12).ToString("yyyyMMddHHmm");
          forecasts = await db.WeatherMidTermForecasts
              .Where(f => f.WeatherLocationId == locationId && f.BaseDate == prevBaseDate)
              .OrderBy(f => f.ForecastDate)
              .ToListAsync();
      }

      var list = forecasts.Select(f => new MidTermForecastDto(
          f.ForecastDate.ToString("yyyy-MM-dd"),
          $"{f.DayAfter}일후",
          f.MinTemp,
          f.MaxTemp,
          f.AmSky,
          f.PmSky,
          f.AmPop,
          f.PmPop
      )).ToList();

      // --- 단기 예보 병합 (오늘, 내일, 모레) ---
      var todayStr = nowKst.ToString("yyyyMMdd");

      var latestShort = await db.WeatherShortTermForecasts
          .Where(f => f.WeatherLocationId == locationId)
          .OrderByDescending(f => f.BaseDate).ThenByDescending(f => f.BaseTime)
          .Select(f => new { f.BaseDate, f.BaseTime })
          .FirstOrDefaultAsync();

      var shortTermDtos = new List<MidTermForecastDto>();

      if (latestShort != null) {
          var shorts = await db.WeatherShortTermForecasts
              .Where(f => f.WeatherLocationId == locationId &&
                          f.BaseDate == latestShort.BaseDate &&
                          f.BaseTime == latestShort.BaseTime)
              .ToListAsync();

          var grouped = shorts
              .GroupBy(s => s.FcstDate)
              .Where(g => string.Compare(g.Key, todayStr) > 0) // 오늘 이후
              .OrderBy(g => g.Key);

          foreach (var g in grouped) {
              var dateStr = g.Key;
              if (!DateTime.TryParseExact(dateStr, "yyyyMMdd", null, System.Globalization.DateTimeStyles.None, out DateTime dateObj)) continue;

              int dDay = (dateObj.Date - nowKst.Date).Days;
              string dayAfterStr = dDay == 0 ? "오늘" : dDay == 1 ? "내일" : dDay == 2 ? "모레" : $"{dDay}일후";

              var temps = g.Where(x => x.TMP.HasValue).Select(x => x.TMP!.Value).ToList();
              int minTemp = temps.Any() ? (int)Math.Round(temps.Min()) : 0;
              int maxTemp = temps.Any() ? (int)Math.Round(temps.Max()) : 0;

              var amData = g.Where(x => int.Parse(x.FcstTime) < 1200).ToList();
              var pmData = g.Where(x => int.Parse(x.FcstTime) >= 1200).ToList();

              int amPop = amData.Any(x => x.POP.HasValue) ? amData.Max(x => x.POP ?? 0) : 0;
              int pmPop = pmData.Any(x => x.POP.HasValue) ? pmData.Max(x => x.POP ?? 0) : 0;

              string GetSky(List<WeatherShortTermForecast> items) {
                  if (!items.Any()) return "";
                  var skyMode = items.Where(x => x.SKY.HasValue)
                      .GroupBy(x => x.SKY)
                      .OrderByDescending(gr => gr.Count())
                      .FirstOrDefault()?.Key;

                  return skyMode switch {
                      1 => "맑음",
                      3 => "구름많음",
                      4 => "흐림",
                      _ => "맑음"
                  };
              }

              shortTermDtos.Add(new MidTermForecastDto(
                  dateObj.ToString("yyyy-MM-dd"),
                  dayAfterStr,
                  minTemp,
                  maxTemp,
                  GetSky(amData),
                  GetSky(pmData),
                  amPop,
                  pmPop
              ));
          }
      }

      // 단기 예보 날짜 제외하고 중기 예보 병합
      var shortDates = shortTermDtos.Select(x => x.Date).ToHashSet();
      var final = shortTermDtos.Concat(list.Where(x => !shortDates.Contains(x.Date))).ToList();

      return Results.Ok(final);
    })
    .WithName("GetMidTermForecast")
    .WithSummary("중기 예보 조회 (주간 날씨)");

    // 6. 기상 특보 조회
    group.MapGet("/warnings", async (LifeEnvDbContext db, bool? all) => {
      var now = DateTimeOffset.UtcNow;
      var lookback = now.AddDays(-7); // 그룹핑을 위해 최근 7일 데이터 조회
      var liftedCutoff = now.AddHours(-10); // 10시간 이내 해제건 필터용

      var allRecent = await db.WeatherWarnings
          .Where(w => w.AnnouncementTime >= lookback)
          .OrderByDescending(w => w.AnnouncementTime)
          .ToListAsync();

      if (all == true)
      {
          var warnings = allRecent;
          var warningIds = warnings.Select(w => w.Id).ToList();

          // 1. Matched Locations 로딩
          var matches = await db.WeatherLocationWarnings
              .Where(mw => warningIds.Contains(mw.WeatherWarningId))
              .Include(mw => mw.WeatherLocation)
              .ToListAsync();

          // 2. Sentences 로딩 (최근 7일 데이터 기준)
          // TmFc 는 KST 발표 시각 문자열이라 비교 문자열도 KST 로 만든다
          var lookbackTmFc = Kst.FromUtc(lookback).ToString("yyyyMMddHHmm");
          var msgs = await db.WeatherWarningMsgs
              .Where(m => m.TmFc.CompareTo(lookbackTmFc) >= 0 || m.CreatedAt >= lookback)
              .Include(m => m.Sentences)
              .ToListAsync();

          foreach (var w in warnings)
          {
              w.MatchedLocations = matches
                  .Where(m => m.WeatherWarningId == w.Id && m.WeatherLocation != null)
                  .Select(m => m.WeatherLocation!)
                  .ToList();

              var msg = msgs.FirstOrDefault(m => m.StnId == w.StnId && m.TmFc == w.TmFc && m.TmSeq == w.TmSeq);
              if (msg != null)
              {
                  w.Sentences = msg.Sentences.ToList();
              }
          }

          return Results.Ok(warnings);
      }

      // WarningNum으로 그룹화.
      // 원본은 WarningNum 이 null 이면 Guid.NewGuid() 로 키를 만들어 호출마다 결과가
      // 달라졌다 — 발표 식별자 (StnId, TmFc, TmSeq) 문자열 키로 대체한다.
      var grouped = allRecent.GroupBy(w => string.IsNullOrEmpty(w.WarningNum)
          ? $"{w.StnId}|{w.TmFc}|{w.TmSeq}"
          : w.WarningNum);

      var resultList = new List<WeatherWarning>();

      foreach (var g in grouped) {
          var items = g.OrderByDescending(x => x.AnnouncementTime).ToList();
          // "해제"를 포함하는 항목이 있는지 확인
          var liftItem = items.FirstOrDefault(x => x.Command != null && x.Command.Contains("해제"));
          bool isLifted = liftItem != null;

          if (isLifted) {
              // 해제된 경우: 해제 시각이 10시간 이내인 경우만 포함
              if (liftItem!.AnnouncementTime >= liftedCutoff) {
                  resultList.Add(liftItem);
              }
          } else {
              // 발효 중인 경우 (해제 항목이 없음): 최신 발령/변경 항목 추가
              resultList.Add(items.First());
          }
      }

      var finalResult = resultList.OrderByDescending(r => r.AnnouncementTime).Take(20).ToList();
      return Results.Ok(finalResult);
    })
    .WithName("GetWeatherWarnings")
    .WithSummary("기상 특보 조회 (all=true: 전체 이력, false/null: 발효 중 + 10시간 내 해제 요약)");


    group.MapGet("/warnings4location", async (LifeEnvDbContext db) => {
      // 1. WarningAreaCode가 있는 활성 관리 지역 조회
      var locationCodes = await db.WeatherLocations
          .Where(l => l.IsActive && !l.IsDeleted && !string.IsNullOrEmpty(l.WarningAreaCode))
          .Select(l => l.WarningAreaCode!)
          .Distinct()
          .ToListAsync();

      if (!locationCodes.Any()) return Results.Ok(new List<object>());

      // 2. 해당 코드의 WeatherWarningZone 정보 조회 (키워드 추출용)
      var zones = await db.WeatherWarningZones
          .Where(z => locationCodes.Contains(z.RegId))
          .Select(z => new { z.RegKo, z.RegName })
          .ToListAsync();

      // 3. 키워드 목록 생성 (RegKo + RegName 공백 분리)
      var keywords = new HashSet<string>();
      foreach (var z in zones) {
        if (!string.IsNullOrEmpty(z.RegKo)) keywords.Add(z.RegKo);
        if (!string.IsNullOrEmpty(z.RegName)) {
            var parts = z.RegName.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            foreach (var part in parts) keywords.Add(part);
        }
      }

      // 키워드가 없으면 빈 목록 반환
      if (!keywords.Any()) return Results.Ok(new List<object>());

      // 4. 오늘의 특보 메시지 조회 (KST 기준)
      var todayStr = Kst.Now.ToString("yyyyMMdd");

      // 오늘 발표된 특보의 메시지 문장들 조회
      var sentences = await db.WeatherWarningMsgSentences
          .Include(s => s.WeatherWarningMsg)
          .Where(s => s.WeatherWarningMsg!.TmFc.StartsWith(todayStr))
          .ToListAsync();

      // Command 조인용 특보 사전 로딩 — 원본은 그룹마다 db.WeatherWarnings.FirstOrDefault
      // 를 동기 호출하는 N+1 이었다. 오늘 발표분(TmFc 가 오늘로 시작)만 한 번에 가져온다.
      var todayWarnings = await db.WeatherWarnings
          .Where(w => w.TmFc.StartsWith(todayStr))
          .ToListAsync();

      // 5. 키워드 포함 여부 필터링 (메모리 내 수행)
      // 문장 내용에 키워드가 하나라도 포함되어 있는지 확인
      var filteredSentences = sentences
          .Where(s => keywords.Any(k => s.Content.Contains(k)))
          .GroupBy(s => s.Title)
          .Select(g => {
              var latest = g.OrderByDescending(s => s.WeatherWarningMsg!.TmFc).First();
              // Command 정보를 찾기 위해 WeatherWarning과 조인 (사전 로딩분 사용)
              var warning = todayWarnings
                  .FirstOrDefault(w => w.StnId == latest.WeatherWarningMsg!.StnId &&
                                     w.TmFc == latest.WeatherWarningMsg!.TmFc &&
                                     w.TmSeq == latest.WeatherWarningMsg!.TmSeq);

              // 클라이언트 요청에 맞춰 DTO 를 따로 정의하지 않고 익명 타입으로 내려 준다
              return new {
                  latest.Id,
                  latest.Title,
                  latest.Content,
                  latest.Sequence,
                  Command = warning?.Command,
                  WeatherWarningMsg = new {
                      latest.WeatherWarningMsg!.TmFc
                  }
              };
          })
          .OrderByDescending(s => s.WeatherWarningMsg.TmFc)
          .ToList();

      return Results.Ok(filteredSentences);
    })
    .WithName("GetWeatherWarnings4Location")
    .WithSummary("오늘의 특보 중 관리지역(WarningAreaCode) 관련 문장(RegKo, RegName 포함) 조회");

    group.MapGet("/warnings4location-range", async (LifeEnvDbContext db) => {
      // 1. 관리 지역 코드 및 키워드 추출 (warnings4location과 동일)
      var locationCodes = await db.WeatherLocations
          .Where(l => l.IsActive && !l.IsDeleted && !string.IsNullOrEmpty(l.WarningAreaCode))
          .Select(l => l.WarningAreaCode!)
          .Distinct()
          .ToListAsync();

      if (!locationCodes.Any()) return Results.Ok(new List<object>());

      var zones = await db.WeatherWarningZones
          .Where(z => locationCodes.Contains(z.RegId))
          .Select(z => new { z.RegKo, z.RegName })
          .ToListAsync();

      var keywords = new HashSet<string>();
      foreach (var z in zones) {
        if (!string.IsNullOrEmpty(z.RegKo)) keywords.Add(z.RegKo);
        if (!string.IsNullOrEmpty(z.RegName)) {
            var parts = z.RegName.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            foreach (var part in parts) keywords.Add(part);
        }
      }

      if (!keywords.Any()) return Results.Ok(new List<object>());

      // 2. 오늘의 특보 메시지 조회 (KST 기준)
      var todayStr = Kst.Now.ToString("yyyyMMdd");

      var sentences = await db.WeatherWarningMsgSentences
          .Include(s => s.WeatherWarningMsg)
          .Where(s => s.WeatherWarningMsg!.TmFc.StartsWith(todayStr))
          .ToListAsync();

      // Command 조인용 특보 사전 로딩 (원본의 항목별 동기 N+1 제거)
      var todayWarnings = await db.WeatherWarnings
          .Where(w => w.TmFc.StartsWith(todayStr))
          .ToListAsync();

      // 3. 키워드 필터링 및 시작/끝 데이터 추출
      var result = sentences
          .Where(s => keywords.Any(k => s.Content.Contains(k)))
          .GroupBy(s => s.Title)
          .SelectMany(g => {
              var ordered = g.OrderBy(s => s.WeatherWarningMsg!.TmFc).ToList();
              var first = ordered.First();
              var last = ordered.Last();

              var items = new List<WeatherWarningMsgSentence> { first };
              if (first.Id != last.Id) {
                  items.Add(last);
              }

              return items.Select(item => {
                  var warning = todayWarnings
                      .FirstOrDefault(w => w.StnId == item.WeatherWarningMsg!.StnId &&
                                         w.TmFc == item.WeatherWarningMsg!.TmFc &&
                                         w.TmSeq == item.WeatherWarningMsg!.TmSeq);
                  return new {
                      item.Id,
                      item.Title,
                      item.Content,
                      item.Sequence,
                      Command = warning?.Command,
                      WeatherWarningMsg = new {
                          item.WeatherWarningMsg!.TmFc
                      }
                  };
              });
          })
          .OrderBy(s => s.WeatherWarningMsg.TmFc)
          .ToList();

      return Results.Ok(result);
    })
    .WithName("GetWeatherWarnings4LocationRange")
    .WithSummary("오늘의 특보 중 관리지역 관련 시작 및 마지막 문장 조회");

    // 6.0 기상 특보 단건 조회
    group.MapGet("/warnings/{id:int}", async (LifeEnvDbContext db, int id) => {
      var warning = await db.WeatherWarnings.FindAsync(id);
      return warning != null ? Results.Ok(warning) : Results.NotFound();
    })
    .WithName("GetWeatherWarning")
    .WithSummary("기상 특보 단건 조회");

    // 6.0.1 기상 특보 통합 상세 조회 (단일 호출용)
    group.MapGet("/warnings/{id:int}/full", async (LifeEnvDbContext db, int id) => {
        var warning = await db.WeatherWarnings.FindAsync(id);
        if (warning == null) return Results.NotFound();

        var msg = await db.WeatherWarningMsgs
            .Include(m => m.Sentences)
            .FirstOrDefaultAsync(m => m.StnId == warning.StnId && m.TmFc == warning.TmFc && m.TmSeq == warning.TmSeq);

        var matchedLocations = await db.WeatherLocationWarnings
            .Where(mw => mw.WeatherWarningId == id)
            .Include(mw => mw.WeatherLocation)
            .Select(mw => mw.WeatherLocation!)
            .ToListAsync();

        // 매칭된 지역들의 WarningAreaCode 수집
        var areaCodes = matchedLocations
            .Where(l => !string.IsNullOrEmpty(l.WarningAreaCode))
            .Select(l => l.WarningAreaCode!)
            .Distinct()
            .ToList();

        // 해당 코드에 매핑되는 WeatherWarningZone 조회
        var relatedZones = new List<WeatherWarningZone>();
        if (areaCodes.Count > 0) {
            relatedZones = await db.WeatherWarningZones
                .Where(z => areaCodes.Contains(z.RegId))
                .ToListAsync();
        }

        // Details 는 원본에서도 영구 빈 테이블이라 이식하지 않았다 — 항상 빈 목록
        return Results.Ok(new WeatherWarningFullDetailsDto(warning, msg, new List<object>(), matchedLocations, relatedZones, msg?.Sentences.ToList() ?? new List<WeatherWarningMsgSentence>()));
    })
    .WithName("GetWeatherWarningFullDetails")
    .WithSummary("기상 특보 통합 상세 조회 (기본+통보문+매칭지역+관련특보구역)");

    // 6.1 기상 특보 통보문 조회
    group.MapGet("/warnings/msg", async (LifeEnvDbContext db, int stnId, string tmFc, int tmSeq) => {
      var msg = await db.WeatherWarningMsgs
          .FirstOrDefaultAsync(i => i.StnId == stnId && i.TmFc == tmFc && i.TmSeq == tmSeq);

      return msg != null ? Results.Ok(msg) : Results.Ok(null);
    })
    .WithName("GetWeatherWarningMsg")
    .WithSummary("기상 특보 통보문 조회");

    // 6.2.1 특정 특보에 영향을 받는 관리 지역 조회
    group.MapGet("/warnings/{id:int}/locations", async (LifeEnvDbContext db, int id) => {
        var matched = await db.WeatherLocationWarnings
            .Include(mw => mw.WeatherLocation)
            .Where(mw => mw.WeatherWarningId == id)
            .Select(mw => mw.WeatherLocation)
            .ToListAsync();
        return Results.Ok(matched);
    })
    .WithName("GetMatchedLocationsForWarning");

    // 6.2.2 관리 지역별 특보 이력 조회
    group.MapGet("/locations/warning-history", async (LifeEnvDbContext db, int? locationId) => {
        var locations = await db.WeatherLocations
            .Where(l => l.IsActive && !l.IsDeleted)
            .OrderBy(l => l.SortOrder)
            .ToListAsync();

        var query = db.WeatherLocationWarnings
            .Include(mw => mw.WeatherWarning)
            .Where(mw => !mw.IsDeleted);

        if (locationId.HasValue)
        {
            query = query.Where(mw => mw.WeatherLocationId == locationId.Value);
        }

        var history = await query
            .OrderByDescending(mw => mw.CreatedAt)
            .Take(2000)
            .ToListAsync();

        return Results.Ok(new {
            locations = locations,
            history = history
        });
    })
    .WithName("GetLocationWarningHistory");

    // 6.5.1 특보 구역 목록 조회
    group.MapGet("/warning-zones", async (LifeEnvDbContext db) => {
      return Results.Ok(await db.WeatherWarningZones
          .Where(z => !z.IsDeleted)
          .OrderBy(z => z.RegId)
          .ToListAsync());
    })
    .WithName("GetWeatherWarningZones")
    .WithSummary("기상 특보 구역 목록 조회");
  }
}
