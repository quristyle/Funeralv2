namespace HelpDeskServer.Dtos;

/// <summary>
/// 헬프데스크 현황판이 한 번에 받아 가는 집계 묶음.
///
/// <para>
/// [왜 한 덩어리인가]
/// </para>
///
/// <para>
/// 현황판이 묻는 것이 열 가지가 넘는다(요약 · 상태 · 유형 · 월별 · 일별 ·
/// 요일 · 시간대 · 회사별 · 담당자별 · 대기 적체 · 최근 접수). 통로를 열 개로
/// 나누면 <b>화면이 열 번 왕복</b>하고, 그 사이에 자료가 바뀌면 타일과 차트가
/// 서로 다른 순간을 가리킨다. 재료가 같은 요청 목록 하나라 <b>한 번 읽어
/// 한꺼번에 접는다</b>.
/// </para>
///
/// <para>
/// 옛 통로(<c>company-stats</c> · <c>all-admin-stats</c> · <c>admin-stats</c> …)는
/// 그대로 둔다 — 다른 화면이 쓰고 있다.
/// </para>
/// </summary>
public sealed class DashboardOverviewDto {
  /// <summary>이 집계가 무엇을 센 것인지. 화면이 「누구의 숫자인가」를 밝힌다.</summary>
  public DashboardScopeDto Scope { get; set; } = new();

  /// <summary>맨 위 타일에 올라가는 숫자들.</summary>
  public DashboardSummaryDto Summary { get; set; } = new();

  /// <summary>상태별 분포(도넛).</summary>
  public List<DashboardSliceDto> Status { get; set; } = [];

  /// <summary>유형별 분포(질문 · 개선 · 오류 …).</summary>
  public List<DashboardSliceDto> Types { get; set; } = [];

  /// <summary>월별 추이.</summary>
  public List<DashboardMonthDto> Monthly { get; set; } = [];

  /// <summary>일별 추이.</summary>
  public List<DashboardDayDto> Daily { get; set; } = [];

  /// <summary>요일별 접수 분포(월~일 일곱 칸 고정).</summary>
  public List<DashboardSliceDto> Weekday { get; set; } = [];

  /// <summary>시간대별 접수 분포(0~23시 스물네 칸 고정).</summary>
  public List<DashboardSliceDto> Hourly { get; set; } = [];

  /// <summary>고객사별 집계.</summary>
  public List<DashboardCompanyDto> Companies { get; set; } = [];

  /// <summary>담당자별 집계.</summary>
  public List<DashboardAdminDto> Admins { get; set; } = [];

  /// <summary>대기 경과 구간별 건수.</summary>
  public List<DashboardSliceDto> PendingAging { get; set; } = [];

  /// <summary>가장 오래 기다린 「대기」 요청들.</summary>
  public List<DashboardRequestDto> OldestPending { get; set; } = [];

  /// <summary>요청을 많이 올린 사람들.</summary>
  public List<DashboardCustomerDto> TopCustomers { get; set; } = [];

  /// <summary>최근 접수.</summary>
  public List<DashboardRequestDto> Recent { get; set; } = [];
}

/// <summary>집계 범위.</summary>
public sealed class DashboardScopeDto {
  /// <summary>담당자 권한으로 보고 있는가.</summary>
  public bool IsAdmin { get; set; }

  /// <summary>포털 계정이 헬프데스크 사용자에 이어져 있는가.</summary>
  public bool IsLinked { get; set; }

  /// <summary>
  /// 회사 하나로 좁혀 센 것인가. 고객 계정은 늘 <c>true</c> 다 —
  /// 남의 회사 요청까지 세어 보여 주면 안 된다.
  /// </summary>
  public bool CompanyScoped { get; set; }

  /// <summary>좁혀 센 회사의 아이디(포털 값). 전체 범위면 null.</summary>
  public string? CompanyId { get; set; }

  /// <summary>좁혀 센 회사의 이름. 전체 범위면 null.</summary>
  public string? CompanyName { get; set; }

  /// <summary>화면 머리에 적을 한마디 — 「전체」 · 「○○(주)」.</summary>
  public string Label { get; set; } = "전체";

  /// <summary>일별 추이를 며칠 치 담았나.</summary>
  public int Days { get; set; }

  /// <summary>월별 추이를 몇 달 치 담았나.</summary>
  public int Months { get; set; }

