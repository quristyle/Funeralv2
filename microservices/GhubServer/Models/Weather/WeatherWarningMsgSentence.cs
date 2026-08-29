using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace GhubServer.Models;

/// <summary>
/// 기상 특보 통보문 문장 분할 모델
/// </summary>
public class WeatherWarningMsgSentence : GhubBaseEntity
{
    /// <summary>
    /// 원본 통보문 ID (FK)
    /// </summary>
    [Required]
    public int WeatherWarningMsgId { get; set; }

    /// <summary>
    /// 원본 통보문 객체
    /// </summary>
    [ForeignKey(nameof(WeatherWarningMsgId))]
    public virtual WeatherWarningMsg? WeatherWarningMsg { get; set; }

    /// <summary>
    /// 필드 구분 (t1: 제목, t2: 발표내용, t3: 예비특보, t4: 참고사항, t5: 기타, t6: 특보발효현황, t7: 예비특보현황)
    /// </summary>
    [Required]
    [MaxLength(10)]
    public string FieldType { get; set; } = string.Empty;

    /// <summary>
    /// 문장 순서
    /// </summary>
    [Required]
    public int Sequence { get; set; }

    /// <summary>
    /// 문장 제목 (예: 특보명, 지역구분 등)
    /// </summary>
    [MaxLength(500)]
    public string? Title { get; set; }

    /// <summary>
    /// 분할된 문장 내용
    /// </summary>
    public string Content { get; set; } = string.Empty;
}
