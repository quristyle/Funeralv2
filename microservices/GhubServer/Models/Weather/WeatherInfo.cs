using System.ComponentModel.DataAnnotations.Schema;

namespace GhubServer.Models;

/// <summary>
/// 날씨 정보 (초단기 실황 수집 이력)
/// </summary>
public class WeatherInfo : GhubBaseEntity
{
    /// <summary>관측 지역 (예: 본사, 지사 코드 등)</summary>
    public string Location { get; set; } = string.Empty;

    /// <summary>관측 지역 ID (FK)</summary>
    public int? WeatherLocationId { get; set; }

    /// <summary>관측 지역 네비게이션</summary>
    [ForeignKey(nameof(WeatherLocationId))]
    public virtual WeatherLocation? WeatherLocation { get; set; }

    /// <summary>관측 시각(UTC 권장)</summary>
    public DateTimeOffset ObservationTime { get; set; } = DateTimeOffset.UtcNow;

    /// <summary>섭씨 온도</summary>
    public double TemperatureC { get; set; }

    /// <summary>기상 상태 설명 (맑음/흐림/비 등)</summary>
    public string Condition { get; set; } = string.Empty;

    /// <summary>습도(%)</summary>
    public int? Humidity { get; set; }

    /// <summary>풍속 (m/s)</summary>
    public double? WindSpeed { get; set; }

    /// <summary>1시간 강수량 (mm)</summary>
    public double? Rainfall { get; set; }

    /// <summary>1시간 신적설 (cm)</summary>
    public double? Snowfall { get; set; }

    /// <summary>풍향 (deg)</summary>
    public double? WindDirection { get; set; }

    /// <summary>강수 형태 (PTY) - 0:없음, 1:비, 2:비/눈, 3:눈, 5:빗방울, 6:빗방울눈날림, 7:눈날림</summary>
    public int? PTY { get; set; }

    /// <summary>동서 바람 성분 (m/s) (UUU) - 동(+), 서(-)</summary>
    public double? UUU { get; set; }

    /// <summary>남북 바람 성분 (m/s) (VVV) - 북(+), 남(-)</summary>
    public double? VVV { get; set; }

    /// <summary>체감온도 (Sensible Temperature)</summary>
    public double? SensibleTemp { get; set; }

    /// <summary>외부 소스 아이콘/이미지 URL</summary>
    public string? IconUrl { get; set; }

    /// <summary>NX 좌표 (DB 미저장)</summary>
    [NotMapped]
    public int NX { get; set; }

    /// <summary>NY 좌표 (DB 미저장)</summary>
    [NotMapped]
    public int NY { get; set; }

    /// <summary>전일 동시간 기온 (DB 미저장, 계산용)</summary>
    [NotMapped]
    public double? YesterdayTemperature { get; set; }
}
