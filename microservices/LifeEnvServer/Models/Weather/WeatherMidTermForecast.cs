using System.ComponentModel.DataAnnotations;

namespace LifeEnvServer.Models;

/// <summary>
/// 중기 예보 정보 (주간 날씨).
/// 수집 이력이라 감사 컬럼이 없다 — LifeEnvBaseEntity 를 상속하지 않는다.
/// </summary>
public class WeatherMidTermForecast
{
    /// <summary>중기 예보 ID</summary>
    public int Id { get; set; }

    /// <summary>기상 관측 지점 ID</summary>
    public int WeatherLocationId { get; set; }

    /// <summary>기상 관측 지점 내비게이션</summary>
    public virtual WeatherLocation? WeatherLocation { get; set; }

    /// <summary>
    /// 발표 시각 (Base Date + 0600/1800)
    /// </summary>
    [MaxLength(12)]
    public string BaseDate { get; set; } = string.Empty;

    /// <summary>
    /// 예보 날짜 (YYYY-MM-DD)
    /// </summary>
    public DateOnly ForecastDate { get; set; }

    /// <summary>
    /// N일 후 (3~10)
    /// </summary>
    public int DayAfter { get; set; }

    /// <summary>오전 날씨 상태</summary>
    public string AmSky { get; set; } = string.Empty;
    /// <summary>오후 날씨 상태</summary>
    public string PmSky { get; set; } = string.Empty;

    /// <summary>오전 강수 확률</summary>
    public int AmPop { get; set; }
    /// <summary>오후 강수 확률</summary>
    public int PmPop { get; set; }

    /// <summary>최저 기온</summary>
    public int MinTemp { get; set; }
    /// <summary>최고 기온</summary>
    public int MaxTemp { get; set; }

    /// <summary>등록 일시</summary>
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}
