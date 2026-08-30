using System.ComponentModel.DataAnnotations.Schema;

namespace GhubServer.Models;

/// <summary>
/// 날씨 기준 부합 기록 (기준 초과 이벤트)
/// </summary>
public class WeatherEventRecord : GhubBaseEntity
{
    /// <summary>
    /// 날씨 정보 ID
    /// </summary>
    public int WeatherInfoId { get; set; }

    /// <summary>
    /// 날씨 정보 네비게이션
    /// </summary>
    [ForeignKey(nameof(WeatherInfoId))]
    public virtual WeatherInfo? WeatherInfo { get; set; }

    /// <summary>
    /// 날씨 기준 ID
    /// </summary>
    public int WeatherStandardId { get; set; }

    /// <summary>
    /// 날씨 기준 네비게이션
    /// </summary>
    [ForeignKey(nameof(WeatherStandardId))]
    public virtual WeatherStandard? WeatherStandard { get; set; }

    /// <summary>
    /// 발생 시각
    /// </summary>
    public DateTimeOffset EventTime { get; set; }

    /// <summary>
    /// 당시 측정값
    /// </summary>
    public double MeasuredValue { get; set; }

    /// <summary>
    /// 알림 발송 여부
    /// </summary>
    public bool IsNotified { get; set; } = false;
}
