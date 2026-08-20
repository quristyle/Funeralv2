namespace HelpDeskServer.Dtos {
  /// <summary>
  /// 특정 관리자의 업무 통계 DTO
  /// </summary>
  public class AdminStatsDto {
    /// <summary>대기 중인 요청 수 (관리 팀 업체)</summary>
    public int PendingCount { get; set; }
    /// <summary>진행 중인 요청 수</summary>
    public int InProgressCount { get; set; }
    /// <summary>완료한 요청 수</summary>
    public int CompletedCount { get; set; }


    /// <summary>종료한 요청 수</summary>
    public int UserCompletedCount { get; set; }
    /// <summary>반려한 요청 수</summary>
    public int RejectedCount { get; set; }
    /// <summary>협의 요청 수</summary>
    public int ConsultationCount { get; set; }
    /// <summary>논의 요청 수</summary>
    public int NegotiationCount { get; set; }
    /// <summary>
    /// 시스템의 전체 요청 수
    /// </summary>
    public int TotalRequests { get; set; }
  }
}
