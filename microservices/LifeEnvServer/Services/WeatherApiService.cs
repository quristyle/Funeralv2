using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Web;
using GhubServer.Data;
using GhubServer.Models;
using GhubServer.Utilities;
using Microsoft.Extensions.Caching.Memory;

namespace GhubServer.Services;

/// <summary>
/// 기상청 API 연동 서비스
/// </summary>
public class WeatherApiService {
  private readonly HttpClient _httpClient;
  private readonly IMemoryCache _cache;
  private readonly ILogger<WeatherApiService> _logger;
  private readonly GhubDbContext _context;

  // 공공데이터포털 인증키. 설정(Weather:ServiceKey)에서 생성 시 한 번만 읽는다.
  // 키가 없으면 각 호출 메서드가 경고 로그만 남기고 null/빈 결과를 돌려준다 — 예외로 죽이지 않는다.
  private readonly string? _serviceKey;
  private readonly string _encodedKey;

  /// <summary>
  /// WeatherApiService 생성자
  /// </summary>
  public WeatherApiService(HttpClient httpClient, IConfiguration configuration, IMemoryCache cache, ILogger<WeatherApiService> logger, GhubDbContext context) {
    _httpClient = httpClient;
    _cache = cache;
    _logger = logger;
    _context = context;

    var key = configuration["Weather:ServiceKey"];
    _serviceKey = IsUsableServiceKey(key) ? key : null;
    _encodedKey = _serviceKey != null ? HttpUtility.UrlEncode(_serviceKey) : string.Empty;
  }

  /// <summary>
  /// 인증키가 쓸 수 있는 값인지. 추적 파일의 자리표시자(__SET_IN_...)는 미설정으로 본다.
  /// </summary>
  public static bool IsUsableServiceKey(string? key) =>
    !string.IsNullOrWhiteSpace(key) && !key.StartsWith("__");

  /// <summary>인증키가 설정돼 있는지</summary>
  public bool HasServiceKey => _serviceKey != null;

  /// <summary>키가 없으면 경고를 남기고 true 를 돌려준다 (호출 메서드는 빈 결과로 반환)</summary>
  private bool KeyMissing(string apiName) {
    if (HasServiceKey) return false;
    _logger.LogWarning("Weather:ServiceKey 가 설정되지 않아 {Api} 호출을 건너뜁니다.", apiName);
    return true;
  }

