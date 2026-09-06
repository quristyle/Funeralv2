namespace JSini.Web.HelpDesk.Api;

// 헬프데스크 도메인 DTO. HelpDeskServer 의 Models/* 및
// Vue 의 fronts/apps/jsini-portal/src/api/helpdesk/types.ts 와 대응한다.
//
// 서버가 camelCase 로 내려주고 HelpDeskApi 의 JsonOptions 가 대소문자를 가리지
// 않으므로 [JsonPropertyName] 은 이름이 규칙대로 못 가는 곳에만 붙인다.

/// <summary>모든 엔티티가 공유하는 감사 필드.</summary>
public abstract class HdEntity
{
    public int Id { get; set; }
    public DateTime? CreatedAt { get; set; }
    public string? CreatedBy { get; set; }
    public DateTime? ModifiedAt { get; set; }
    public string? ModifiedBy { get; set; }
}

/// <summary>고객사.</summary>
public sealed class Company : HdEntity
{
    public string Name { get; set; } = string.Empty;
}

/// <summary>팀.</summary>
public sealed class Team : HdEntity
{
    public string Name { get; set; } = string.Empty;
    public string? Remark { get; set; }
}

/// <summary>관리자(담당자).</summary>
public sealed class Admin : HdEntity
{
    public string LoginId { get; set; } = string.Empty;
    public string UserName { get; set; } = string.Empty;
    public string? Email { get; set; }
    public string? Photo { get; set; }
    public bool? IsDeleted { get; set; }
    public List<AdminTeam>? AdminTeams { get; set; }
}

/// <summary>관리자-팀 매핑.</summary>
public sealed class AdminTeam
{
    public int AdminId { get; set; }
    public int TeamId { get; set; }
    public Team? Team { get; set; }
}

/// <summary>고객.</summary>
public sealed class Customer : HdEntity
{
    public string LoginId { get; set; } = string.Empty;
    public string UserName { get; set; } = string.Empty;
    public int CompanyId { get; set; }
    public Company? Company { get; set; }
    public string? Email { get; set; }
    public string? Photo { get; set; }
    public string? Sex { get; set; }
    public string? Status { get; set; }
    public string? Remake { get; set; }
    public bool? IsDeleted { get; set; }
}

/// <summary>
/// 개선요청(티켓).
/// 상태(<see cref="Status"/>)는 서버가 이름으로 실어 보내고
/// (Completed·Consultation·Delete·InProgress·Negotiation·Pending·Rejected·UserCompleted),
/// 검색 조건에는 열거형 순번(0~7)을 받는다. 유형은
/// Addition·Bug·Emergency·Error·Etc·Improvement·Question.
/// </summary>
public sealed class ImprovementRequest : HdEntity
{
    public string Title { get; set; } = string.Empty;
    public string Status { get; set; } = "Pending";
    public string? StatusName { get; set; }
    public string? IpType { get; set; }
    public string? Content { get; set; }
    public string? Description { get; set; }
    public bool? IsEmergency { get; set; }
    public int? CustomerId { get; set; }
    public Customer? Customer { get; set; }
    public int? CompanyId { get; set; }
    public Company? Company { get; set; }
    public int? AdminId { get; set; }
    public Admin? Admin { get; set; }
    public int? AssignedAdminId { get; set; }
    public Admin? AssignedAdmin { get; set; }
    public int? ProjectId { get; set; }
    public DateTime? DueDate { get; set; }
    public DateTime? CompletedAt { get; set; }
    public DateTime? CompletededAt { get; set; }
    public string? MainPhoto { get; set; }
    public int? AttachmentCount { get; set; }
    public List<Attachment>? Attachments { get; set; }
    public List<ImprovementComment>? Comments { get; set; }
}

/// <summary>요청 댓글.</summary>
public sealed class ImprovementComment : HdEntity
{
    public int RequestId { get; set; }
    public string Content { get; set; } = string.Empty;
    public string? AuthorName { get; set; }
    /// <summary>admin | customer.</summary>
    public string? AuthorType { get; set; }
    public int? ParentId { get; set; }
    public List<Attachment>? Attachments { get; set; }
}

