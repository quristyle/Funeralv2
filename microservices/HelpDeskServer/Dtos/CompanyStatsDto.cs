
namespace HelpDeskServer.Dtos;

/// <summary>
/// 고객사별 요청 통계 DTO
/// </summary>
public class CompanyStatsDto {


  /// <summary>고객사 Id</summary>
  public int? Id { get; set; }
  /// <summary>고객사 이름</summary>
  public string? CompanyName { get; set; }
  /// <summary>마지막 접수대기 요청 일시</summary>
  public DateTime? LastPendingDate { get; set; }
  /// <summary>접수대기 요청 수</summary>
  public int PendingCount { get; set; }
  /// <summary>진행 중인 요청 수</summary>
  public int InProgressCount { get; set; }
  /// <summary>협의 요청 수</summary>
  public int ConsultationCount { get; set; }
  /// <summary>논의 요청 수</summary>
  public int NegotiationCount { get; set; }
  /// <summary>완료된 요청 수</summary>
  public int CompletedCount { get; set; }
  /// <summary>반려된 요청 수</summary>
  public int RejectedCount { get; set; }
  /// <summary>
  /// 완료율 (반려 제외)
  /// </summary>
  public double CompletionRate { get; set; }
}