  /// <summary>
  /// 특정 위치의 날씨 정보를 기상청 API에서 가져와 반환합니다.
  /// </summary>
  public async Task<WeatherInfo?> GetRealTimeWeatherAsync(WeatherLocation loc) {
    // 이름은 중복될 수 있으므로 캐시 키는 위치 ID 기준이다
    string cacheKey = $"Weather_{loc.Id}";
    // 케쉬에 있으면 캐쉬에서 가져와서 제공.
    if (_cache.TryGetValue(cacheKey, out WeatherInfo? cachedWeather)) {
      return cachedWeather;
    }

    if (KeyMissing("초단기 실황(getUltraSrtNcst)")) return null;

    int nx = loc.NX;
    int ny = loc.NY;
    string locationName = loc.Name;
    int wid = loc.Id;

    try {
      // 기상청 base_date/base_time 은 KST 기준이다
      var now = Kst.Now;
      var baseTime = now.Minute < 45 ? now.AddHours(-1) : now;
      string dateStr = baseTime.ToString("yyyyMMdd");
      string timeStr = baseTime.ToString("HH") + "00";

      string url = $"http://apis.data.go.kr/1360000/VilageFcstInfoService_2.0/getUltraSrtNcst?serviceKey={_encodedKey}&pageNo=1&numOfRows=10&dataType=JSON&base_date={dateStr}&base_time={timeStr}&nx={nx}&ny={ny}";

      var response = await _httpClient.GetAsync(url);
      if (!response.IsSuccessStatusCode) return null;

      var json = await response.Content.ReadFromJsonAsync<JsonElement>();
      var items = json.GetProperty("response").GetProperty("body").GetProperty("items").GetProperty("item");

      var weather = new WeatherInfo {
        Location = locationName,
        ObservationTime = DateTimeOffset.UtcNow,
        CreatedAt = DateTimeOffset.UtcNow,
        CreatedBy = "System_API",
        NX = nx,
        NY = ny
      };

      string ptyValue = "0";
      foreach (var item in items.EnumerateArray()) {
        string category = item.GetProperty("category").GetString() ?? "";
        var obsrProp = item.GetProperty("obsrValue");
        string obsValue = obsrProp.ValueKind == JsonValueKind.String ? (obsrProp.GetString() ?? "0") : obsrProp.GetRawText();

        switch (category) {
          case "T1H": // 기온
            weather.TemperatureC = double.Parse(obsValue);
            break;
          case "REH": // 습도
            weather.Humidity = (int)double.Parse(obsValue);
            break;
          case "WSD": // 풍속
            weather.WindSpeed = double.Parse(obsValue);
            break;
          case "RN1": // 1시간 강수량
            weather.Rainfall = double.Parse(obsValue);
            break;
          case "VEC": // 풍향
            weather.WindDirection = double.Parse(obsValue);
            break;
          case "PTY": // 강수형태
            ptyValue = obsValue;
            if (int.TryParse(obsValue, out int pty)) weather.PTY = pty;
            weather.Condition = GetConditionFromPty(obsValue);
            break;
          case "UUU": // 동서 바람
            weather.UUU = double.Parse(obsValue);
            break;
          case "VVV": // 남북 바람
            weather.VVV = double.Parse(obsValue);
            break;
        }
      }

      if (string.IsNullOrEmpty(weather.Condition)) {
        // PTY가 0일 경우, 초단기 예보(Forecast)의 SKY 정보를 조회하여 날씨 상태 설정
        try {
          var forecast = await GetUltraSrtForecastAsync(locationName, nx, ny);
          if (forecast != null && forecast.ContainsKey("SKY")) {
            string sky = forecast["SKY"];
            weather.Condition = sky switch {
              "1" => "맑음",
              "3" => "구름많음",
              "4" => "흐림",
              _ => "맑음"
            };
          }
          else {
            weather.Condition = "맑음";
          }
        }
        catch {
          weather.Condition = "맑음";
        }
      }

      // 눈 관련 기상(비/눈, 눈, 빗방울눈날림, 눈날림)일 경우 신적설 정보(SNO) 조회
      if (ptyValue == "2" || ptyValue == "3" || ptyValue == "6" || ptyValue == "7") {
        try {
          var vilageFcst = await GetVilageForecastAsync(nx, ny);
          if (vilageFcst != null && vilageFcst.Count > 0) {
            var first = vilageFcst.FirstOrDefault();
            if (first != null && first.TryGetValue("SNO", out string? snoStr) && !string.IsNullOrEmpty(snoStr)) {
              if (double.TryParse(snoStr, out double sno)) {
                weather.Snowfall = sno;
              }
            }
          }
        }
        catch (Exception ex) {
          _logger.LogWarning(ex, "신적설 정보 조회 중 오류 발생");
        }
      }

      // --- 어제 동시간대 기온 조회 ---
      try {
          var yesterday = DateTimeOffset.UtcNow.AddDays(-1);
          var from = yesterday.AddMinutes(-40);
          var to = yesterday.AddMinutes(40);

          // EF Core 번역 호환성을 위해 범위 내 데이터를 먼저 가져온 후 메모리에서 정렬
          var candidates = _context.WeatherInfos
              .Where(w => w.WeatherLocationId == wid && w.ObservationTime >= from && w.ObservationTime <= to)
              .ToList();

          var yesterdayInfo = candidates
              .OrderBy(w => Math.Abs((w.ObservationTime - yesterday).TotalMinutes))
              .FirstOrDefault();

          if (yesterdayInfo != null) {
              weather.YesterdayTemperature = yesterdayInfo.TemperatureC;
          }
      }
      catch (Exception ex) {
           _logger.LogWarning(ex, "어제 날씨 데이터 조회 실패");
      }
      // -------------------------------------

      // 캐시로 재사용되는 객체이므로 넣기 전에 위치 ID 와 체감온도를 채워 둔다
      weather.WeatherLocationId = wid;
      weather.SensibleTemp = CalculateSensibleTemp(weather.TemperatureC, weather.Humidity, weather.WindSpeed);

      _cache.Set(cacheKey, weather, TimeSpan.FromMinutes(10));
      return weather;
    }
    catch (Exception ex) {
      _logger.LogError(ex, "기상청 API 호출 중 오류 발생");
      return null;
    }
  }