/// <summary>첨부파일.</summary>
public sealed class Attachment : HdEntity
{
    public string FileName { get; set; } = string.Empty;
    public string? FilePath { get; set; }
    public long? FileSize { get; set; }
    public string? ContentType { get; set; }
    public string? EntityType { get; set; }
    public int? EntityId { get; set; }
}

/// <summary>프로젝트.</summary>
public sealed class Project : HdEntity
{
    public string Name { get; set; } = string.Empty;
    public int? CompanyId { get; set; }
    public int? TeamId { get; set; }
    public Team? Team { get; set; }
    public DateTime? ProjectStart { get; set; }
    public DateTime? ProjectEnd { get; set; }
    public string? Remark { get; set; }
}

/// <summary>WBS 작업 항목. 기본키가 <c>wbsRid</c> 다.</summary>
public sealed class Wbs
{
    public int WbsRid { get; set; }
    public int ProjectId { get; set; }
    public string WbsName { get; set; } = string.Empty;
    public string? WbsCode { get; set; }
    public int? WbsLevel { get; set; }
    public string? WbsType { get; set; }
    public int? ParentWbsId { get; set; }
    public string? Status { get; set; }
    public string? Priority { get; set; }
    public double? Progress { get; set; }
    public string? RiskLevel { get; set; }
    public int? ManagerId { get; set; }
    public int? ResponsibleUserId { get; set; }
    public DateTime? PlanStart { get; set; }
    public DateTime? PlanEnd { get; set; }
    public DateTime? ActualStart { get; set; }
    public DateTime? ActualEnd { get; set; }
}

/// <summary>
/// 서버가 내려주는 WBS 트리 노드.
/// 원본이 PrimeVue TreeTable 구조 그대로다 — 실제 값은 <see cref="Data"/> 안에 있다.
/// </summary>
public sealed class WbsTreeNode
{
    public string Key { get; set; } = string.Empty;
    public Wbs Data { get; set; } = new();
    public List<WbsTreeNode>? Children { get; set; }
}

/// <summary>WBS 선후행 연결.</summary>
public sealed class WbsLink
{
    public int Id { get; set; }
    public int Source { get; set; }
    public int Target { get; set; }
    public string? Type { get; set; }
}

/// <summary>WBS 다이어그램. <c>diagramData</c> 에 그래프 정의가 문자열로 들어 있다.</summary>
public sealed class WbsDiagram
{
    public int WbsRid { get; set; }
    public string? DiagramData { get; set; }
}

/// <summary>일정. <b>기본키만 uuid(문자열)다</b> — jsini.schedules.id 가 uuid 타입.</summary>
public sealed class Schedule
{
    public string Id { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public DateTime? StartDate { get; set; }
    public DateTime? EndDate { get; set; }
    public int? CompanyId { get; set; }
    /// <summary>특정 회사에 묶이지 않은 공통 일정인지.</summary>
    public bool? IsCommon { get; set; }
    public bool? IsCompleted { get; set; }
    public DateTime? CompletedDate { get; set; }
    public DateTime? CreatedAt { get; set; }
    public string? CreatedBy { get; set; }
}

/// <summary>체크리스트 항목.</summary>
public sealed class Checklist : HdEntity
{
    public string ItemName { get; set; } = string.Empty;
    public string? Category { get; set; }
    public bool? IsChecked { get; set; }
    public DateTime? CompletedAt { get; set; }
    public string? Note { get; set; }
    public int? SortOrder { get; set; }
}

/// <summary>funeralv2 계정 ↔ 헬프데스크 계정 매핑.</summary>
public sealed class AuthUserLink
{
    public int Id { get; set; }
    public string AuthUserId { get; set; } = string.Empty;
    public int HelpdeskUserId { get; set; }
    /// <summary>admin | customer.</summary>
    public string UserType { get; set; } = "admin";
    public string? UserName { get; set; }
    public DateTime? CreatedAt { get; set; }
}

/// <summary>
/// 현재 토큰이 해석된 헬프데스크 신원.
///
/// 두 가지를 구분해 담는다.
/// <list type="bullet">
///   <item><see cref="IsAdmin"/> — <b>무엇을 할 수 있는가.</b> 포털 역할이 관리자면
///     계정 연결이 없어도 참이다. 조회·관리 화면을 열지 결정하는 값이다.</item>
///   <item><see cref="Linked"/> / <see cref="HelpdeskUserId"/> — <b>'내 것'을 가리킬 수
///     있는가.</b> 작성자·담당자·댓글은 헬프데스크 내부 ID 를 참조하므로 연결이
///     없으면 "내가 쓴 것"을 찾을 수 없다.</item>
/// </list>
/// </summary>
public sealed class HelpdeskIdentity
{
    public bool? IsAdmin { get; set; }
    public bool? Linked { get; set; }
    /// <summary>담당자 권한이 계정 연결이 아니라 포털 역할에서 온 것인가.</summary>
    public bool? AdminByRole { get; set; }
    public int? HelpdeskUserId { get; set; }
    public string? CompanyId { get; set; }
    /// <summary>admin | customer | null.</summary>
    public string? LoginType { get; set; }

