using System.ComponentModel.DataAnnotations;

namespace GhubServer.Models;

/// <summary>
/// 초단기 예보 (Ultra Short Term Forecast).
/// 수집 이력이라 감사 컬럼이 없다 — GhubBaseEntity 를 상속하지 않는다.
/// 속성명 T1H 는 snake_case 변환 규칙(([a-z0-9])([A-Z]))에 따라 t1_h 컬럼이 된다.
/// </summary>
public class WeatherUltraSrtForecast
{
    /// <summary>초단기 예보 ID</summary>
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

    /// <summary>1시간 기온 (℃) — 컬럼 t1_h</summary>
    public double? T1H { get; set; }
    /// <summary>1시간 강수량 (mm)</summary>
    public string? RN1 { get; set; }
    /// <summary>하늘 상태 코드</summary>
    public int? SKY { get; set; }
    /// <summary>동서바람성분 (m/s)</summary>
    public double? UUU { get; set; }
    /// <summary>남북바람성분 (m/s)</summary>
    public double? VVV { get; set; }
    /// <summary>습도 (%)</summary>
    public int? REH { get; set; }
    /// <summary>강수 형태 코드</summary>
    public int? PTY { get; set; }
    /// <summary>낙뢰 코드</summary>
    public int? LGT { get; set; }
    /// <summary>풍향 (deg)</summary>
    public double? VEC { get; set; }
    /// <summary>풍속 (m/s)</summary>
    public double? WSD { get; set; }

    /// <summary>등록 일시</summary>
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}
