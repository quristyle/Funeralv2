using System.ComponentModel.DataAnnotations;

namespace GhubServer.Models;

/// <summary>
/// 기상청 특보 구역 정보 (구역 트리 — RegUp 이 부모)
/// </summary>
public class WeatherWarningZone : GhubBaseEntity
{
    /// <summary>
    /// 특보구역코드 (REG_ID)
    /// </summary>
    [Required]
    [MaxLength(20)]
    public string RegId { get; set; } = string.Empty;

    /// <summary>
    /// 시작시각 (TM_ST) - 년월일시분(KST)
    /// </summary>
    [MaxLength(14)]
    public string? TmSt { get; set; }

    /// <summary>
    /// 종료시각 (TM_ED) - 년월일시분(KST)
    /// </summary>
    [MaxLength(14)]
    public string? TmEd { get; set; }

    /// <summary>
    /// 특성 (REG_SP)
    /// </summary>
    [MaxLength(100)]
    public string? RegSp { get; set; }

    /// <summary>
    /// 상위 특보구역코드 (REG_UP)
    /// </summary>
    [MaxLength(20)]
    public string? RegUp { get; set; }

    /// <summary>
    /// 특보구역명(약어) (REG_KO)
    /// </summary>
    [MaxLength(100)]
    public string? RegKo { get; set; }

    /// <summary>
    /// 특보구역명 (REG_NAME)
    /// </summary>
    [MaxLength(200)]
    public string? RegName { get; set; }
}
