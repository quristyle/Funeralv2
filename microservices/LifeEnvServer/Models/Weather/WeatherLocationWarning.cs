namespace LifeEnvServer.Models;

/// <summary>
/// 기상 특보 지역별 매칭 이력 (특보 ↔ 지역 다대다)
/// </summary>
public class WeatherLocationWarning : LifeEnvBaseEntity
{
    /// <summary>
    /// 특보 마스터 ID
    /// </summary>
    public int WeatherWarningId { get; set; }

    /// <summary>기상 특보 마스터 정보</summary>
    public WeatherWarning? WeatherWarning { get; set; }

    /// <summary>
    /// 관측 지역 ID
    /// </summary>
    public int WeatherLocationId { get; set; }

    /// <summary>기상 관측 지점 마스터 정보</summary>
    public WeatherLocation? WeatherLocation { get; set; }

    /// <summary>
    /// 알림 발송 여부
    /// </summary>
    public bool IsNotified { get; set; }

    /// <summary>
    /// 알림 발송 시각
    /// </summary>
    public DateTimeOffset? NotifiedAt { get; set; }
}
