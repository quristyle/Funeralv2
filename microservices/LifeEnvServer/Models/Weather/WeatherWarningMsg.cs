using System.ComponentModel.DataAnnotations;

namespace LifeEnvServer.Models;

/// <summary>
/// 기상 특보 통보문 상세 정보
/// </summary>
public class WeatherWarningMsg : LifeEnvBaseEntity
{
    /// <summary>발표 시각 (yyyyMMddHHmm)</summary>
    [MaxLength(14)]
    public string TmFc { get; set; } = string.Empty;

    /// <summary>지점 번호</summary>
    public int StnId { get; set; }

    /// <summary>발표 번호</summary>
    public int TmSeq { get; set; }

    /// <summary>제목</summary>
    [MaxLength(500)]
    public string Title { get; set; } = string.Empty;

    /// <summary>특보 발효 현황 1</summary>
    public string? T1 { get; set; }
    /// <summary>특보 발효 현황</summary>
    public string? T2 { get; set; }
    /// <summary>예비 특보 발효 현황</summary>
    public string? T3 { get; set; }
    /// <summary>기상 상세 설명</summary>
    public string? T4 { get; set; }
    /// <summary>유의 사항</summary>
    public string? T5 { get; set; }
    /// <summary>특보 발효 현황 요약</summary>
    public string? T6 { get; set; }
    /// <summary>예비 특보 발효 현황 요약</summary>
    public string? T7 { get; set; }

    /// <summary>기타 정보</summary>
    public string? Other { get; set; }
    /// <summary>발령 정보</summary>
    public string? WarFc { get; set; }

    /// <summary>
    /// 분할된 통보문 문장 목록
    /// </summary>
    public virtual ICollection<WeatherWarningMsgSentence> Sentences { get; set; } = new List<WeatherWarningMsgSentence>();
}