  /// <summary>
  /// 초단기 예보 조회 (SKY, PTY 등)
  /// </summary>
  public async Task<Dictionary<string, string>?> GetUltraSrtForecastAsync(string locationName, int nx, int ny) {
    if (KeyMissing("초단기 예보(getUltraSrtFcst)")) return null;

    var now = Kst.Now;
    var baseTime = now.Minute < 45 ? now.AddHours(-1) : now;
    string dateStr = baseTime.ToString("yyyyMMdd");
    string timeStr = baseTime.ToString("HH") + "30"; // 초단기예보는 매시 30분 기준 생성 (API 제공은 45분)

    string url = $"http://apis.data.go.kr/1360000/VilageFcstInfoService_2.0/getUltraSrtFcst?serviceKey={_encodedKey}&pageNo=1&numOfRows=60&dataType=JSON&base_date={dateStr}&base_time={timeStr}&nx={nx}&ny={ny}";

    try {
      var response = await _httpClient.GetAsync(url);
      if (!response.IsSuccessStatusCode) return null;

      var json = await response.Content.ReadFromJsonAsync<JsonElement>();
      var items = json.GetProperty("response").GetProperty("body").GetProperty("items").GetProperty("item");

      // 가장 빠른 예측 시간의 데이터를 가져옴
      var firstItem = items.EnumerateArray().FirstOrDefault();
      var firstTimeProp = firstItem.GetProperty("fcstTime");
      var firstTime = firstTimeProp.ValueKind == JsonValueKind.String ? firstTimeProp.GetString() : firstTimeProp.GetRawText();

      var result = new Dictionary<string, string>();

      foreach (var item in items.EnumerateArray()) {
        var fcstTimeProp = item.GetProperty("fcstTime");
        var fcstTimeCurrent = fcstTimeProp.ValueKind == JsonValueKind.String ? fcstTimeProp.GetString() : fcstTimeProp.GetRawText();

        if (fcstTimeCurrent == firstTime) {
          string category = item.GetProperty("category").GetString() ?? "";
          var fcstValueProp = item.GetProperty("fcstValue");
          string fcstValue = fcstValueProp.ValueKind == JsonValueKind.String ? (fcstValueProp.GetString() ?? "") : fcstValueProp.GetRawText();
          result[category] = fcstValue;
        }
      }
      return result;
    }
    catch (Exception ex) {
      _logger.LogError(ex, "초단기 예보 API 호출 중 오류");
      return null;
    }
  }