  /// <summary>집계 시각(UTC). 화면이 「언제 기준인가」를 적는다.</summary>
  public DateTime GeneratedAt { get; set; }
}

/// <summary>맨 위 타일 · 지표.</summary>
public sealed class DashboardSummaryDto {
  /// <summary>전체 요청 수(삭제 상태 제외).</summary>
  public int Total { get; set; }

  /// <summary>대기</summary>
  public int Pending { get; set; }

  /// <summary>진행</summary>
  public int InProgress { get; set; }

  /// <summary>협의</summary>
  public int Consultation { get; set; }

  /// <summary>논의</summary>
  public int Negotiation { get; set; }

  /// <summary>완료(담당자가 처리 완료로 바꾼 것)</summary>
  public int Completed { get; set; }

  /// <summary>종료(요청자가 확인해 닫은 것)</summary>
  public int UserCompleted { get; set; }

  /// <summary>반려</summary>
  public int Rejected { get; set; }

  /// <summary>아직 안 닫힌 것 — 대기 + 진행 + 협의 + 논의.</summary>
  public int Open { get; set; }

  /// <summary>닫힌 것 — 완료 + 종료 + 반려.</summary>
  public int Closed { get; set; }

  /// <summary>담당자가 아직 안 붙은 것(안 닫힌 것 중에서).</summary>
  public int Unassigned { get; set; }

  /// <summary>유형이 「긴급/장애」인 것 중 아직 안 닫힌 것.</summary>
  public int EmergencyOpen { get; set; }

  /// <summary>오늘 접수(KST)</summary>
  public int Today { get; set; }

  /// <summary>어제 접수(KST)</summary>
  public int Yesterday { get; set; }

  /// <summary>이번 주 접수(월요일부터, KST)</summary>
  public int ThisWeek { get; set; }

  /// <summary>이번 달 접수(KST)</summary>
  public int ThisMonth { get; set; }

  /// <summary>지난달 접수(KST)</summary>
  public int LastMonth { get; set; }

  /// <summary>오늘 완료(KST)</summary>
  public int CompletedToday { get; set; }

  /// <summary>이번 달 완료(KST)</summary>
  public int CompletedThisMonth { get; set; }

  /// <summary>
  /// 이번 달 접수가 지난달보다 몇 % 늘었나. 지난달이 0 이면 0 이다.
  /// </summary>
  public double MonthOverMonthRate { get; set; }

  /// <summary>완료율 — (완료 + 종료) / (전체 − 반려). 반려는 모수에서 뺀다.</summary>
  public double CompletionRate { get; set; }

  /// <summary>접수까지 걸린 평균 시간(시간). 접수 시각이 찍힌 건만 센다.</summary>
  public double AvgResponseHours { get; set; }

  /// <summary>그 평균을 낸 표본 수. 0 이면 「아직 잴 수 없다」는 뜻이다.</summary>
  public int ResponseSamples { get; set; }

  /// <summary>완료까지 걸린 평균 시간(시간).</summary>
  public double AvgResolutionHours { get; set; }

  /// <summary>완료까지 걸린 시간의 중앙값(시간). 평균은 한 건이 길면 통째로 끌려간다.</summary>
  public double MedianResolutionHours { get; set; }

  /// <summary>그 평균·중앙값을 낸 표본 수.</summary>
  public int ResolutionSamples { get; set; }

  /// <summary>하루 안에 접수된 비율(%). 접수 시각이 찍힌 건이 모수다.</summary>
  public double Sla24Rate { get; set; }

  /// <summary>사흘 안에 완료된 비율(%). 완료된 건이 모수다.</summary>
  public double Sla72Rate { get; set; }

  /// <summary>가장 오래 기다린 대기 건의 경과 일수.</summary>
  public double OldestPendingDays { get; set; }

  /// <summary>안 닫힌 건의 평균 경과 일수 — 적체가 얼마나 묵었나.</summary>
  public double AvgOpenAgeDays { get; set; }

  /// <summary>댓글 총수.</summary>
  public int Comments { get; set; }

  /// <summary>요청 한 건당 댓글 수.</summary>
  public double CommentsPerRequest { get; set; }

  /// <summary>댓글이 한 줄도 안 달린 채 안 닫혀 있는 건 — 아무도 응답하지 않은 것.</summary>
  public int NoReplyOpen { get; set; }

