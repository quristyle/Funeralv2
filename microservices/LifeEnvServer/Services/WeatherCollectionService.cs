using System.Text.Json;
using LifeEnvServer.Data;
using LifeEnvServer.Models;
using LifeEnvServer.Utilities;
using Microsoft.EntityFrameworkCore;

namespace LifeEnvServer.Services;

/// <summary>
/// 주기적으로 기상 정보를 수집하는 백그라운드 서비스
/// </summary>
public class WeatherCollectionService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<WeatherCollectionService> _logger;

    // 수집 주기(분): Weather:CollectMinutes (기본 30)
    private readonly TimeSpan _period;

    // 인증키 유무만 기억한다 — 없으면 사이클 전체를 쉰다 (예외로 죽이지 않는다)
    private readonly bool _hasServiceKey;

    // 특보 조회 지점: Weather:WarningStnId (기본 108 = 전국)
    private readonly int _warningStnId;

    public WeatherCollectionService(IServiceScopeFactory scopeFactory, ILogger<WeatherCollectionService> logger, IConfiguration configuration)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
        _period = TimeSpan.FromMinutes(Math.Max(1, configuration.GetValue<int>("Weather:CollectMinutes", 30)));
        _hasServiceKey = WeatherApiService.IsUsableServiceKey(configuration["Weather:ServiceKey"]);
        _warningStnId = configuration.GetValue<int>("Weather:WarningStnId", 108);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Weather Collection Service is starting. (주기: {Minutes}분)", _period.TotalMinutes);

        using var timer = new PeriodicTimer(_period);

        // 시작 시 1회 실행 (또는 타이머 대기 후 실행하려면 순서 변경)
        await RunCycleAsync(stoppingToken);

        while (await timer.WaitForNextTickAsync(stoppingToken)) {
            await RunCycleAsync(stoppingToken);
        }
    }

    // 한 사이클: 실황 → 특보 → 중기 → 초단기 → 단기
    private async Task RunCycleAsync(CancellationToken stoppingToken) {
        if (!_hasServiceKey) {
            _logger.LogWarning("Weather:ServiceKey 가 설정되지 않아 이번 수집 사이클을 건너뜁니다.");
            return;
        }

        await CollectWeatherDataAsync(stoppingToken);
        await CollectWeatherWarningsAsync(stoppingToken);
        await CollectMidTermForecastAsync(stoppingToken);
        await CollectUltraShortTermForecastAsync(stoppingToken);
        await CollectShortTermForecastAsync(stoppingToken);
    }