  /// <summary>
  /// 초단기 예보 리스트 조회 (전체 시간대 반환)
  /// </summary>
  public async Task<List<Dictionary<string, string>>?> GetUltraSrtForecastListAsync(int nx, int ny) {
    if (KeyMissing("초단기 예보 리스트(getUltraSrtFcst)")) return null;

    var now = Kst.Now;
    var baseTime = now.Minute < 45 ? now.AddHours(-1) : now;
    string dateStr = baseTime.ToString("yyyyMMdd");
    string timeStr = baseTime.ToString("HH") + "30";

    string url = $"http://apis.data.go.kr/1360000/VilageFcstInfoService_2.0/getUltraSrtFcst?serviceKey={_encodedKey}&pageNo=1&numOfRows=60&dataType=JSON&base_date={dateStr}&base_time={timeStr}&nx={nx}&ny={ny}";

    try {
      var response = await _httpClient.GetAsync(url);
      if (!response.IsSuccessStatusCode) return null;

      var json = await response.Content.ReadFromJsonAsync<JsonElement>();
      var items = json.GetProperty("response").GetProperty("body").GetProperty("items").GetProperty("item");

      var grouped = new Dictionary<string, Dictionary<string, string>>();

      foreach (var item in items.EnumerateArray()) {
        var fcstDateProp = item.GetProperty("fcstDate");
        var fcstTimeProp = item.GetProperty("fcstTime");
        string fcstDate = fcstDateProp.ValueKind == JsonValueKind.String ? (fcstDateProp.GetString() ?? "") : fcstDateProp.GetRawText();
        string fcstTime = fcstTimeProp.ValueKind == JsonValueKind.String ? (fcstTimeProp.GetString() ?? "") : fcstTimeProp.GetRawText();

        string category = item.GetProperty("category").GetString() ?? "";

        var fcstValueProp = item.GetProperty("fcstValue");
        string fcstValue = fcstValueProp.ValueKind == JsonValueKind.String ? (fcstValueProp.GetString() ?? "") : fcstValueProp.GetRawText();

        string key = $"{fcstDate}{fcstTime}";
        if (!grouped.ContainsKey(key)) {
            grouped[key] = new Dictionary<string, string> {
                { "fcstDate", fcstDate },
                { "fcstTime", fcstTime }
            };
        }
        grouped[key][category] = fcstValue;
      }

      return grouped.Values
        .OrderBy(x => x["fcstDate"])
        .ThenBy(x => x["fcstTime"])
        .ToList();
    }
    catch (Exception ex) {
      _logger.LogError(ex, "초단기 예보 리스트 API 호출 중 오류");
      return null;
    }
  }

  /// <summary>
  /// 중기 육상 예보 조회 (3일~10일 후 날씨)
  /// </summary>
  public async Task<string?> GetMidLandForecastAsync(string regId) {
    if (KeyMissing("중기 육상 예보(getMidLandFcst)")) return null;

    string tmFc = GetMidTermTmFc();
    string url = $"http://apis.data.go.kr/1360000/MidFcstInfoService/getMidLandFcst?serviceKey={_encodedKey}&pageNo=1&numOfRows=10&dataType=JSON&regId={regId}&tmFc={tmFc}";

    try {
      var response = await _httpClient.GetAsync(url);
      if (!response.IsSuccessStatusCode) return null;
      return await response.Content.ReadAsStringAsync();
    }
    catch (Exception ex) {
      _logger.LogError(ex, "중기 육상 예보 API 호출 중 오류");
      return null;
    }
  }

  /// <summary>
  /// 중기 기온 예보 조회 (3일~10일 후 기온)
  /// </summary>
  public async Task<string?> GetMidTaAsync(string regId) {
    if (KeyMissing("중기 기온 예보(getMidTa)")) return null;

    string tmFc = GetMidTermTmFc();
    string url = $"http://apis.data.go.kr/1360000/MidFcstInfoService/getMidTa?serviceKey={_encodedKey}&pageNo=1&numOfRows=10&dataType=JSON&regId={regId}&tmFc={tmFc}";

    try {
      var response = await _httpClient.GetAsync(url);
      if (!response.IsSuccessStatusCode) return null;
      return await response.Content.ReadAsStringAsync();
    }
    catch (Exception ex) {
      _logger.LogError(ex, "중기 기온 예보 API 호출 중 오류");
      return null;
    }
  }

