namespace JSini.Web.Admin.Api;

public sealed class NoticeDto
{
    public string Id { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public bool IsPopup { get; set; }
    public bool IsPublic { get; set; }
    public int OrderNo { get; set; }
    public int Status { get; set; }
    public DateTime? StartAt { get; set; }
    public DateTime? EndAt { get; set; }
    public DateTime CreatedAt { get; set; }
    public string? AuthorName { get; set; }
}

public sealed class SaveNoticeDto
{
    public string Title { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public bool IsPopup { get; set; }
    public bool IsPublic { get; set; }
    public int OrderNo { get; set; }
    public int Status { get; set; } = 1;
    public DateTime? StartAt { get; set; }
    public DateTime? EndAt { get; set; }
}

// ============================================================
// 포털관리 화면들이 쓰는 자료 모양.
//
// **백엔드 DTO 를 그대로 참조하지 않는다.** 모듈이 마이크로서비스 프로젝트를
// 참조하면 프론트가 백엔드 배포에 묶이고, 백엔드가 내부 정리를 할 때마다
// 화면이 깨진다. 이름과 칸만 맞춘 사본을 둔다 — 실제로 화면이 읽는 칸만.
// ============================================================

/// <summary>포털 계정 한 명. AuthServer 의 <c>AccountDto</c> 와 짝이다.</summary>
public sealed class AccountDto
{
    public string Id { get; set; } = string.Empty;
    public string LoginId { get; set; } = string.Empty;
    public string UserName { get; set; } = string.Empty;
    public string? Email { get; set; }
    public string? Phone { get; set; }

    /// <summary>ACTIVE · LOCKED · RESIGNED.</summary>
    public string Status { get; set; } = "ACTIVE";

    public string? CompanyId { get; set; }
    public string? CompanyName { get; set; }
    public string? DeptId { get; set; }
    public string? DeptName { get; set; }
    public DateTime CreatedAt { get; set; }
    public List<string> RoleNames { get; set; } = [];

    /// <summary>가진 역할의 식별자. 편집 폼이 이 값으로 역할을 고른다.</summary>
    public List<string> RoleIds { get; set; } = [];

    /// <summary>생일. 생활과환경의 생일 화면이 이 값을 읽는다.</summary>
    public DateOnly? BirthDate { get; set; }

    public bool BirthDateIsLunar { get; set; }

    /// <summary>축하 대상인가. 끄면 생일 목록에 나오지 않는다.</summary>
    public bool BirthdayCelebrated { get; set; } = true;

    /// <summary>표에 한 칸으로 보여 줄 역할 이름들.</summary>
    public string RoleText => string.Join(", ", RoleNames);
}

/// <summary>권한 그룹.</summary>
public sealed class RoleDto
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Remark { get; set; }
    public int Status { get; set; } = 1;
    public List<string> Permissions { get; set; } = [];
}

/// <summary>역할이 가진 메뉴별 권한. 저장할 때도 같은 모양으로 돌려보낸다.</summary>
public sealed class RoleMenuDto
{
    public string MenuId { get; set; } = string.Empty;
    public string MenuName { get; set; } = string.Empty;
    public string? ParentId { get; set; }

    public bool CanView { get; set; }
    public bool CanSearch { get; set; }
    public bool CanCreate { get; set; }
    public bool CanUpdate { get; set; }
    public bool CanDelete { get; set; }
    public bool CanPrint { get; set; }
    public bool CanExcel { get; set; }
}

/// <summary>회사.</summary>
public sealed class CompanyDto
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? ShortName { get; set; }
    public string? BusinessNumber { get; set; }
    public string? Representative { get; set; }
    public string? ZipCode { get; set; }
    public string? Address { get; set; }
    public string? AddressDetail { get; set; }
    public string? Remark { get; set; }
    public int Status { get; set; } = 1;
    public int SortOrder { get; set; }

    /// <summary>이 회사에 속한 사람 수. 서버가 세어 준다 — 보내지 않는다.</summary>
    public int UserCount { get; set; }

    /// <summary>이 회사의 부서 수. 서버가 세어 준다.</summary>
    public int DeptCount { get; set; }
}

/// <summary>부서. 트리라서 자식을 안고 온다.</summary>
public sealed class DeptDto
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Pid { get; set; }
    public string? CompanyId { get; set; }
    public string? CompanyName { get; set; }
    public string? Remark { get; set; }
    public int Status { get; set; }
    public int SortOrder { get; set; }

    /// <summary>이 부서에 직접 속한 사람 수.</summary>
    public int UserCount { get; set; }

    /// <summary>하위 부서까지 합한 사람 수.</summary>
    public int TotalUserCount { get; set; }

    public List<DeptDto>? Children { get; set; }
}

