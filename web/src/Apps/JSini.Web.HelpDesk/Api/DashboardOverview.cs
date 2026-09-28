namespace JSini.Web.HelpDesk.Api;

// 현황판(`dashboard/overview`)이 한 번에 받아 오는 집계 묶음.
//
// 서버의 HelpDeskServer/Dtos/DashboardOverviewDto.cs 와 짝이다. **칸 이름을
// 서버에 맞춰 둔다** — 어긋나면 역직렬화가 오류 없이 0 을 채우고, 증상이
// 「숫자가 전부 0 이다」로 나타나 원인이 화면처럼 보인다.
//
// 통로를 하나로 둔 까닭(왕복 열 번 · 조각마다 다른 순간)은 서버 쪽 머리말에 있다.

/// <summary>현황판 한 판.</summary>
public sealed class DashboardOverview
{
    public DashboardScope Scope { get; set; } = new();
    public DashboardSummary Summary { get; set; } = new();
    public List<DashboardSlice> Status { get; set; } = [];
    public List<DashboardSlice> Types { get; set; } = [];
    public List<DashboardMonth> Monthly { get; set; } = [];
    public List<DashboardDay> Daily { get; set; } = [];
    public List<DashboardSlice> Weekday { get; set; } = [];
    public List<DashboardSlice> Hourly { get; set; } = [];
    public List<DashboardCompany> Companies { get; set; } = [];
    public List<DashboardAdmin> Admins { get; set; } = [];
    public List<DashboardSlice> PendingAging { get; set; } = [];
    public List<DashboardRequest> OldestPending { get; set; } = [];
    public List<DashboardCustomer> TopCustomers { get; set; } = [];
    public List<DashboardRequest> Recent { get; set; } = [];
}

/// <summary>이 숫자들이 무엇을 센 것인가.</summary>
public sealed class DashboardScope
{
    public bool IsAdmin { get; set; }
    public bool IsLinked { get; set; }

    /// <summary>회사 하나로 좁혀 센 것인가. 고객 계정은 늘 참이다.</summary>
    public bool CompanyScoped { get; set; }

    public string? CompanyId { get; set; }
    public string? CompanyName { get; set; }

    /// <summary>화면 머리에 적을 한마디 — 「전체」 · 「○○(주)」.</summary>
    public string Label { get; set; } = "전체";

    public int Days { get; set; }
    public int Months { get; set; }

    /// <summary>집계 시각(UTC).</summary>
    public DateTime GeneratedAt { get; set; }
}

/// <summary>타일에 올라가는 숫자들.</summary>
public sealed class DashboardSummary
{
    public int Total { get; set; }
    public int Pending { get; set; }
    public int InProgress { get; set; }
    public int Consultation { get; set; }
    public int Negotiation { get; set; }
    public int Completed { get; set; }
    public int UserCompleted { get; set; }
    public int Rejected { get; set; }

    /// <summary>아직 안 닫힌 것.</summary>
    public int Open { get; set; }

    public int Closed { get; set; }

    /// <summary>담당자가 아직 안 붙은 것.</summary>
    public int Unassigned { get; set; }

    /// <summary>긴급/장애 유형 중 안 닫힌 것.</summary>
    public int EmergencyOpen { get; set; }

    public int Today { get; set; }
    public int Yesterday { get; set; }
    public int ThisWeek { get; set; }
    public int ThisMonth { get; set; }
    public int LastMonth { get; set; }
    public int CompletedToday { get; set; }
    public int CompletedThisMonth { get; set; }

    /// <summary>이번 달이 지난달보다 몇 % 늘었나.</summary>
    public double MonthOverMonthRate { get; set; }

    public double CompletionRate { get; set; }

    /// <summary>접수까지 걸린 평균 시간.</summary>
    public double AvgResponseHours { get; set; }

    /// <summary>그 평균을 낸 표본 수. 0 이면 아직 잴 수 없다.</summary>
    public int ResponseSamples { get; set; }

    public double AvgResolutionHours { get; set; }
    public double MedianResolutionHours { get; set; }
    public int ResolutionSamples { get; set; }

    /// <summary>하루 안에 접수된 비율(%).</summary>
    public double Sla24Rate { get; set; }

    /// <summary>사흘 안에 완료된 비율(%).</summary>
    public double Sla72Rate { get; set; }

    public double OldestPendingDays { get; set; }
    public double AvgOpenAgeDays { get; set; }
    public int Comments { get; set; }
    public double CommentsPerRequest { get; set; }