    /// <summary>보여 줄 이름. 포털 계정 이름을 우선한다.</summary>
    public string? UserName { get; set; }

    /// <summary>헬프데스크 표에 적혀 있는 이름. 포털 이름과 다를 수 있다 — 참고용이다.</summary>
    public string? HelpdeskUserName { get; set; }

    // ── 포털 쪽 신원 ───────────────────────────────────────
    //
    // 서버가 함께 준다. 옛 화면은 이 정보를 얻으려고 포털의
    // `funeral/info/my-info` 를 따로 불렀는데, 그럴 필요가 없다.

    /// <summary>포털 로그인 아이디. 계정 연결의 열쇠가 되는 값이다.</summary>
    public string? JsiniUserId { get; set; }

    public string? JsiniUserName { get; set; }

    public string? JsiniEmail { get; set; }

    /// <summary>
    /// 포털 역할들. <b>담당자 권한이 여기서 올 수 있다</b> —
    /// 계정 연결이 없어도 <c>AdminByRole</c> 이 참이 되는 경로다.
    /// </summary>
    public List<string> JsiniRoles { get; set; } = [];
}

/// <summary>관리자·고객을 합친 사용자 한 줄 (담당자 선택 등에 사용).</summary>
public sealed class HdUser
{
    public int UserId { get; set; }
    public string UserName { get; set; } = string.Empty;
    public string UserType { get; set; } = string.Empty;
}

// ── util (MC 모델 · 파서) ─────────────────────────────────────────

/// <summary>프로토콜 규격(모델).</summary>
public sealed class McModel
{
    public int Id { get; set; }
    public string McName { get; set; } = string.Empty;
    /// <summary>전문 시작을 알리는 키 바이트.</summary>
    public string? StartKey { get; set; }
    public List<ParseItem>? ParseItems { get; set; }
    public List<AckFind>? AckFinds { get; set; }
    public List<BinarySample>? Samples { get; set; }
}

/// <summary>전문 종류를 가려내는 규칙(키 바이트 + 블록 분해 방식).</summary>
public sealed class ParseItem
{
    public int Id { get; set; }
    public string Desc { get; set; } = string.Empty;
    public List<int>? Keys { get; set; }
    public int? KeyIdx { get; set; }
    /// <summary>블록 분해 길이. '8' 또는 '4,2,1,1' 처럼 콤마로 구분.</summary>
    public string? BlocParseLength { get; set; }
    /// <summary>블록 해석 방식 — number 또는 date.</summary>
    public string? BlocParseType { get; set; }
    /// <summary>수신/송신 구분.</summary>
    public string? PTYPE { get; set; }
    public int? MC_ModelsId { get; set; }
    public List<TagItem>? TagItems { get; set; }
}

/// <summary>전문에서 뽑아낼 태그(필드) 정의.</summary>
public sealed class TagItem
{
    public int Id { get; set; }
    public string Desc { get; set; } = string.Empty;
    /// <summary>블록 내 시작 바이트 위치(0부터).</summary>
    public int? TagIdx { get; set; }
    /// <summary>읽을 바이트 수.</summary>
    public int? TagLength { get; set; }
    public string? DataType { get; set; }
    public int? SortNo { get; set; }
    public int? ParseItemId { get; set; }
}

/// <summary>ACK 프레임 판정 규칙.</summary>
public sealed class AckFind
{
    public int Id { get; set; }
    public int? MC_ModelsId { get; set; }
    public string? StartCalcIdx { get; set; }
    public string? StartCalcTarget { get; set; }
    public string? StartCalcArrow { get; set; }
    public string? StartCalcEquals { get; set; }
    public string? StartCalcValue { get; set; }
    public string? EndCalcIdx { get; set; }
    public string? EndCalcTarget { get; set; }
    public string? EndCalcArrow { get; set; }
    public string? EndCalcEquals { get; set; }
    public string? EndCalcValue { get; set; }
}

/// <summary>보관된 전문 샘플. 목록 조회에는 content 가 빠져 있다.</summary>
public sealed class BinarySample
{
    public int Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Content { get; set; }
    public int? MC_ModelsId { get; set; }
    public DateTime? CreatedAt { get; set; }
}

/// <summary>
/// 일정 한 건. HelpDeskServer 의 <c>Schedule</c> 과 짝이다.
/// </summary>
/// <remarks>
/// <b>날짜만 있고 시각이 없다.</b> 서버가 <c>DateTime</c> 으로 들고 있지만
/// 실제로 쓰는 것은 날짜뿐이라, 달력에는 종일 일정으로 그린다 —
/// 시간표로 그리면 모든 일정이 자정에 붙어 한 줄로 겹친다.
/// </remarks>
public sealed class ScheduleDto
{
    public Guid? Id { get; set; }