/// <summary>다국어 자원 한 줄.</summary>
public sealed class I18nResourceDto
{
    public int Id { get; set; }
    public string Key { get; set; } = string.Empty;
    public string Locale { get; set; } = string.Empty;
    public string Value { get; set; } = string.Empty;
    public string? Category { get; set; }
}

/// <summary>
/// biz-select 설정 한 줄.
///
/// 화면의 드롭다운이 무엇을 어디서 읽을지 정하는 표다. 코드를 고치지 않고
/// 드롭다운을 늘리려고 만든 자리라, 여기가 곧 "메타데이터 관리" 화면이다.
/// </summary>
public sealed class BizSelectConfigDto
{
    public string Id { get; set; } = string.Empty;

    /// <summary>화면이 부르는 이름 (<c>portal_account</c>).</summary>
    public string BizType { get; set; } = string.Empty;

    /// <summary>어느 서비스로 나갈지 (<c>auth</c> · <c>helpdesk</c>).</summary>
    public string ServiceCode { get; set; } = "auth";

    public string ApiUrl { get; set; } = string.Empty;
    public string HttpMethod { get; set; } = "GET";
}

/// <summary>플레이어 배포 상태.</summary>
public sealed class PlayerReleaseDto
{
    /// <summary>배포에 필요한 설정이 갖춰졌는가.</summary>
    public bool Configured { get; set; }

    /// <summary>안 갖춰졌을 때 무엇을 채워야 하는지.</summary>
    public string? SetupHint { get; set; }

    public bool CanRelease { get; set; }
    public string Repository { get; set; } = string.Empty;
    public string Branch { get; set; } = string.Empty;
    public string? HeadSha { get; set; }
}

/// <summary>푸시 발송 요약.</summary>
public sealed class PushStatsDto
{
    public int TotalSent { get; set; }
    public int SuccessCount { get; set; }
    public int FailureCount { get; set; }

    /// <summary>성공률(%). 서버가 계산해 준다.</summary>
    public double SuccessRate { get; set; }
}

/// <summary>푸시 성공률 추이 한 점.</summary>
public sealed class PushTrendPointDto
{
    public string Period { get; set; } = string.Empty;
    public int Sent { get; set; }
    public int Success { get; set; }
    public double SuccessRate { get; set; }
}

/// <summary>실패 사유별 건수.</summary>
public sealed class PushFailureReasonDto
{
    public string Reason { get; set; } = string.Empty;
    public int Count { get; set; }
}

/// <summary>푸시 발송 이력 한 줄.</summary>
public sealed class PushLogDto
{
    public int Id { get; set; }
    public string? Title { get; set; }
    public string? Body { get; set; }
    public string? TargetUser { get; set; }
    public bool Success { get; set; }
    public string? FailureReason { get; set; }
    public DateTime? SentAt { get; set; }
}

/// <summary>알림함의 알림 한 건.</summary>
public sealed class NotificationDto
{
    public int Id { get; set; }
    public string? Title { get; set; }
    public string? Body { get; set; }
    public bool IsRead { get; set; }
    public DateTime? CreatedAt { get; set; }
}

/// <summary>내 알림 수신 설정.</summary>
public sealed class NotificationPreferenceDto
{
    public bool PushEnabled { get; set; }
    public bool EmailEnabled { get; set; }

    /// <summary>방해 금지 시작 시각 (<c>HH:mm</c>). 비면 쓰지 않는다.</summary>
    public string? QuietFrom { get; set; }

    public string? QuietTo { get; set; }
}

/// <summary>등록된 푸시 구독(기기) 하나.</summary>
public sealed class PushSubscriptionDto
{
    public string Id { get; set; } = string.Empty;
    public string? UserAgent { get; set; }
    public DateTime? CreatedAt { get; set; }
    public DateTime? LastUsedAt { get; set; }
}

/// <summary>로그인한 사람의 정보.</summary>
public sealed class UserInfoDto
{
    public string? Id { get; set; }
    public string? UserId { get; set; }
    public string? Username { get; set; }
    public string? RealName { get; set; }
    public string? CompanyName { get; set; }
    public string? DeptName { get; set; }
    public string? Desc { get; set; }
    public List<string> RoleNames { get; set; } = [];
}

/// <summary>내 활동 기록 한 줄.</summary>
public sealed class UserActivityDto
{
    public string? Action { get; set; }
    public string? Detail { get; set; }
    public string? IpAddress { get; set; }
    public DateTime? CreatedAt { get; set; }
}