  /// <summary>
  /// 단기 예보 조회 (3시간 단위 등, API 2.0은 1시간 단위 제공 가능)
  /// </summary>
  public async Task<List<Dictionary<string, string>>?> GetVilageForecastAsync(int nx, int ny) {
    if (KeyMissing("단기 예보(getVilageFcst)")) return null;

    // 단기예보는 0200, 0500, ... 2300 (3시간 간격) 생성
    // API 제공 시간은 +10분
    var now = Kst.Now;
    var baseDate = now.ToString("yyyyMMdd");

    // 가장 최근의 BaseTime 찾기 (02, 05, 08, 11, 14, 17, 20, 23)
    int[] baseHours = { 2, 5, 8, 11, 14, 17, 20, 23 };
    int currentHour = now.Hour;
    int baseHour = 2; // Default

    // 현재 시간이 02:10 이전이면 전날 23:00 사용
    if (currentHour < 2 || (currentHour == 2 && now.Minute < 10)) {
      baseDate = now.AddDays(-1).ToString("yyyyMMdd");
      baseHour = 23;
    }
    else {
      // 현재 시간보다 작거나 같은 것 중 가장 큰 것 찾기 (단, 10분 마진 고려)
      foreach (var h in baseHours) {
        if (currentHour > h || (currentHour == h && now.Minute >= 10)) {
          baseHour = h;
        }
      }
    }

    string baseTime = baseHour.ToString("D2") + "00";
    string url = $"http://apis.data.go.kr/1360000/VilageFcstInfoService_2.0/getVilageFcst?serviceKey={_encodedKey}&pageNo=1&numOfRows=1000&dataType=JSON&base_date={baseDate}&base_time={baseTime}&nx={nx}&ny={ny}";

    try {
      var response = await _httpClient.GetAsync(url);
      if (!response.IsSuccessStatusCode) return null;

      var json = await response.Content.ReadFromJsonAsync<JsonElement>();
      var items = json.GetProperty("response").GetProperty("body").GetProperty("items").GetProperty("item");

      // Group by fcstDate + fcstTime
      var grouped = new Dictionary<string, Dictionary<string, string>>();

      foreach (var item in items.EnumerateArray()) {
        var fcstDateProp = item.GetProperty("fcstDate");
        var fcstTimeProp = item.GetProperty("fcstTime");
        string fcstDate = fcstDateProp.ValueKind == JsonValueKind.String ? (fcstDateProp.GetString() ?? "") : fcstDateProp.GetRawText();
        string fcstTime = fcstTimeProp.ValueKind == JsonValueKind.String ? (fcstTimeProp.GetString() ?? "") : fcstTimeProp.GetRawText();

        string category = item.GetProperty("category").GetString() ?? "";

        var fcstValueProp = item.GetProperty("fcstValue");
        string fcstValue = fcstValueProp.ValueKind == JsonValueKind.String ? (fcstValueProp.GetString() ?? "") : fcstValueProp.GetRawText();

        string key = $"{fcstDate}{fcstTime}";
        if (!grouped.ContainsKey(key)) {
          grouped[key] = new Dictionary<string, string> {
                { "fcstDate", fcstDate },
                { "fcstTime", fcstTime }
            };
        }
        grouped[key][category] = fcstValue;
      }

      return grouped.Values
        .OrderBy(x => x.ContainsKey("fcstDate") ? x["fcstDate"] : string.Empty)
        .ThenBy(x => x.ContainsKey("fcstTime") ? x["fcstTime"] : string.Empty)
        .ToList();

    }
    catch (Exception ex) {
      _logger.LogError(ex, "단기 예보 API 호출 중 오류");
      return null;
    }
  }

  private string GetMidTermTmFc() {
    var now = Kst.Now;
    // 06:00, 18:00 발표 (KST)
    if (now.Hour < 6) {
      return now.AddDays(-1).ToString("yyyyMMdd") + "1800";
    }
    else if (now.Hour < 18) {
      return now.ToString("yyyyMMdd") + "0600";
    }
    else {
      return now.ToString("yyyyMMdd") + "1800";
    }
  }

  private string GetConditionFromPty(string pty) {
    return pty switch {
      "1" => "비",
      "2" => "비/눈",
      "3" => "눈",
      "5" => "빗방울",
      "6" => "빗방울눈날림",
      "7" => "눈날림",
      _ => "" // 빈 문자열 반환 -> Forecast SKY 사용
    };
  }

