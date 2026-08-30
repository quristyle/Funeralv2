using System.ComponentModel.DataAnnotations;

namespace LifeEnvServer.Models;

/// <summary>
/// 단기 예보 (Short Term Forecast / Vilage Forecast).
/// BaseTime: 02,05,08,11,14,17,20,23시 (1일 8회).
/// 수집 이력이라 감사 컬럼이 없다 — LifeEnvBaseEntity 를 상속하지 않는다.
/// </summary>
public class WeatherShortTermForecast
{
    /// <summary>단기 예보 ID</summary>
    public int Id { get; set; }

    /// <summary>기상 관측 지점 ID</summary>
    public int WeatherLocationId { get; set; }

    /// <summary>기상 관측 지점 내비게이션</summary>
    public virtual WeatherLocation? WeatherLocation { get; set; }

    /// <summary>발표 날짜</summary>
    [MaxLength(10)]
    public string BaseDate { get; set; } = string.Empty;

    /// <summary>발표 시각</summary>
    [MaxLength(10)]
    public string BaseTime { get; set; } = string.Empty;

    /// <summary>예보 날짜</summary>
    [MaxLength(10)]
    public string FcstDate { get; set; } = string.Empty;

    /// <summary>예보 시각</summary>
    [MaxLength(10)]
    public string FcstTime { get; set; } = string.Empty;

    /// <summary>강수 확률 (%)</summary>
    public int? POP { get; set; }
    /// <summary>강수 형태 코드</summary>
    public int? PTY { get; set; }
    /// <summary>1시간 강수량</summary>
    public string? PCP { get; set; }
    /// <summary>습도 (%)</summary>
    public int? REH { get; set; }
    /// <summary>1시간 신적설</summary>
    public string? SNO { get; set; }
    /// <summary>하늘 상태 코드</summary>
    public int? SKY { get; set; }
    /// <summary>1시간 기온 (℃)</summary>
    public double? TMP { get; set; }
    /// <summary>일최저기온 (℃)</summary>
    public double? TMN { get; set; }
    /// <summary>일최고기온 (℃)</summary>
    public double? TMX { get; set; }
    /// <summary>동서바람성분 (m/s)</summary>
    public double? UUU { get; set; }
    /// <summary>남북바람성분 (m/s)</summary>
    public double? VVV { get; set; }
    /// <summary>파고 (M)</summary>
    public double? WAV { get; set; }
    /// <summary>풍향 (deg)</summary>
    public double? VEC { get; set; }
    /// <summary>풍속 (m/s)</summary>
    public double? WSD { get; set; }

    /// <summary>등록 일시</summary>
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}