/// <summary>
/// 배포 현황 한 덩어리.
///
/// 서버가 GitHub 과 도커에서 각각 긁어 합쳐 준다. 둘 중 하나가 실패해도
/// 나머지는 온다 — <c>Github.Error</c> 에 이유가 담긴다.
/// </summary>
public sealed class DeployStatusDto
{
    public string? Repo { get; set; }
    public DateTimeOffset? GeneratedAt { get; set; }
    public GithubStatusDto Github { get; set; } = new();

    /// <summary>도커 컨테이너들. 서버가 소켓에서 읽어 온 그대로다.</summary>
    public List<DockerContainerDto> Docker { get; set; } = [];
}

/// <summary>GitHub 쪽 상태.</summary>
public sealed class GithubStatusDto
{
    /// <summary>읽지 못했을 때의 이유. 정상이면 <c>null</c>.</summary>
    public string? Error { get; set; }

    public List<GithubRunDto> Runs { get; set; } = [];
    public List<GithubRunnerDto> Runners { get; set; } = [];
}

/// <summary>워크플로 실행 한 건.</summary>
public sealed class GithubRunDto
{
    public string? Name { get; set; }
    public string? Status { get; set; }
    public string? Conclusion { get; set; }
    public string? HeadBranch { get; set; }
    public DateTimeOffset? CreatedAt { get; set; }
    public string? HtmlUrl { get; set; }
}

/// <summary>셀프 호스티드 러너 하나.</summary>
public sealed class GithubRunnerDto
{
    public string? Name { get; set; }
    public string? Status { get; set; }
    public bool Busy { get; set; }
}

/// <summary>도커 컨테이너 하나.</summary>
public sealed class DockerContainerDto
{
    public string? Name { get; set; }
    public string? Image { get; set; }
    public string? State { get; set; }
    public string? Status { get; set; }
}


// ── 저장용 DTO ──────────────────────────────────────────────
//
// 조회용과 갈라 둔다. 조회 응답에는 서버가 채워 주는 칸(회사 이름·사람 수·
// 만든 때)이 섞여 있는데, 그것을 그대로 되돌려 보내면 서버가 무시하거나
// 거절한다. **보낼 칸만** 담는다.

/// <summary>계정 등록·수정.</summary>
public sealed class SaveAccountDto
{
    /// <summary>로그인 아이디. <b>등록할 때만</b> 쓴다 — 수정에서는 바꿀 수 없다.</summary>
    public string LoginId { get; set; } = string.Empty;

    public string UserName { get; set; } = string.Empty;
    public string? Email { get; set; }
    public string? Phone { get; set; }

    /// <summary>ACTIVE · LOCKED · RESIGNED.</summary>
    public string Status { get; set; } = "ACTIVE";

    public string? DeptId { get; set; }
    public List<string> RoleIds { get; set; } = [];

    public DateOnly? BirthDate { get; set; }
    public bool BirthDateIsLunar { get; set; }

    /// <summary>축하 대상인가. 끄면 생일 목록에 나오지 않는다.</summary>
    public bool BirthdayCelebrated { get; set; } = true;
}

/// <summary>역할 등록·수정.</summary>
public sealed class SaveRoleDto
{
    /// <summary>
    /// 역할 식별자. 등록할 때 사람이 정한다(<c>ADMIN</c> 처럼).
    ///
    /// 자동 번호가 아닌 이유는 권한 판정과 로그에 그대로 찍히기 때문이다 —
    /// 숫자면 무슨 역할인지 알 수 없다. 수정할 때는 바꾸지 않는다.
    /// </summary>
    public string? Id { get; set; }

    public string Name { get; set; } = string.Empty;
    public string? Remark { get; set; }
    public int Status { get; set; } = 1;
    public List<string> Permissions { get; set; } = [];
}

/// <summary>회사 등록·수정.</summary>
public sealed class SaveCompanyDto
{
    public string Name { get; set; } = string.Empty;
    public string? ShortName { get; set; }
    public string? BusinessNumber { get; set; }
    public string? Representative { get; set; }
    public string? ZipCode { get; set; }
    public string? Address { get; set; }
    public string? AddressDetail { get; set; }
    public string? Remark { get; set; }
    public int Status { get; set; } = 1;
    public int SortOrder { get; set; }
}

/// <summary>부서 등록·수정.</summary>
public sealed class SaveDeptDto
{
    public string Name { get; set; } = string.Empty;

    /// <summary>상위 부서. 비우면 최상위다.</summary>
    public string? Pid { get; set; }

