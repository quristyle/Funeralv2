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
    /// <summary>
    /// 「내 것」을 셀 수 있었나 — 이 포털 계정을 가리키는 담당자 줄을 찾았는가.
    /// </summary>
    /// <remarks>
    /// <b>거짓이면 위의 건수는 0 이 아니라 「모름」이다.</b> 담당자 권한은
    /// 있으나 아직 아무것도 접수한 적이 없어 담당자 줄이 없는 사람이 그렇다.
    /// 화면이 그 둘을 같은 0 으로 그리면 「내가 한 일이 없다」로 읽힌다.
    /// </remarks>
    public bool Linked { get; set; }
    /// <summary>
    /// 나를 가리키는 <b>담당자 줄 번호</b>(<c>admin.id</c>). 못 찾았으면 null.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>화면이 이 값을 스스로 알아낼 수 없다.</b> 포털 계정이 들고 있는
    /// 헬프데스크 번호(<c>auth_user_links.helpdeskuserid</c>)는 대개
    /// <b>고객 줄</b>을 가리킨다 — 담당자도 요청을 올리므로 고객으로 먼저
    /// 이어지고, 접수자 줄을 만드는 쪽은 그 연결에 손대지 않기 때문이다
    /// (<c>AssigneeProvisioner</c>).
    /// </para>
    /// <para>
    /// 그 둘을 같은 번호로 여기면 「내 것만 보기」가 <b>남의 줄을 거른다</b> —
    /// 고객 2번과 담당자 2번은 서로 다른 사람이다. 그래서 이 집계가 실제로
    /// 센 번호를 그대로 실어 보낸다.
    /// </para>
    /// </remarks>
    public int? AdminId { get; set; }
  }
}
