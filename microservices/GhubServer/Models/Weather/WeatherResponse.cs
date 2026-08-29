using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace GhubServer.Models;

/// <summary>
/// 날씨 기준별 대응 정보 (행동 요령)
/// </summary>
public class WeatherResponse : GhubBaseEntity
{
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
    /// 대응 행동 내용 (예: 그늘에서 휴식 제공, 작업 중지)
    /// </summary>
    [Required]
    public string ActionContent { get; set; } = string.Empty;

    /// <summary>
    /// 세부 설명 또는 비고
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// 정렬 순서
    /// </summary>
    public int SortOrder { get; set; }
}