    public string? CompanyId { get; set; }
    public string? Remark { get; set; }
    public int Status { get; set; } = 1;
    public int SortOrder { get; set; }
}


// ── 메뉴 ────────────────────────────────────────────────────
//
// 셸이 쓰는 `MenuNode`(JSini.Web.Models)와 **일부러 다르다.** 그쪽은 사이드바를
// 그리는 데 필요한 것만 담은 읽기 전용 모양이고, 이쪽은 메뉴 관리 화면이
// 고쳐서 되돌려 보내는 모양이라 서버 DTO 를 그대로 따라간다.

/// <summary>메뉴 한 건. 서버의 <c>SystemMenuDto</c> 를 그대로 받는다.</summary>
public sealed class SystemMenuDto
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Path { get; set; } = string.Empty;

    /// <summary>
    /// 옛 Vue 파일 경로. <b>더 이상 읽지 않는다</b> — 라우팅은 Blazor 의
    /// <c>@page</c> 가 정한다. 목록에 보여 주기는 하되 이관 상태를 가늠하는
    /// 참고값일 뿐이다.
    /// </summary>
    public string? Component { get; set; }

    public string? Pid { get; set; }
    public string? Redirect { get; set; }

    /// <summary><c>MENU</c> · <c>CATALOG</c> · <c>BUTTON</c> · <c>EMBEDDED</c> · <c>LINK</c>.</summary>
    public string Type { get; set; } = "MENU";

    public string? AuthCode { get; set; }
    public int Status { get; set; } = 1;

    public SystemMenuMetaDto Meta { get; set; } = new();
    public List<SystemMenuDto>? Children { get; set; }
    public MenuPermissionItemsDto Permissions { get; set; } = new();
}

/// <summary>메뉴의 보이기 관련 값들.</summary>
public sealed class SystemMenuMetaDto
{
    /// <summary>저장된 제목. 다국어 키일 수도 있다.</summary>
    public string? Title { get; set; }

    /// <summary>
    /// 사람이 읽는 제목. <b>서버가 다국어 표를 찾아 넣어 준다.</b>
    /// 못 찾으면 <c>null</c> 이고, 그때는 <see cref="Title"/> 이 이미 글자다.
    /// </summary>
    public string? TitleText { get; set; }

    public string? Icon { get; set; }
    public int Order { get; set; }
    public bool HideInMenu { get; set; }
    public bool HideChildrenInMenu { get; set; }
    public bool HideInBreadcrumb { get; set; }
    public bool HideInTab { get; set; }
    public bool KeepAlive { get; set; } = true;
    public bool AffixTab { get; set; }
    public string? Link { get; set; }
    public string? IframeSrc { get; set; }

    /// <summary>휴대폰에서 이 메뉴를 보여 줄지.</summary>
    public bool UseMobile { get; set; } = true;

    /// <summary>태블릿에서 이 메뉴를 보여 줄지.</summary>
    public bool UseTablet { get; set; } = true;
}

/// <summary>
/// 메뉴가 허용하는 동작들. 화면의 등록·수정·삭제·엑셀 단추가 이 값을 본다
/// (<c>PermissionView</c>).
/// </summary>
public sealed class MenuPermissionItemsDto
{
    public bool UseView { get; set; } = true;
    public bool UseSearch { get; set; } = true;
    public bool UseCreate { get; set; } = true;
    public bool UseUpdate { get; set; } = true;
    public bool UseDelete { get; set; } = true;
    public bool UsePrint { get; set; } = true;
    public bool UseExcel { get; set; } = true;
}

/// <summary>메뉴 등록·수정.</summary>
public sealed class SaveSystemMenuDto
{
    public string Name { get; set; } = string.Empty;
    public string Path { get; set; } = string.Empty;
    public string? Component { get; set; }
    public string? Pid { get; set; }
    public string? Redirect { get; set; }
    public string Type { get; set; } = "MENU";
    public string? AuthCode { get; set; }

    /// <summary>
    /// 0 은 중지, 1 은 사용. <b>일부러 nullable 이다</b> — 값을 안 싣는 요청은
    /// 서버가 상태를 건드리지 않는다.
    /// </summary>
    public int? Status { get; set; } = 1;

    public SystemMenuMetaDto Meta { get; set; } = new();
    public MenuPermissionItemsDto Permissions { get; set; } = new();
}


// ── 공통코드 ────────────────────────────────────────────────

/// <summary>코드 묶음. 「사용 상태」·「사용자 구분」 같은 단위다.</summary>
public sealed class CommonCodeGroupDto
{
    public string Id { get; set; } = string.Empty;
    public string GroupCode { get; set; } = string.Empty;
    public string GroupName { get; set; } = string.Empty;

