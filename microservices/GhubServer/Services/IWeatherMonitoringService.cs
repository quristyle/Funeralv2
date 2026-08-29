using GhubServer.Models;

namespace GhubServer.Services;

/// <summary>
/// 날씨 모니터링 및 기준 체크 서비스
/// </summary>
public interface IWeatherMonitoringService
{
    /// <summary>
    /// 수집된 날씨 정보를 기준으로 설정된 기준을 체크하고 기록을 남깁니다.
    /// </summary>
    Task CheckWeatherStandardsAsync(WeatherInfo weatherInfo);
}