  /// <summary>
  /// 기온, 습도, 풍속을 이용하여 체감온도를 계산합니다.
  /// (수집 서비스와 실시간 조회가 함께 쓰므로 여기 한 곳에만 둔다)
  /// </summary>
  public static double? CalculateSensibleTemp(double t, int? rh, double? v)
  {
      double? sensibleTemp = t; // 기본값은 실제 기온

      // 1. 여름철 체감온도 (Heat Index, 열지수)
      // 기온 27℃ 이상 + 습도 존재 시 적용
      if (t >= 22 && rh.HasValue) // if (t >= 27 && rh.HasValue)
      {
          double r = rh.Value;
          sensibleTemp = -8.784695
                         + 1.61139411 * t
                         + 2.338549 * r
                         - 0.14611605 * t * r
                         - 0.012308094 * Math.Pow(t, 2)
                         - 0.016424828 * Math.Pow(r, 2)
                         + 0.002211732 * Math.Pow(t, 2) * r
                         + 0.00072546 * t * Math.Pow(r, 2)
                         - 0.000003582 * Math.Pow(t, 2) * Math.Pow(r, 2);
      }
      // 2. 겨울철 체감온도 (Wind Chill, 풍속 포함)
      // 기온 10℃ 이하 + 풍속 1.3m/s 이상 시 적용
      else if (t <= 15 && v.HasValue && v.Value >= 1.0) //else if (t <= 10 && v.HasValue && v.Value >= 1.3) //
      {
          double vKmH = v.Value * 3.6; // m/s -> km/h 변환
          sensibleTemp = 13.12
                         + 0.6215 * t
                         - 11.37 * Math.Pow(vKmH, 0.16)
                         + 0.3965 * t * Math.Pow(vKmH, 0.16);
      }

      return sensibleTemp.HasValue ? Math.Round(sensibleTemp.Value, 1) : (double?)null;
  }

  /// <summary>
  /// 지역명으로 기상청 격자 좌표를 검색합니다.
  /// </summary>
  public List<GridCoordinate> SearchGridCoordinates(string query) {
    if (string.IsNullOrWhiteSpace(query)) return new List<GridCoordinate>();

    return _context.GridCoordinates.Where(w =>
      (w.Region1 != null && w.Region1.Contains(query)) ||
      (w.Region2 != null && w.Region2.Contains(query)) ||
      (w.Region3 != null && w.Region3.Contains(query)))
    .ToList();
  }