    /// <summary>여러 단으로 겹칠 수 있는 묶음인지(대분류-중분류-소분류).</summary>
    public bool IsHierarchical { get; set; }

    public int SortOrder { get; set; }
    public string? Remark { get; set; }
    public DateTime CreatedAt { get; set; }
}

/// <summary>묶음 등록·수정.</summary>
public sealed class SaveCommonCodeGroupDto
{
    public string GroupCode { get; set; } = string.Empty;
    public string GroupName { get; set; } = string.Empty;
    public bool IsHierarchical { get; set; }
    public int SortOrder { get; set; }
    public string? Remark { get; set; }
}

/// <summary>코드 한 건.</summary>
public sealed class CommonCodeDto
{
    public string Id { get; set; } = string.Empty;
    public string GroupId { get; set; } = string.Empty;
    public string? ParentId { get; set; }
    public string CodeValue { get; set; } = string.Empty;
    public string CodeName { get; set; } = string.Empty;

    /// <summary>다국어 키. 넣으면 화면이 그 값으로 옮겨 보여 준다.</summary>
    public string? I18nKey { get; set; }

    public int SortOrder { get; set; }
    public int Level { get; set; }
    public bool IsLeaf { get; set; }
    public int Status { get; set; } = 1;
    public string? Remark { get; set; }
    public List<CommonCodeDto>? Children { get; set; }
}

/// <summary>코드 등록·수정.</summary>
public sealed class SaveCommonCodeDto
{
    public string GroupId { get; set; } = string.Empty;
    public string? ParentId { get; set; }
    public string CodeValue { get; set; } = string.Empty;
    public string CodeName { get; set; } = string.Empty;
    public string? I18nKey { get; set; }
    public int SortOrder { get; set; }
    public int Status { get; set; } = 1;
    public string? Remark { get; set; }
}


// ── 배포 도구 ───────────────────────────────────────────────

/// <summary>걸 수 있는 배포 대상 하나.</summary>
public sealed class ReleaseTargetDto
{
    public string Key { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }

    /// <summary>진행 상황을 단계별로 알려 주는 대상인지.</summary>
    public bool ReportsProgress { get; set; }

    public int TimeoutSeconds { get; set; }
    public int EstimatedSeconds { get; set; }

    /// <summary>지금 돌고 있는 실행. 비어 있으면 쉬는 중이다.</summary>
    public string? ActiveRunId { get; set; }

    public ReleaseRunDto? LastRun { get; set; }
}

/// <summary>
/// 배포 대상 목록.
///
/// <b>목록이 아니라 덩어리다</b> — 목록과 함께 "이 사람이 걸 수 있는가"
/// (<see cref="CanRelease"/>)와 설정 경고를 같이 준다. 그 둘이 없으면 화면이
/// 단추를 보여 줄지 말지 판단할 수 없다.
/// </summary>
public sealed class ReleaseTargetListDto
{
    public List<ReleaseTargetDto> Items { get; set; } = [];
    public bool CanRelease { get; set; }
    public string? ConfigWarning { get; set; }
}

/// <summary>배포 실행 한 건.</summary>
public sealed class ReleaseRunDto
{
    public string Id { get; set; } = string.Empty;
    public string TargetKey { get; set; } = string.Empty;
    public string TargetName { get; set; } = string.Empty;

    /// <summary><c>queued</c> · <c>running</c> · <c>succeeded</c> · <c>failed</c> · <c>timeout</c>.</summary>
    public string Status { get; set; } = string.Empty;

    public bool ReportsProgress { get; set; }
    public string? RequestedBy { get; set; }
    public DateTime RequestedAt { get; set; }
    public DateTime? StartedAt { get; set; }
    public DateTime? FinishedAt { get; set; }
    public int? ExitCode { get; set; }
    public string? CurrentStep { get; set; }
    public string? Message { get; set; }
    public string? DeployedVersion { get; set; }
    public int LastSeq { get; set; }

    /// <summary>끝난 실행인지. 참이면 더 물어볼 것이 없다.</summary>
    public bool IsFinal { get; set; }

    public List<ReleaseRunEventDto> Events { get; set; } = [];
}

/// <summary>배포 진행 기록 한 줄.</summary>
public sealed class ReleaseRunEventDto
{
    public int Seq { get; set; }

    /// <summary><c>info</c> · <c>warn</c> · <c>error</c>.</summary>
    public string Level { get; set; } = string.Empty;

    public string? Step { get; set; }
    public string Message { get; set; } = string.Empty;
    public DateTime At { get; set; }
}
