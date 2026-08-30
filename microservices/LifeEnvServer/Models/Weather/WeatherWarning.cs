using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace LifeEnvServer.Models;

/// <summary>
/// 기상청 날씨 특보 정보
/// </summary>
public class WeatherWarning : LifeEnvBaseEntity
{
    /// <summary>
    /// 발표시각 (yyyyMMddHHmm) - API의 tmFc
    /// </summary>
    [Required]
    [MaxLength(14)]
    public string TmFc { get; set; } = string.Empty;

    /// <summary>
    /// 지점코드 (예: 108 전국)
    /// </summary>
    public int StnId { get; set; }

    /// <summary>
    /// 발표번호
    /// </summary>
    public int TmSeq { get; set; }

    /// <summary>
    /// 특보 제목 (t1)
    /// </summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>
    /// 특보 내용 (t2) - 발효현황 등 상세 텍스트
    /// </summary>
    public string Content { get; set; } = string.Empty;

    /// <summary>
    /// 예비 특보 등 기타 정보 (other)
    /// </summary>
    public string? Other { get; set; }

    /// <summary>
    /// 특보 번호 (예: 제01-198호)
    /// </summary>
    public string? WarningNum { get; set; }

    /// <summary>
    /// 실제 발령 시각
    /// </summary>
    public DateTimeOffset? AnnouncementTime { get; set; }

    /// <summary>
    /// 특보 상태 (발령, 해제, 변경, 대체 등)
    /// </summary>
    public string? Command { get; set; }

    /// <summary>
    /// 수집 시각
    /// </summary>
    public DateTimeOffset CollectedAt { get; set; } = DateTimeOffset.UtcNow;

    /// <summary>
    /// 매칭된 관리 지역 목록 (DB 미저장)
    /// </summary>
    [NotMapped]
    public List<WeatherLocation> MatchedLocations { get; set; } = new();

    /// <summary>
    /// 분할된 통보문 문장 목록 (DB 미저장)
    /// </summary>
    [NotMapped]
    public List<WeatherWarningMsgSentence> Sentences { get; set; } = new();
}
