namespace HelpDeskServer.Dtos {
  /// <summary>
  /// 월별 요청 통계 DTO
  /// </summary>
  public class MonthlyStatsDto {
    /// <summary>월 (예: 2025-03)</summary>
    public string Month { get; set; } = string.Empty;
    /// <summary>접수건수</summary>
    public int TotalCount { get; set; }
    /// <summary>완료건수</summary>
    public int CompletedCount { get; set; }
  }
}