  /// <summary>
  /// 기상 특보 조회 (특보 현황 getWthrWrnList )
  /// </summary>
  public async Task<List<WeatherWarning>> GetWeatherWarningAsync(int stnId = 108) {
    if (KeyMissing("기상 특보(getWthrWrnList)")) return new List<WeatherWarning>();

    var now = Kst.Now;
    string fromDate = now.AddDays(-2).ToString("yyyyMMdd"); // 최근 2일

    string url = $"http://apis.data.go.kr/1360000/WthrWrnInfoService/getWthrWrnList?serviceKey={_encodedKey}&pageNo=1&numOfRows=10&dataType=JSON&stnId={stnId}&fromDate={fromDate}";

    try {
      var response = await _httpClient.GetAsync(url);
      if (!response.IsSuccessStatusCode) return new List<WeatherWarning>();

      var jsonStr = await response.Content.ReadAsStringAsync();
      using var doc = JsonDocument.Parse(jsonStr);
      var root = doc.RootElement;

      // Check Response Code
      if (root.TryGetProperty("response", out var res) &&
          res.TryGetProperty("header", out var header) &&
          header.TryGetProperty("resultCode", out var code)) {

        string codeStr = code.ValueKind == JsonValueKind.String ? (code.GetString() ?? "") : code.GetRawText();
        if (codeStr != "00" && codeStr != "0") return new List<WeatherWarning>();
      }

      var itemsNode = root.GetProperty("response").GetProperty("body").GetProperty("items");
      // items가 빈 문자열("")로 올 경우 데이터 없음
      if (itemsNode.ValueKind == JsonValueKind.String) return new List<WeatherWarning>();

      var items = itemsNode.GetProperty("item");
      var result = new List<WeatherWarning>();

      foreach (var item in items.EnumerateArray()) {
        int stnIdValue = 0;
        var stnIdProp = item.GetProperty("stnId");
        if (stnIdProp.ValueKind == JsonValueKind.Number) {
            stnIdValue = stnIdProp.GetInt32();
        } else {
            int.TryParse(stnIdProp.GetString(), out stnIdValue);
        }

        string tmFcValue = "";
        if (item.TryGetProperty("tmFc", out var tmFcProp)) {
            tmFcValue = tmFcProp.ValueKind == JsonValueKind.Number ? tmFcProp.GetRawText() : (tmFcProp.GetString() ?? "");
        }

        int tmSeqValue = 0;
        if (item.TryGetProperty("tmSeq", out var tmSeqProp)) {
            if (tmSeqProp.ValueKind == JsonValueKind.Number) {
                tmSeqValue = tmSeqProp.GetInt32();
            } else {
                int.TryParse(tmSeqProp.GetString(), out tmSeqValue);
            }
        }

        string title = item.TryGetProperty("title", out var t1) ? (t1.GetString() ?? "") : (item.TryGetProperty("t1", out var t1_alt) ? (t1_alt.GetString() ?? "") : "");
        string content = item.TryGetProperty("t2", out var t2) ? (t2.GetString() ?? "") : "";

        // --- Improved Parsing Logic ---
        string? warningNum = null;
        DateTimeOffset? announcementTime = null;
        string command = "발표";
        string cleanTitle = title;

        // 1. Extract Warning Number (e.g., 제01-199호)
        var numMatch = Regex.Match(title, @"제\s*\d+-\d+\s*호");
        if (numMatch.Success) {
            warningNum = numMatch.Value;
        }

        // 2. Extract Actual Announcement Time from string (e.g., 2026.01.17.00:00)
        //    통보문에 적힌 시각은 KST 벽시계다 — Kst.ToUtc 로 환산해 UTC 로 저장한다
        var timeMatch = Regex.Match(title, @"\d{4}\.\d{2}\.\d{2}\.\d{2}:\d{2}");
        if (timeMatch.Success) {
            if (DateTime.TryParseExact(timeMatch.Value, "yyyy.MM.dd.HH:mm", null, DateTimeStyles.None, out var actualTime)) {
                announcementTime = Kst.ToUtc(actualTime);
            }
        }

        // Fallback to API tmFc if string parsing failed
        if (announcementTime == null && DateTime.TryParseExact(tmFcValue, "yyyyMMddHHmm", null, DateTimeStyles.None, out var parsedTime)) {
            announcementTime = Kst.ToUtc(parsedTime);
        }

        // 3. Extract Core Title (content after '/')
        if (title.Contains("/")) {
            var parts = title.Split('/');
            if (parts.Length > 1) {
                cleanTitle = parts[1].Trim();
            }
        }

        // 4. Determine Command (Status) and Clean Title
        if (cleanTitle.Contains("해제")) command = "해제";
        else if (cleanTitle.Contains("대체")) command = "대체";
        else if (cleanTitle.Contains("변경")) command = "변경";
        else if (cleanTitle.Contains("발표")) command = "발표";

        // Remove status keywords and special symbols from Title
        cleanTitle = Regex.Replace(cleanTitle, @"(발표|해제|변경|대체|\(\*\))", "").Trim();

        result.Add(new WeatherWarning {
          TmFc = tmFcValue,
          StnId = stnIdValue,
          TmSeq = tmSeqValue,
          Title = cleanTitle,
          Content = content,
          Other = item.TryGetProperty("other", out var o) ? (o.ValueKind == JsonValueKind.String ? o.GetString() : o.GetRawText()) : null,
          AnnouncementTime = announcementTime,
          WarningNum = warningNum,
          Command = command,
          CreatedAt = DateTimeOffset.UtcNow,
          CreatedBy = "System_API"
        });
      }

      return result;
    }
    catch (Exception ex) {
      _logger.LogError(ex, "기상 특보 API 호출 오류");
      return new List<WeatherWarning>();
    }
  }

