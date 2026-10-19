namespace HelpDeskServer.Dtos {
  /// <summary>특정 관리자의 업무 통계 DTO</summary>
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
    /// <summary>시스템의 전체 요청 수</summary>
    public int TotalRequests { get; set; }
    /// <summary>「내 것」을 셀 수 있었나 — 이 포털 계정을 가리키는 담당자 줄을 찾았는가. 거짓이면 위의 건수는 0 이 아니라 「모름」이다.</summary>
    public bool Linked { get; set; }
    /// <summary>나를 가리키는 <b>담당자 줄 번호</b>(<c>admin.id</c>). 못 찾았으면 null. 화면이 이 값을 스스로 알아낼 수 없다.</summary>
    public int? AdminId { get; set; }
  }
}
