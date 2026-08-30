using System.ComponentModel.DataAnnotations;

namespace LifeEnvServer.Models;

/// <summary>
/// 날씨 기준 정보 (풍속, 강우, 강설, 폭염, 한파 등)
/// </summary>
public class WeatherStandard : LifeEnvBaseEntity
{
    /// <summary>
    /// 기준 구분 (WIND, RAIN, SNOW, HEAT, COLD)
    /// </summary>
    [Required]
    [MaxLength(20)]
    public string Category { get; set; } = string.Empty;

    /// <summary>
    /// 기준 명칭 (예: 강풍 기준, 폭염주의보, 한파경보)
    /// </summary>
    [Required]
    [MaxLength(100)]
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// 기준 설명/조건 텍스트 (예: 초당 10m/s 이상, 체감온도 33도 이상 2일 지속)
    /// </summary>
    [MaxLength(500)]
    public string ConditionText { get; set; } = string.Empty;

    /// <summary>
    /// 수치 기준값 1 (예: 10, 1, 33)
    /// </summary>
    public double? ThresholdValue { get; set; }

    /// <summary>
    /// 비교 연산자 (GE: 크거나 같음, LE: 작거나 같음, GT: 큼, LT: 작음, EQ: 동일, BT: 사이, NB: 범위 밖, DGE: 차이 이상, DLE: 차이 이하)
    /// </summary>
    [MaxLength(10)]
    public string? Operator { get; set; }

    /// <summary>
    /// 수치 기준값 2 (Between 연산자 등에서 사용)
    /// </summary>
    public double? ThresholdValue2 { get; set; }

    /// <summary>
    /// 단위 (m/s, mm, cm, C)
    /// </summary>
    [MaxLength(20)]
    public string? Unit { get; set; }

    /// <summary>
    /// 작업 상태 코드 (ALLOW, CAUTION, RESTRICTED, SUSPENDED)
    /// Common Code: WORK_WEATHER_STATUS
    /// </summary>
    [MaxLength(20)]
    public string? WorkStatus { get; set; }

    /// <summary>
    /// 정렬 순서
    /// </summary>
    public int SortOrder { get; set; }

    /// <summary>
    /// 지속 기간 (단위: 일, 예: 2일 이상 지속)
    /// </summary>
    public int? Duration { get; set; }

    /// <summary>
    /// 전날 대비 차이 기준값 (예: 10도 이상 하강 시 10)
    /// </summary>
    public double? PrevDayDiff { get; set; }

    /// <summary>
    /// 평년값 대비 차이 기준값 (예: 평년보다 3도 낮을 때 -3)
    /// </summary>
    public double? AvgYearDiff { get; set; }

    /// <summary>
    /// 알림 발송 주기 (단위: 분, 0이면 즉시/매번 발송)
    /// </summary>
    public int? NotificationInterval { get; set; }

    /// <summary>
    /// 체감온도 사용 여부 (기온 관련 기준에서 실제 기온 대신 체감온도와 비교할지 여부)
    /// </summary>
    public bool UseSensibleTemp { get; set; } = false;
}