    public string Title { get; set; } = string.Empty;

    public string? Description { get; set; }

    public DateTime StartDate { get; set; }

    public DateTime EndDate { get; set; }

    /// <summary>공통 일정인가. 참이면 모든 고객사에 보인다.</summary>
    public bool IsCommon { get; set; }

    /// <summary>공통이 아닐 때의 대상 고객사.</summary>
    public int? CompanyId { get; set; }

    public bool IsCompleted { get; set; }

    public DateTime? CompletedDate { get; set; }

    /// <summary>
    /// 달력 부품에 넘길 「종일」 표시.
    ///
    /// <para>
    /// <b>읽기 전용으로 두면 안 된다.</b> DxScheduler 는 약속을 만들 때 이 칸에
    /// 값을 <b>써 넣는다</b> — get 만 있으면 그 자리에서 500 이 난다(실제로 밟았다).
    /// 기본값이 참이고 서버는 이 칸을 모른다.
    /// </para>
    /// </summary>
    public bool AllDay { get; set; } = true;
}

/// <summary>
/// 한 달치 요청 통계. 서버의 <c>MaintenanceReportDto</c> 와 짝이다.
/// </summary>
/// <remarks>
/// SM 모니터링과 월간 보고서가 같은 것을 쓴다 — 한쪽만 고치면 두 화면의
/// 숫자가 달라진다.
/// </remarks>
public sealed class MonthlyReport
{
    public int Year { get; set; }
    public int Month { get; set; }

    /// <summary>그 달에 접수된 건수.</summary>
    public int TotalCreated { get; set; }

    public int AdminCompletedCount { get; set; }
    public int UserCompletedCount { get; set; }
    public int PendingCount { get; set; }
    public int InProgressCount { get; set; }
    public int ConsultationCount { get; set; }
    public int NegotiationCount { get; set; }

    public double ResolutionRate { get; set; }

    public Dictionary<string, int>? RequestsByStatus { get; set; }
    public Dictionary<string, int>? RequestsByType { get; set; }

    /// <summary>평균 해결 시간(시간). SM 계약에서 실제로 따지는 숫자다(MTTR).</summary>
    public double AvgResolutionTime { get; set; }

    public Dictionary<string, int>? ResolutionTimeDistribution { get; set; }

    public List<DailyRequestStat>? DailyStats { get; set; }
}

/// <summary>날짜별 접수·완료 건수.</summary>
public sealed class DailyRequestStat
{
    public int Day { get; set; }
    public int CreatedCount { get; set; }
    public int CompletedCount { get; set; }
}