  /// <summary>요청을 올린 적이 있는 사람 수.</summary>
  public int Requesters { get; set; }

  /// <summary>요청을 맡은 적이 있는 담당자 수.</summary>
  public int ActiveAdmins { get; set; }

  /// <summary>요청이 하나라도 있는 회사 수.</summary>
  public int ActiveCompanies { get; set; }

  /// <summary>가장 최근 접수 시각(UTC).</summary>
  public DateTime? LastRequestedAt { get; set; }
}

/// <summary>이름 하나에 수 하나 — 도넛·막대 조각.</summary>
public sealed class DashboardSliceDto {
  /// <summary>열쇠(enum 이름 · 요일 번호 · 시각). 화면이 색을 고를 때 쓴다.</summary>
  public string Key { get; set; } = string.Empty;

  /// <summary>사람이 읽을 이름.</summary>
  public string Label { get; set; } = string.Empty;

  /// <summary>건수.</summary>
  public int Count { get; set; }

  /// <summary>전체에서 차지하는 비율(%).</summary>
  public double Share { get; set; }
}

/// <summary>월별 한 칸.</summary>
public sealed class DashboardMonthDto {
  /// <summary><c>yyyy-MM</c></summary>
  public string Month { get; set; } = string.Empty;

  /// <summary>차트 축에 적을 짧은 이름 — <c>9월</c> · 해가 바뀌면 <c>26/1월</c>.</summary>
  public string Label { get; set; } = string.Empty;

  /// <summary>그 달에 접수된 건수.</summary>
  public int Requested { get; set; }

  /// <summary>그 달에 완료·종료된 건수.</summary>
  public int Completed { get; set; }

  /// <summary>그 달에 반려된 건수.</summary>
  public int Rejected { get; set; }

  /// <summary>
  /// 그 달 말까지 쌓인 미처리 — 그때까지 들어온 것에서 닫힌 것을 뺀 수.
  /// 접수·완료 막대만으로는 <b>적체가 늘고 있는지</b>가 안 보인다.
  /// </summary>
  public int Backlog { get; set; }

  /// <summary>그 달에 완료된 건의 평균 처리 시간(시간).</summary>
  public double AvgResolutionHours { get; set; }
}

/// <summary>일별 한 칸.</summary>
public sealed class DashboardDayDto {
  /// <summary><c>yyyy-MM-dd</c> (KST)</summary>
  public string Date { get; set; } = string.Empty;

  /// <summary>차트 축에 적을 짧은 이름 — <c>09/28</c>.</summary>
  public string Label { get; set; } = string.Empty;

  /// <summary>요일 이름 — 주말을 가려 보려고 함께 준다.</summary>
  public string Weekday { get; set; } = string.Empty;

  /// <summary>토·일인가.</summary>
  public bool IsWeekend { get; set; }

  /// <summary>그날 접수된 건수.</summary>
  public int Requested { get; set; }

  /// <summary>그날 완료·종료된 건수.</summary>
  public int Completed { get; set; }
}

/// <summary>고객사별 한 줄.</summary>
public sealed class DashboardCompanyDto {
  /// <summary>회사 아이디(포털 값). 회사를 모르는 요청은 <c>(미지정)</c> 한 줄로 모인다.</summary>
  public string CompanyId { get; set; } = string.Empty;

  /// <summary>회사 이름. 못 풀면 아이디가 그대로 온다.</summary>
  public string CompanyName { get; set; } = string.Empty;

  /// <summary>전체</summary>
  public int Total { get; set; }

  /// <summary>대기</summary>
  public int Pending { get; set; }

  /// <summary>진행</summary>
  public int InProgress { get; set; }

  /// <summary>협의 + 논의</summary>
  public int Talking { get; set; }

  /// <summary>완료 + 종료</summary>
  public int Completed { get; set; }

  /// <summary>반려</summary>
  public int Rejected { get; set; }

  /// <summary>안 닫힌 것</summary>
  public int Open { get; set; }

  /// <summary>완료율(%) — 반려 제외.</summary>
  public double CompletionRate { get; set; }

  /// <summary>평균 접수 소요(시간).</summary>
  public double AvgResponseHours { get; set; }

  /// <summary>평균 처리 소요(시간).</summary>
  public double AvgResolutionHours { get; set; }

  /// <summary>이번 달 접수.</summary>
  public int ThisMonth { get; set; }