  /// <summary>
  /// 기상 특보 통보문 조회 (getWthrWrnMsg)
  /// </summary>
  public async Task<WeatherWarningMsg?> GetWeatherWarningMsgAsync(int stnId, string tmFc, int tmSeq) {
    if (KeyMissing("특보 통보문(getWthrWrnMsg)")) return null;

    string url = $"http://apis.data.go.kr/1360000/WthrWrnInfoService/getWthrWrnMsg?serviceKey={_encodedKey}&pageNo=1&numOfRows=1&dataType=JSON&stnId={stnId}&tmFc={tmFc}&tmSeq={tmSeq}";

    try {
      var response = await _httpClient.GetAsync(url);
      if (!response.IsSuccessStatusCode) return null;

      var jsonStr = await response.Content.ReadAsStringAsync();
      using var doc = JsonDocument.Parse(jsonStr);
      var root = doc.RootElement;

      var itemsNode = root.GetProperty("response").GetProperty("body").GetProperty("items");
      if (itemsNode.ValueKind == JsonValueKind.String) return null;

      var item = itemsNode.GetProperty("item").EnumerateArray().FirstOrDefault();
      if (item.ValueKind == JsonValueKind.Undefined) return null;

      string GetVal(JsonElement el, string prop) {
        if (!el.TryGetProperty(prop, out var p)) return "";
        return p.ValueKind == JsonValueKind.Number ? p.GetRawText() : (p.GetString() ?? "");
      }

      return new WeatherWarningMsg {
        TmFc = tmFc,
        StnId = stnId,
        TmSeq = tmSeq,
        Title = GetVal(item, "t1"),
        T1 = GetVal(item, "t1"),
        T2 = GetVal(item, "t2"),
        T3 = GetVal(item, "t3"),
        T4 = GetVal(item, "t4"),
        T5 = GetVal(item, "t5"),
        T6 = GetVal(item, "t6"),
        T7 = GetVal(item, "t7"),
        Other = GetVal(item, "other"),
        WarFc = GetVal(item, "warFc"),
        CreatedAt = DateTimeOffset.UtcNow,
        CreatedBy = "System_API"
      };
    } catch (Exception ex) {
      _logger.LogError(ex, "특보 통보문 조회 오류: stnId={StnId}, tmFc={TmFc}, tmSeq={TmSeq}", stnId, tmFc, tmSeq);
      return null;
    }
  }

  /// <summary>
  /// 기상 특보 상세 현황 조회 (getPwnStatus)
  /// </summary>
  public async Task<List<WeatherWarningStatus>> GetWeatherWarningStatusAsync(int stnId = 108) {
    if (KeyMissing("특보 현황(getPwnStatus)")) return new List<WeatherWarningStatus>();

    string url = $"http://apis.data.go.kr/1360000/WthrWrnInfoService/getPwnStatus?serviceKey={_encodedKey}&pageNo=1&numOfRows=100&dataType=JSON&stnId={stnId}";

    var result = new List<WeatherWarningStatus>();

    try {
      var response = await _httpClient.GetAsync(url);
      if (!response.IsSuccessStatusCode) return result;

      var jsonStr = await response.Content.ReadAsStringAsync();
      using var doc = JsonDocument.Parse(jsonStr);
      var root = doc.RootElement;

      var itemsNode = root.GetProperty("response").GetProperty("body").GetProperty("items");
      if (itemsNode.ValueKind == JsonValueKind.String) return result;

      var items = itemsNode.GetProperty("item");

      string GetVal(JsonElement el, string prop) {
        if (!el.TryGetProperty(prop, out var p)) return "";
        return p.ValueKind == JsonValueKind.Number ? p.GetRawText() : (p.GetString() ?? "");
      }

      foreach (var item in items.EnumerateArray()) {
        int seq = 0;
        int.TryParse(GetVal(item, "tmSeq"), out seq);

        result.Add(new WeatherWarningStatus {
          TmFc = GetVal(item, "tmFc"),
          TmEf = GetVal(item, "tmEf"),
          StnId = stnId,
          TmSeq = seq,
          T6 = GetVal(item, "t6"),
          T7 = GetVal(item, "t7"),
          Other = GetVal(item, "other"),
          CreatedAt = DateTimeOffset.UtcNow,
          CreatedBy = "System_API"
        });
      }
    } catch (Exception ex) {
      _logger.LogError(ex, "특보 현황 조회 오류: stnId={StnId}", stnId);
    }

    return result;
  }
}
