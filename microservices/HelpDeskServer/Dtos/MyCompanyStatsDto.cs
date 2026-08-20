namespace HelpDeskServer.Dtos {
  /// <summary>
  /// '내 회사'의 요청 통계 DTO
  /// </summary>
  public class MyCompanyStatsDto {
    /// <summary>접수대기 요청 수</summary>
    public int PendingCount { get; set; }
    /// <summary>진행 중인 요청 수</summary>
    public int InProgressCount { get; set; }
    /// <summary>완료된 요청 수</summary>
    public int CompletedCount { get; set; }
    /// <summary>종료된 요청 수</summary>
    public int UserCompletedCount { get; set; }
    /// <summary>반려된 요청 수</summary>
    public int RejectedCount { get; set; }
    /// <summary>협의 요청 수</summary>
    public int ConsultationCount { get; set; }
    /// <summary>논의 요청 수</summary>
    public int NegotiationCount { get; set; }
    /// <summary>
    /// 총 요청 수
    /// </summary>
    public int TotalCount { get; set; }
  }
}