    /// <summary>댓글이 한 줄도 안 달린 채 안 닫혀 있는 건.</summary>
    public int NoReplyOpen { get; set; }

    public int Requesters { get; set; }
    public int ActiveAdmins { get; set; }
    public int ActiveCompanies { get; set; }
    public DateTime? LastRequestedAt { get; set; }
}

/// <summary>이름 하나에 수 하나 — 도넛·막대 조각.</summary>
public sealed class DashboardSlice
{
    /// <summary>열쇠(상태 enum 이름 · 요일 번호 · 시각). 화면이 색을 고를 때 쓴다.</summary>
    public string Key { get; set; } = string.Empty;

    public string Label { get; set; } = string.Empty;
    public int Count { get; set; }
    public double Share { get; set; }
}

/// <summary>월별 한 칸.</summary>
public sealed class DashboardMonth
{
    public string Month { get; set; } = string.Empty;
    public string Label { get; set; } = string.Empty;
    public int Requested { get; set; }
    public int Completed { get; set; }
    public int Rejected { get; set; }

    /// <summary>그 달 말까지 쌓인 미처리.</summary>
    public int Backlog { get; set; }

    public double AvgResolutionHours { get; set; }
}

/// <summary>일별 한 칸.</summary>
public sealed class DashboardDay
{
    public string Date { get; set; } = string.Empty;
    public string Label { get; set; } = string.Empty;
    public string Weekday { get; set; } = string.Empty;
    public bool IsWeekend { get; set; }
    public int Requested { get; set; }
    public int Completed { get; set; }
}

/// <summary>고객사별 한 줄.</summary>
public sealed class DashboardCompany
{
    public string CompanyId { get; set; } = string.Empty;
    public string CompanyName { get; set; } = string.Empty;
    public int Total { get; set; }
    public int Pending { get; set; }
    public int InProgress { get; set; }

    /// <summary>협의 + 논의.</summary>
    public int Talking { get; set; }

    public int Completed { get; set; }
    public int Rejected { get; set; }
    public int Open { get; set; }
    public double CompletionRate { get; set; }
    public double AvgResponseHours { get; set; }
    public double AvgResolutionHours { get; set; }
    public int ThisMonth { get; set; }
    public int EmergencyOpen { get; set; }
    public double OldestPendingDays { get; set; }
    public DateTime? LastRequestedAt { get; set; }
    public int Requesters { get; set; }
}

/// <summary>담당자별 한 줄.</summary>
public sealed class DashboardAdmin
{
    public int AdminId { get; set; }
    public string AdminName { get; set; } = string.Empty;
    public string? Photo { get; set; }
    public int Assigned { get; set; }
    public int InProgress { get; set; }
    public int Talking { get; set; }
    public int Completed { get; set; }
    public int Rejected { get; set; }
    public int Open { get; set; }
    public double CompletionRate { get; set; }
    public double AvgResolutionHours { get; set; }
    public int CompletedThisMonth { get; set; }

    /// <summary>전체 완료 건에서 차지하는 비율(%).</summary>
    public double Share { get; set; }

    public DateTime? LastCompletedAt { get; set; }
}

/// <summary>요청자별 한 줄.</summary>
public sealed class DashboardCustomer
{
    public int CustomerId { get; set; }
    public string UserName { get; set; } = string.Empty;
    public string CompanyName { get; set; } = string.Empty;
    public int Total { get; set; }
    public int Open { get; set; }
    public int Completed { get; set; }
    public DateTime? LastRequestedAt { get; set; }
}

/// <summary>목록에 한 줄로 서는 요청.</summary>
public sealed class DashboardRequest
{
    public int Id { get; set; }
    public string Title { get; set; } = string.Empty;

    /// <summary>상태 enum 이름. 배지 색을 여기서 고른다.</summary>
    public string Status { get; set; } = string.Empty;

    public string StatusName { get; set; } = string.Empty;
    public string TypeName { get; set; } = string.Empty;
    public bool IsEmergency { get; set; }
    public string CompanyName { get; set; } = string.Empty;
    public string CustomerName { get; set; } = string.Empty;
    public string? AdminName { get; set; }
    public DateTime RequestedAt { get; set; }
    public DateTime? AcceptedAt { get; set; }

    /// <summary>지금까지(닫혔으면 닫힐 때까지) 흐른 날수.</summary>
    public double AgeDays { get; set; }

    public int Comments { get; set; }
}