// 관리지역 기상 정보 수집
  private async Task CollectWeatherDataAsync(CancellationToken stoppingToken) {
    try {
      using var scope = _scopeFactory.CreateScope();
      var db = scope.ServiceProvider.GetRequiredService<LifeEnvDbContext>();
      var weatherApi = scope.ServiceProvider.GetRequiredService<WeatherApiService>();

      // 활성화된 관측 지역 조회
      var locations = await db.WeatherLocations
          .Where(l => l.IsActive && !l.IsDeleted)
          .ToListAsync(stoppingToken);

      _logger.LogInformation("Starting weather collection for {Count} locations.", locations.Count);

      foreach (var loc in locations) {
        try {
          var weatherInfo = await weatherApi.GetRealTimeWeatherAsync(loc);

          if (weatherInfo != null) {
            // 체감온도(SensibleTemp)와 WeatherLocationId 는 GetRealTimeWeatherAsync 가
            // 캐시에 넣기 전에 채워 둔다 — 여기서는 저장 메타만 덧붙인다.

            // DB에 저장 (이력 관리)
            // WeatherApiService에서 생성한 객체는 ID가 0인 새 객체임
            weatherInfo.CreatedBy = "System_Background";
            weatherInfo.CreatedAt = DateTimeOffset.UtcNow;
            weatherInfo.WeatherLocationId = loc.Id;
            weatherInfo.WeatherLocation = null; // 중복 트래킹 에러 방지

            // 명시적으로 관계 설정이 필요하다면 여기서 처리
            db.WeatherInfos.Add(weatherInfo);
            await db.SaveChangesAsync(stoppingToken);

            // 날씨 기준 체크 및 기록
            try {
                var monitoringService = scope.ServiceProvider.GetRequiredService<IWeatherMonitoringService>();
                await monitoringService.CheckWeatherStandardsAsync(weatherInfo);
            }
            catch (Exception ex) {
                _logger.LogError(ex, "Failed to check weather standards for {Name}", loc.Name);
            }
          }
        }
        catch (Exception ex) {
          _logger.LogError(ex, "Failed to collect weather for {Name}", loc.Name);
        }
      }

      // await db.SaveChangesAsync(stoppingToken); // Moved inside loop to ensure monitoring service gets IDs
      _logger.LogInformation("Weather collection completed.");
    }
    catch (Exception ex) {
      _logger.LogError(ex, "Error occurred executing Weather Collection Service.");
    }
  }

  // 초단기 예보 수집 (매시 45분 이후 호출 권장 -> 주기 수집이므로 적절히 커버됨)
  private async Task CollectUltraShortTermForecastAsync(CancellationToken stoppingToken) {
      try {
          using var scope = _scopeFactory.CreateScope();
          var db = scope.ServiceProvider.GetRequiredService<LifeEnvDbContext>();
          var weatherApi = scope.ServiceProvider.GetRequiredService<WeatherApiService>();

          var locations = await db.WeatherLocations
              .Where(l => l.IsActive && !l.IsDeleted)
              .ToListAsync(stoppingToken);

          if (locations.Count == 0) return;

          // BaseTime 계산 (매시 30분 기준, KST)
          var now = Kst.Now;
          var baseTime = now.Minute < 45 ? now.AddHours(-1) : now;
          string dateStr = baseTime.ToString("yyyyMMdd");
          string timeStr = baseTime.ToString("HH") + "30";

          foreach (var loc in locations) {
              try {
                  // 중복 체크
                  bool exists = await db.WeatherUltraSrtForecasts
                      .AnyAsync(f => f.WeatherLocationId == loc.Id && f.BaseDate == dateStr && f.BaseTime == timeStr, stoppingToken);

                  if (exists) continue;

                  var forecastList = await weatherApi.GetUltraSrtForecastListAsync(loc.NX, loc.NY);
                  if (forecastList == null || forecastList.Count == 0) continue;

                  foreach (var item in forecastList) {
                      var fcst = new WeatherUltraSrtForecast {
                          WeatherLocationId = loc.Id,
                          BaseDate = dateStr,
                          BaseTime = timeStr,
                          FcstDate = item.GetValueOrDefault("fcstDate", ""),
                          FcstTime = item.GetValueOrDefault("fcstTime", ""),
                          CreatedAt = DateTimeOffset.UtcNow
                      };

                      if (item.TryGetValue("T1H", out var t1h) && double.TryParse(t1h, out double vT1h)) fcst.T1H = vT1h;
                      if (item.TryGetValue("RN1", out var rn1)) fcst.RN1 = rn1;
                      if (item.TryGetValue("SKY", out var sky) && int.TryParse(sky, out int vSky)) fcst.SKY = vSky;
                      if (item.TryGetValue("UUU", out var uuu) && double.TryParse(uuu, out double vUuu)) fcst.UUU = vUuu;
                      if (item.TryGetValue("VVV", out var vvv) && double.TryParse(vvv, out double vVvv)) fcst.VVV = vVvv;
                      if (item.TryGetValue("REH", out var reh) && int.TryParse(reh, out int vReh)) fcst.REH = vReh;
                      if (item.TryGetValue("PTY", out var pty) && int.TryParse(pty, out int vPty)) fcst.PTY = vPty;
                      if (item.TryGetValue("LGT", out var lgt) && int.TryParse(lgt, out int vLgt)) fcst.LGT = vLgt;
                      if (item.TryGetValue("VEC", out var vec) && double.TryParse(vec, out double vVec)) fcst.VEC = vVec;
                      if (item.TryGetValue("WSD", out var wsd) && double.TryParse(wsd, out double vWsd)) fcst.WSD = vWsd;

                      db.WeatherUltraSrtForecasts.Add(fcst);
                  }

                  await db.SaveChangesAsync(stoppingToken);
                  _logger.LogInformation("Collected Ultra Short Term Forecast for {Name} ({Date} {Time})", loc.Name, dateStr, timeStr);
              }
              catch (Exception ex) {
                  _logger.LogError(ex, "Failed to collect Ultra Short Term Forecast for {Name}", loc.Name);
              }
          }
      }
      catch (Exception ex) {
          _logger.LogError(ex, "Error executing Ultra Short Term Forecast Collection.");
      }
  }

  // 단기 예보 수집 (3시간 간격)
  private async Task CollectShortTermForecastAsync(CancellationToken stoppingToken) {
      try {
          using var scope = _scopeFactory.CreateScope();
          var db = scope.ServiceProvider.GetRequiredService<LifeEnvDbContext>();
          var weatherApi = scope.ServiceProvider.GetRequiredService<WeatherApiService>();

          var locations = await db.WeatherLocations
              .Where(l => l.IsActive && !l.IsDeleted)
              .ToListAsync(stoppingToken);

          if (locations.Count == 0) return;

          // BaseTime 계산 logic (ApiService와 동일하게 맞춤, KST)
          var now = Kst.Now;
          var baseDate = now.ToString("yyyyMMdd");
          int[] baseHours = { 2, 5, 8, 11, 14, 17, 20, 23 };
          int currentHour = now.Hour;
          int baseHour = 2;

          if (currentHour < 2 || (currentHour == 2 && now.Minute < 10)) {
            baseDate = now.AddDays(-1).ToString("yyyyMMdd");
            baseHour = 23;
          } else {
            foreach (var h in baseHours) {
              if (currentHour > h || (currentHour == h && now.Minute >= 10)) {
                baseHour = h;
              }
            }
          }
          string baseTime = baseHour.ToString("D2") + "00";

          foreach (var loc in locations) {
              try {
                  bool exists = await db.WeatherShortTermForecasts
                      .AnyAsync(f => f.WeatherLocationId == loc.Id && f.BaseDate == baseDate && f.BaseTime == baseTime, stoppingToken);

                  if (exists) continue;

                  var forecastList = await weatherApi.GetVilageForecastAsync(loc.NX, loc.NY);
                  if (forecastList == null || forecastList.Count == 0) continue;

                  foreach (var item in forecastList) {
                      var fcst = new WeatherShortTermForecast {
                          WeatherLocationId = loc.Id,
                          BaseDate = baseDate,
                          BaseTime = baseTime,
                          FcstDate = item.GetValueOrDefault("fcstDate", ""),
                          FcstTime = item.GetValueOrDefault("fcstTime", ""),
                          CreatedAt = DateTimeOffset.UtcNow
                      };

                      if (item.TryGetValue("POP", out var pop) && int.TryParse(pop, out int vPop)) fcst.POP = vPop;
                      if (item.TryGetValue("PTY", out var pty) && int.TryParse(pty, out int vPty)) fcst.PTY = vPty;
                      if (item.TryGetValue("PCP", out var pcp)) fcst.PCP = pcp;
                      if (item.TryGetValue("REH", out var reh) && int.TryParse(reh, out int vReh)) fcst.REH = vReh;
                      if (item.TryGetValue("SNO", out var sno)) fcst.SNO = sno;
                      if (item.TryGetValue("SKY", out var sky) && int.TryParse(sky, out int vSky)) fcst.SKY = vSky;
                      if (item.TryGetValue("TMP", out var tmp) && double.TryParse(tmp, out double vTmp)) fcst.TMP = vTmp;
                      if (item.TryGetValue("TMN", out var tmn) && double.TryParse(tmn, out double vTmn)) fcst.TMN = vTmn;
                      if (item.TryGetValue("TMX", out var tmx) && double.TryParse(tmx, out double vTmx)) fcst.TMX = vTmx;
                      if (item.TryGetValue("UUU", out var uuu) && double.TryParse(uuu, out double vUuu)) fcst.UUU = vUuu;
                      if (item.TryGetValue("VVV", out var vvv) && double.TryParse(vvv, out double vVvv)) fcst.VVV = vVvv;
                      if (item.TryGetValue("WAV", out var wav) && double.TryParse(wav, out double vWav)) fcst.WAV = vWav;
                      if (item.TryGetValue("VEC", out var vec) && double.TryParse(vec, out double vVec)) fcst.VEC = vVec;
                      if (item.TryGetValue("WSD", out var wsd) && double.TryParse(wsd, out double vWsd)) fcst.WSD = vWsd;

                      db.WeatherShortTermForecasts.Add(fcst);
                  }

                  await db.SaveChangesAsync(stoppingToken);
                  _logger.LogInformation("Collected Short Term Forecast for {Name} ({Date} {Time})", loc.Name, baseDate, baseTime);
              }
              catch (Exception ex) {
                  _logger.LogError(ex, "Failed to collect Short Term Forecast for {Name}", loc.Name);
              }
          }
      }
      catch (Exception ex) {
          _logger.LogError(ex, "Error executing Short Term Forecast Collection.");
      }
  }

  // 중기 예보 데이터 수집
  private async Task CollectMidTermForecastAsync(CancellationToken stoppingToken) {
      try {
          using var scope = _scopeFactory.CreateScope();
          var db = scope.ServiceProvider.GetRequiredService<LifeEnvDbContext>();
          var weatherApi = scope.ServiceProvider.GetRequiredService<WeatherApiService>();

          // 중기 예보 코드가 설정된 지역만 조회
          var locations = await db.WeatherLocations
              .Where(l => l.IsActive && !l.IsDeleted && !string.IsNullOrEmpty(l.MidTermLandCode) && !string.IsNullOrEmpty(l.MidTermTempCode))
              .ToListAsync(stoppingToken);

          if (locations.Count == 0) return;

          // 중기 예보는 하루 2회(06, 18시 KST) 발표되므로, 최신 발표 시각 계산
          var baseDate = MidTermForecastReader.BaseDate(Kst.Now);

          // 이미 수집된 BaseDate인지 확인 (지역별로 다를 수 있으니 여기선 생략하고, 각 지역 처리 시 중복 체크)

          foreach (var loc in locations) {
              try {
                  // 이미 해당 BaseDate로 수집된 데이터가 있는지 확인
                  bool exists = await db.WeatherMidTermForecasts.AnyAsync(f => f.WeatherLocationId == loc.Id && f.BaseDate == baseDate, stoppingToken);
                  if (exists) continue; // 이미 수집됨

                  var landJson = await weatherApi.GetMidLandForecastAsync(loc.MidTermLandCode!);
                  var tempJson = await weatherApi.GetMidTaAsync(loc.MidTermTempCode!);

                  // 읽는 일은 내 위치 주간 예보와 같은 자리(MidTermForecastReader)를 쓴다 — 한쪽만 고치면 두 화면이 다른 말을 한다.
                  var days = MidTermForecastReader.Read(landJson, tempJson, baseDate);

                  if (days.Count == 0) {
                      // 읽어 낸 날이 하나도 없으면 기상청 미발표, 키 누락, 응답 모양 변경 중 하나다 — 조용히 넘기면 원인을 못 찾는다.
                      _logger.LogWarning("Mid-Term forecast empty for {Name} ({BaseDate})", loc.Name, baseDate);
                      continue;
                  }

                  foreach (var day in days) {
                      db.WeatherMidTermForecasts.Add(new WeatherMidTermForecast {
                          WeatherLocationId = loc.Id,
                          BaseDate = baseDate,
                          ForecastDate = day.Date,
                          DayAfter = day.DayAfter,
                          AmSky = day.AmSky,
                          PmSky = day.PmSky,
                          AmPop = day.AmPop,
                          PmPop = day.PmPop,
                          MinTemp = day.MinTemp,
                          MaxTemp = day.MaxTemp,
                          CreatedAt = DateTimeOffset.UtcNow
                      });
                  }

                  await db.SaveChangesAsync(stoppingToken);
                  _logger.LogInformation("Collected Mid-Term forecast for {Name} ({BaseDate})", loc.Name, baseDate);
              }
              catch (Exception ex) {
                  _logger.LogError(ex, "Failed to collect mid-term forecast for {Name}", loc.Name);
              }
          }
      }
      catch (Exception ex) {
          _logger.LogError(ex, "Error executing Mid-Term Forecast Collection.");
      }
  }