  /// <summary>긴급/장애 유형 중 안 닫힌 것.</summary>
  public int EmergencyOpen { get; set; }

  /// <summary>가장 오래 기다린 대기 건의 경과 일수.</summary>
  public double OldestPendingDays { get; set; }

  /// <summary>마지막 접수 시각(UTC).</summary>
  public DateTime? LastRequestedAt { get; set; }

  /// <summary>이 회사에서 요청을 올린 사람 수.</summary>
  public int Requesters { get; set; }
}

/// <summary>담당자별 한 줄.</summary>
public sealed class DashboardAdminDto {
  /// <summary>담당자 번호(헬프데스크 내부).</summary>
  public int AdminId { get; set; }

  /// <summary>담당자 이름.</summary>
  public string AdminName { get; set; } = string.Empty;

  /// <summary>사진 URL.</summary>
  public string? Photo { get; set; }

  /// <summary>맡은 전체 건수.</summary>
  public int Assigned { get; set; }

  /// <summary>진행</summary>
  public int InProgress { get; set; }

  /// <summary>협의 + 논의</summary>
  public int Talking { get; set; }

  /// <summary>완료 + 종료</summary>
  public int Completed { get; set; }

  /// <summary>반려</summary>
  public int Rejected { get; set; }

  /// <summary>아직 안 닫힌 것 — 지금 이 사람이 들고 있는 짐.</summary>
  public int Open { get; set; }

  /// <summary>완료율(%) — 반려 제외.</summary>
  public double CompletionRate { get; set; }

  /// <summary>평균 처리 소요(시간).</summary>
  public double AvgResolutionHours { get; set; }

  /// <summary>이번 달 완료.</summary>
  public int CompletedThisMonth { get; set; }

  /// <summary>전체 완료 건에서 차지하는 비율(%).</summary>
  public double Share { get; set; }

  /// <summary>마지막으로 무언가를 완료한 시각(UTC).</summary>
  public DateTime? LastCompletedAt { get; set; }
}

/// <summary>요청자별 한 줄.</summary>
public sealed class DashboardCustomerDto {
  /// <summary>고객 번호(헬프데스크 내부).</summary>
  public int CustomerId { get; set; }

  /// <summary>이름.</summary>
  public string UserName { get; set; } = string.Empty;

  /// <summary>소속 회사 이름.</summary>
  public string CompanyName { get; set; } = string.Empty;

  /// <summary>올린 전체 건수.</summary>
  public int Total { get; set; }

  /// <summary>그중 안 닫힌 것.</summary>
  public int Open { get; set; }

  /// <summary>그중 완료·종료된 것.</summary>
  public int Completed { get; set; }

  /// <summary>마지막 접수 시각(UTC).</summary>
  public DateTime? LastRequestedAt { get; set; }
}

/// <summary>목록에 한 줄로 서는 요청.</summary>
public sealed class DashboardRequestDto {
  /// <summary>요청 번호.</summary>
  public int Id { get; set; }

  /// <summary>제목.</summary>
  public string Title { get; set; } = string.Empty;

  /// <summary>상태 enum 이름 — 화면이 색을 고를 때 쓴다.</summary>
  public string Status { get; set; } = string.Empty;

  /// <summary>상태 이름(한국어).</summary>
  public string StatusName { get; set; } = string.Empty;

  /// <summary>유형 이름(한국어).</summary>
  public string TypeName { get; set; } = string.Empty;

  /// <summary>긴급/장애 유형인가.</summary>
  public bool IsEmergency { get; set; }

  /// <summary>고객사 이름.</summary>
  public string CompanyName { get; set; } = string.Empty;

  /// <summary>요청자 이름.</summary>
  public string CustomerName { get; set; } = string.Empty;

  /// <summary>담당자 이름. 아직 없으면 null.</summary>
  public string? AdminName { get; set; }

  /// <summary>접수 요청 시각(UTC).</summary>
  public DateTime RequestedAt { get; set; }

  /// <summary>담당자가 맡은 시각(UTC).</summary>
  public DateTime? AcceptedAt { get; set; }

  /// <summary>지금까지 흐른 날수 — 닫힌 건이면 닫힐 때까지 걸린 날수.</summary>
  public double AgeDays { get; set; }

  /// <summary>달린 댓글 수.</summary>
  public int Comments { get; set; }
}
