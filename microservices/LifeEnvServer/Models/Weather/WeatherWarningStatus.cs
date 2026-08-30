using System.ComponentModel.DataAnnotations;

namespace LifeEnvServer.Models;

/// <summary>
/// 기상 특보 현황 정보 (getPwnStatus 원본)
/// </summary>
public class WeatherWarningStatus : LifeEnvBaseEntity
{
    /// <summary>발표 시각 (yyyyMMddHHmm)</summary>
    [MaxLength(14)]
    public string TmFc { get; set; } = string.Empty;

    /// <summary>발효 시각 (yyyyMMddHHmm)</summary>
    [MaxLength(14)]
    public string TmEf { get; set; } = string.Empty;

    /// <summary>지점 번호</summary>
    public int StnId { get; set; }

    /// <summary>발표 번호</summary>
    public int TmSeq { get; set; }

    /// <summary>현황 내용</summary>
    public string? Content { get; set; }

    /// <summary>특보 발효 현황 요약</summary>
    public string? T6 { get; set; }
    /// <summary>예비 특보 발효 현황 요약</summary>
    public string? T7 { get; set; }
    /// <summary>기타 정보</summary>
    public string? Other { get; set; }
}