//특보 데이터 수집
  private async Task CollectWeatherWarningsAsync(CancellationToken stoppingToken) {
    try {
      using var scope = _scopeFactory.CreateScope();
      var db = scope.ServiceProvider.GetRequiredService<LifeEnvDbContext>();
      var weatherApi = scope.ServiceProvider.GetRequiredService<WeatherApiService>();

      var warnings = await weatherApi.GetWeatherWarningAsync(_warningStnId); // 기본 108: 전국
      if (warnings == null || warnings.Count == 0) return;

      int count = 0;

      // 특보 행의 Id 가 아직 0 이라 발송 표시를 붙일 수 없으므로, 저장이 끝난 뒤 한꺼번에 알린다.
      var pending = new List<PendingWarningNotice>();

      foreach (var w in warnings) {
        // 해제 명령 중복 방지 로직
        if (!string.IsNullOrEmpty(w.Command) && w.Command.Contains("해제") && !string.IsNullOrEmpty(w.WarningNum)) {
            bool isLiftedAlready = await db.WeatherWarnings
                .AnyAsync(existing => existing.WarningNum == w.WarningNum && existing.Command != null && existing.Command.Contains("해제"), stoppingToken);

            if (isLiftedAlready) continue;
        }

        // 변경 명령 중복 방지 로직 (동일 번호, 동일 발표시각의 변경 명령 존재 시 무시)
        if (!string.IsNullOrEmpty(w.Command) && w.Command.Contains("변경") && !string.IsNullOrEmpty(w.WarningNum)) {
            bool isChangedAlready = await db.WeatherWarnings
                .AnyAsync(existing => existing.WarningNum == w.WarningNum && existing.TmFc == w.TmFc && existing.Command != null && existing.Command.Contains("변경"), stoppingToken);

            if (isChangedAlready) continue;
        }

        // Check if this warning already exists (StnId, TmFc, TmSeq)
        bool exists = await db.WeatherWarnings
            .AnyAsync(existing => existing.StnId == w.StnId && existing.TmFc == w.TmFc && existing.TmSeq == w.TmSeq, stoppingToken);

        if (!exists) {
          var notice = await ProcessWarningAsync(db, weatherApi, w, stoppingToken);
          if (notice.Matches.Count > 0) pending.Add(notice);
          count++;
        }
      }

      if (count > 0) {
        await db.SaveChangesAsync(stoppingToken);
        _logger.LogInformation("Collected {Count} new weather warnings with messages and details.", count);

        // 관리 지역에 걸린 특보만 알린다 — 전국 특보를 다 내보내면 알림이 하루 수십 건이 된다.
        await NotifyMatchedWarningsAsync(scope, db, pending, stoppingToken);
      }
    }
    catch (Exception ex) {
      _logger.LogError(ex, "Failed to collect weather warnings.");
    }
  }

  /// <summary>단일 기상 특보를 처리해 DB 에 저장하고 지역을 매칭한다. 알림 여부는 부르는 쪽이 저장 뒤에 정한다.</summary>
  public async Task<PendingWarningNotice> ProcessWarningAsync(LifeEnvDbContext db, WeatherApiService weatherApi, WeatherWarning w, CancellationToken stoppingToken)
  {
      if (w.Id == 0) {
          // New entity
          w.CreatedBy = string.IsNullOrEmpty(w.CreatedBy) ? "System_Background" : w.CreatedBy;
          db.WeatherWarnings.Add(w);

          // Note: If ID is needed for WeatherLocationWarning foreign key,
          // EF Core handles it if we use the navigation property 'WeatherWarning = w'.
      }

      // Fetch and store Bulletin Message (getWthrWrnMsg)
      WeatherWarningMsg? msg = null;
      try {
        msg = await weatherApi.GetWeatherWarningMsgAsync(w.StnId, w.TmFc, w.TmSeq);
        if (msg != null) {
          msg.CreatedBy = "System_Background";

          // 통보문 문장 분할 파싱
          ParseAndAddSentences(msg);

          // Check duplication before add? Caller ensures uniqueness of Warning.
          // But Msg might be fetched separately? Assuming 1:1 for now.
          db.WeatherWarningMsgs.Add(msg);
        }
      } catch (Exception ex) {
        _logger.LogError(ex, "Failed to fetch warning message for {TmFc}", w.TmFc);
      }

      // Fetch and store Status (getPwnStatus - 구역별 실시간 상태)
      try {
        var statusList = await weatherApi.GetWeatherWarningStatusAsync(w.StnId);
        foreach (var s in statusList) {
          s.CreatedBy = "System_Background";
          db.WeatherWarningStatuses.Add(s);
        }
      } catch (Exception ex) {
        _logger.LogError(ex, "Failed to fetch warning status for {TmFc}", w.TmFc);
      }

      // --- 관리 지역 매칭 로직 (개선됨: 텍스트 파싱 + Zone Hierarchy) ---
      var matches = new List<WeatherLocationWarning>();
      string? summary = null;
      try {
          (matches, summary) = await MatchLocationsByContent(db, w, msg, stoppingToken);
      } catch (Exception ex) {
          _logger.LogError(ex, "Failed to match locations for {TmFc}", w.TmFc);
      }

      return new PendingWarningNotice(w, matches, summary);
  }

  /// <summary>알림을 기다리는 특보 한 건 — 특보 · 새로 맞은 지역 · 특보 종류 요약.</summary>
  public sealed record PendingWarningNotice(
      WeatherWarning Warning, List<WeatherLocationWarning> Matches, string? Summary);

  /// <summary>
  /// 새로 맞은 특보를 알린다. 저장이 끝난 뒤 부른다(매칭 시점에는 특보 Id 가 0). IsNotified 는 발송 시도로 세운다 — 성공일 때만 세우면 장애 뒤 밀린 특보가 한꺼번에 쏟아진다.
  /// 특보 하나에 알림 하나다. 지역마다 보내면 광역 특보 한 건에 알림이 여럿 뜬다.
  /// </summary>
  private async Task NotifyMatchedWarningsAsync(
      IServiceScope scope, LifeEnvDbContext db, List<PendingWarningNotice> pending, CancellationToken stoppingToken)
  {
      if (pending.Count == 0) return;

      var notify = scope.ServiceProvider.GetRequiredService<WeatherNotifyClient>();
      var now = DateTimeOffset.UtcNow;

      foreach (var notice in pending)
      {
          var names = notice.Matches
              .Select(m => m.WeatherLocation?.Name)
              .Where(n => !string.IsNullOrWhiteSpace(n))
              .Select(n => n!)
              .Distinct()
              .ToList();

          try
          {
              await notify.NotifyWarningAsync(
                  notice.Warning.Title,
                  notice.Warning.Command,
                  notice.Warning.WarningNum,
                  names,
                  notice.Summary,
                  notice.Warning.AnnouncementTime);
          }
          catch (Exception ex)
          {
              // NotifyWarningAsync 가 이미 예외를 삼키지만, 한 건이 터져도 남은 특보는
              // 계속 나가야 하므로 여기서 한 번 더 막는다.
              _logger.LogError(ex, "Failed to notify weather warning {TmFc}", notice.Warning.TmFc);
          }

          foreach (var m in notice.Matches)
          {
              m.IsNotified = true;
              m.NotifiedAt = now;
          }
      }

      await db.SaveChangesAsync(stoppingToken);
  }

  /// <summary>특보 본문을 분석해 관리 지역과 매칭한다. 요약은 우리 지역과 맞은 줄의 특보 종류다.</summary>
  private async Task<(List<WeatherLocationWarning> Matches, string? Summary)> MatchLocationsByContent(LifeEnvDbContext db, WeatherWarning warning, WeatherWarningMsg? msg, CancellationToken stoppingToken)
  {
      var matchedLocationIds = new HashSet<int>();

      // 알림 제목의 특보 종류는 우리 지역과 맞은 줄에서만 거둔다 — 안 맞은 줄까지 넣으면 남의 지역 특보를 읊는다.
      var matchedKinds = new List<string>();

      // 2. 통보문 텍스트를 파싱해 매칭한다 (getWthrWrnMsg).
      string contentToParse = msg?.T6 ?? warning.Content ?? "";

      if (!string.IsNullOrWhiteSpace(contentToParse))
      {
          // 특보 구역 마스터 로드 (메모리 캐싱 고려 가능)
          var zones = await db.WeatherWarningZones
              .Where(z => !z.IsDeleted)
              .ToListAsync(stoppingToken);

          // 구역명으로 ID 찾기 위한 룩업 (REG_KO -> REG_ID)
          // "경상북도", "영덕", "서울 동남권" 등
          var nameToIds = zones
              .Where(z => !string.IsNullOrEmpty(z.RegKo))
              .ToLookup(z => z.RegKo!.Trim(), z => z.RegId);

          // ID로 하위 구역 찾기 위한 룩업 (REG_UP -> REG_ID list)
          var parentToChildren = zones
              .Where(z => !string.IsNullOrEmpty(z.RegUp))
              .ToLookup(z => z.RegUp!, z => z.RegId);

          // 'o [특보명] : [지역목록]' 형식을 문자열로 나눠 키워드로 매칭한다.
          var lines = contentToParse.Split('\n');
          foreach (var line in lines)
          {
              if (!line.Trim().StartsWith("o")) continue;
              var parts = line.Split(':');
              if (parts.Length < 2) continue;

              var regionPart = parts[1];
              // 괄호 안의 내용도 지역명이므로 쉼표로 바꿔 읽는다.
              var cleanRegions = regionPart
                  .Replace("(", ",")
                  .Replace(")", ",")
                  .Replace(".", ",")
                  .Split(',')
                  .Select(r => r.Trim())
                  .Where(r => !string.IsNullOrEmpty(r))
                  .ToList();

              var matchedZoneIds = new HashSet<string>();

              foreach (var regionName in cleanRegions)
              {
                  // 2-1. 이름으로 Zone ID 찾기
                  if (nameToIds.Contains(regionName))
                  {
                      foreach (var id in nameToIds[regionName])
                      {
                          matchedZoneIds.Add(id);
                          // 광역명만 뜨면 그 하위 지역까지 매칭한다(AddChildrenRecursive).


                          // 광역명만 뜬 경우(예: 전라남도) 하위 관리 지역을 모두 모은다.
                          AddChildrenRecursive(id, parentToChildren, matchedZoneIds);
                      }
                  }
              }

              // 2-3. 매칭된 Zone ID를 가진 WeatherLocation 찾기
              if (matchedZoneIds.Any())
              {
                  var textMatched = await db.WeatherLocations
                      .Where(l => l.IsActive && !l.IsDeleted && l.WarningAreaCode != null && matchedZoneIds.Contains(l.WarningAreaCode))
                      .Select(l => l.Id)
                      .ToListAsync(stoppingToken);

                  foreach (var id in textMatched) matchedLocationIds.Add(id);

                  if (textMatched.Count > 0)
                  {
                      // 'o 강풍주의보 : 경상북도(영덕…)' 에서 앞머리 글머리표를 떼고 종류만 남긴다.
                      var kind = parts[0].Trim().TrimStart('o', 'O', '\u3147', '\u25cb', '\u00b7', '-', '*').Trim();
                      if (!string.IsNullOrEmpty(kind) && !matchedKinds.Contains(kind)) matchedKinds.Add(kind);
                  }
              }
          }
      }

      // 최종 저장
      var created = new List<WeatherLocationWarning>();
      foreach (var locId in matchedLocationIds)
      {
          // 이미 추가된지 확인 (중복 방지)
          if (warning.Id > 0) {
              var exists = await db.WeatherLocationWarnings
                  .AnyAsync(mw => mw.WeatherWarningId == warning.Id && mw.WeatherLocationId == locId, stoppingToken);
              if (exists) continue;
          }

          var loc = await db.WeatherLocations.FindAsync(new object[] { locId }, stoppingToken);
          if (loc != null) {
              var link = new WeatherLocationWarning
              {
                  // WeatherWarningId = warning.Id, // Do not set ID if 0
                  WeatherWarning = warning,      // Link to parent entity explicitly
                  WeatherLocationId = locId,
                  WeatherLocation = loc,
                  // 발송 표시는 NotifyMatchedWarningsAsync 가 저장 뒤에 세운다.
                  IsNotified = false,
                  CreatedAt = DateTimeOffset.UtcNow,
                  CreatedBy = "System_Background"
              };
              db.WeatherLocationWarnings.Add(link);
              created.Add(link);
              _logger.LogInformation("Weather warning matched for location: {Name} (by Content Analysis)", loc.Name);
          }
      }

      return (created, matchedKinds.Count > 0 ? string.Join(" \u00b7 ", matchedKinds) : null);
  }

  private void AddChildrenRecursive(string currentId, ILookup<string, string> parentToChildren, HashSet<string> result)
  {
      if (parentToChildren.Contains(currentId))
      {
          foreach (var childId in parentToChildren[currentId])
          {
              if (!result.Contains(childId))
              {
                  result.Add(childId);
                  AddChildrenRecursive(childId, parentToChildren, result);
              }
          }
      }
  }

  /// <summary>
  /// 통보문 필드(t1~t7)를 문장 단위로 파싱하여 Sentences 컬렉션에 추가합니다.
  /// </summary>
  private void ParseAndAddSentences(WeatherWarningMsg msg)
  {
      int seq = 1;

      void ParseField(string? text, string fieldType)
      {
          if (string.IsNullOrWhiteSpace(text)) return;

          var lines = text.Split(new[] { '\n' }, StringSplitOptions.RemoveEmptyEntries);
          foreach (var line in lines)
          {
              var trimmed = line.Trim();
              if (string.IsNullOrWhiteSpace(trimmed)) continue;

              string? title = null;
              string content = trimmed;

              // 콜론(:)으로 제목과 내용을 나눈다.
              int colonIndex = trimmed.IndexOf(':');
              if (colonIndex <= 0) continue; // 콜론이 없으면 저장하지 않음

              title = trimmed.Substring(0, colonIndex).Trim();
              content = trimmed.Substring(colonIndex + 1).Trim();

              msg.Sentences.Add(new WeatherWarningMsgSentence
              {
                  FieldType = fieldType,
                  Sequence = seq++,
                  Title = title.Replace("o", "").Trim(),
                  Content = content,
                  CreatedBy = "System_Background",
                  CreatedAt = DateTimeOffset.UtcNow
              });
          }
      }

      ParseField(msg.T1, "t1");
      ParseField(msg.T2, "t2");
      ParseField(msg.T3, "t3");
      ParseField(msg.T4, "t4");
      //ParseField(msg.T5, "t5");
      ParseField(msg.T6, "t6");
      ParseField(msg.T7, "t7");
  }
}
