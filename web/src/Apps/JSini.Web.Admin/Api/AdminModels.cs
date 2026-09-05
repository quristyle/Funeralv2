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

    /// <summary>서비스 <b>안쪽</b> 경로. 게이트웨이 접두사는 빼고 적는다.</summary>
    public string ApiUrl { get; set; } = string.Empty;

    public string HttpMethod { get; set; } = "GET";

    // ── 아래 셋이 없으면 드롭다운이 빈 채로 뜬다 ─────────
    //
    // 자료는 오는데 **어느 칸을 보여 주고 어느 칸을 값으로 쓸지** 모르기
    // 때문이다. 화면에서 그 원인이 안 보이면 「서버가 안 준다」로 읽힌다.

    /// <summary>사람에게 보여 줄 칸 이름.</summary>
    public string? LabelField { get; set; }

    /// <summary>저장될 값이 담긴 칸 이름.</summary>
    public string? ValueField { get; set; }

    /// <summary>응답에서 목록이 들어 있는 자리. funeralv2 봉투는 <c>result</c>.</summary>
    public string? ResultPath { get; set; }

    /// <summary>특별한 가공이 필요할 때만 쓰는 처리기 이름.</summary>
    public string? ProcessorType { get; set; }

    /// <summary>늘 함께 보내는 고정 값. JSON 객체 글자다.</summary>
    public string? StaticParams { get; set; }

    /// <summary>화면이 넘긴 값을 본문 어디에 넣을지. 점 표기.</summary>
    public string? ParamPath { get; set; }

    public string? Remark { get; set; }

    public DateTime? CreatedAt { get; set; }
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

    /// <summary>기상 특보 알림을 받는가. 생활과환경이 이 값을 본다.</summary>
    public bool WeatherEnabled { get; set; }

    /// <summary>
    /// 저장된 설정인가. 거짓이면 서버가 준 <b>기본값</b>이라는 뜻이다.
    ///
    /// 화면이 이것을 구별해야 「아직 정한 적 없음」과 「전부 꺼 둠」이
    /// 같아 보이지 않는다.
    /// </summary>
    public bool Saved { get; set; }

    public DateTime? UpdatedAt { get; set; }
}

/// <summary>
/// 내 알림 설정 응답 전체.
///
/// 설정만 오는 것이 아니라 <b>푸시를 쓸 수 있는 환경인지</b>와 등록된 기기까지
/// 함께 온다. 셋이 한 화면에서 같이 쓰이므로 통째로 받는다.
/// </summary>
public sealed class NotificationSettingsDto
{
    /// <summary>설정 주인의 종류(<c>jsini</c> · <c>helpdesk</c>).</summary>
    public string? OwnerType { get; set; }

    public string? OwnerKey { get; set; }

    public NotificationPreferenceDto Preference { get; set; } = new();

    /// <summary>
    /// 서버가 푸시를 보낼 수 있는 상태인가(VAPID 키가 설정돼 있는가).
    /// 거짓이면 스위치를 켜도 아무 일이 일어나지 않는다.
    /// </summary>
    public bool PushAvailable { get; set; }

    /// <summary>브라우저가 구독을 만들 때 쓰는 공개 키.</summary>
    public string? VapidPublicKey { get; set; }

    /// <summary>이 계정으로 등록된 기기들.</summary>
    public List<PushDeviceDto> Devices { get; set; } = [];
}

/// <summary>푸시를 받도록 등록된 기기 하나.</summary>
public sealed class PushDeviceDto
{
    public string? Id { get; set; }
    public string? UserAgent { get; set; }
    public DateTime? CreatedAt { get; set; }
    public DateTime? LastUsedAt { get; set; }
}

/// <summary>
/// 구독 목록 응답. <b>배열이 아니라 <c>{ items, count }</c> 객체다.</b>
/// </summary>
public sealed class PushSubscriptionListDto
{
    public List<PushDeviceDto> Items { get; set; } = [];
    public int Count { get; set; }
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

    // ── 고칠 수 있는 것들 ────────────────────────────────
    //
    // 응답에는 이보다 훨씬 많은 칸이 온다(보안 설정·알림 설정 …).
    // 화면이 쓰는 것만 담는다 — 안 쓰는 칸을 DTO 에 두면 「여기서 고칠 수
    // 있나」로 읽힌다.

    public string? Email { get; set; }
    public string? Phone { get; set; }

    /// <summary>한 줄 소개.</summary>
    public string? Introduction { get; set; }

    /// <summary>생년월일 <c>yyyy-MM-dd</c>. 생일 화면이 이 값을 읽는다.</summary>
    public string? BirthDate { get; set; }

    public bool BirthDateIsLunar { get; set; }

    /// <summary>프로필 사진 주소. 없는 쪽이 흔하다.</summary>
    public string? Avatar { get; set; }

    public DateTime? LastLoginAt { get; set; }
    public string? LastLoginIp { get; set; }
}

/// <summary>
/// 내 정보 저장.
///
/// <b>null 과 빈 문자열이 다르다.</b> 서버가 <c>null</c> 인 칸은 건드리지 않고
/// 빈 문자열은 「지운다」로 읽는다. 그래서 화면이 안 고친 칸을 <c>null</c> 로
/// 두면 그 값이 살아남는다.
/// </summary>
public sealed class UpdateProfileDto
{
    public string? RealName { get; set; }
    public string? Introduction { get; set; }
    public string? Email { get; set; }
    public string? Phone { get; set; }

    /// <summary><c>yyyy-MM-dd</c>. 빈 문자열이면 지운다.</summary>
    public string? BirthDate { get; set; }

    public bool? BirthDateIsLunar { get; set; }
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

    /// <summary>
    /// 도커 쪽. <b>배열이 아니라 객체다.</b>
    ///
    /// 한동안 <c>List&lt;DockerContainerDto&gt;</c> 로 선언해 두어 두 화면이
    /// 「응답을 해석하지 못했습니다」로 끝났다 — 배열 자리에 객체가 오면
    /// System.Text.Json 이 예외를 던지고, 표가 비는 것이 아니라 화면 전체가
    /// 안 뜬다.
    /// </summary>
    public DockerStatusDto Docker { get; set; } = new();
}

/// <summary>도커 쪽 상태. 소켓을 못 읽으면 <see cref="Available"/> 가 거짓이다.</summary>
public sealed class DockerStatusDto
{
    /// <summary>도커 소켓을 읽을 수 있었는가. 거짓이면 목록이 비어 있다.</summary>
    public bool Available { get; set; }

    /// <summary>못 읽었을 때의 이유.</summary>
    public string? Error { get; set; }

    /// <summary>compose 로 뜬 컨테이너들. compose 가 아닌 것은 서버가 걸러 준다.</summary>
    public List<DockerContainerDto> Containers { get; set; } = [];

    public List<DockerImageDto> Images { get; set; } = [];

    /// <summary>이미지가 차지한 용량 합계(MB). 배포 태그가 얼마나 쌓였는지 본다.</summary>
    public long ImagesTotalMb { get; set; }
}

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
    public long Id { get; set; }
    public string? Name { get; set; }

    /// <summary><c>queued</c> · <c>in_progress</c> · <c>completed</c>.</summary>
    public string? Status { get; set; }

    /// <summary><c>success</c> · <c>failure</c> · <c>cancelled</c>. 진행 중이면 <c>null</c>.</summary>
    public string? Conclusion { get; set; }

    /// <summary>서버가 <c>branch</c> 로 보낸다 — <c>head_branch</c> 가 아니다.</summary>
    public string? Branch { get; set; }

    public string? Sha { get; set; }

    /// <summary>무엇이 이 실행을 걸었는가 (<c>push</c> · <c>workflow_dispatch</c>).</summary>
    public string? Event { get; set; }

    /// <summary>건 사람의 GitHub 아이디.</summary>
    public string? Actor { get; set; }

    /// <summary>커밋 제목. 어느 변경의 배포인지 이것으로 안다.</summary>
    public string? Title { get; set; }

    public DateTimeOffset? StartedAt { get; set; }
    public DateTimeOffset? UpdatedAt { get; set; }

    /// <summary>걸린 시간(초). 아직 시작하지 않았으면 <c>null</c>.</summary>
    public double? DurationSec { get; set; }

    public string? HtmlUrl { get; set; }
}

public sealed class GithubRunnerDto
{
    public string? Name { get; set; }

    /// <summary><c>online</c> · <c>offline</c>.</summary>
    public string? Status { get; set; }

    public bool Busy { get; set; }

    /// <summary>러너에 붙은 꼬리표(<c>self-hosted</c> · <c>Linux</c> · <c>prod</c>).</summary>
    public List<string> Labels { get; set; } = [];
}

/// <summary>컨테이너 한 개.</summary>
public sealed class DockerContainerDto
{
    /// <summary>compose 서비스 이름. <b>서버가 <c>service</c> 로 보낸다.</b></summary>
    public string? Service { get; set; }

    public string? Project { get; set; }
    public string? Image { get; set; }

    /// <summary>이미지 태그. 어느 판이 떠 있는지 이것으로 본다.</summary>
    public string? Tag { get; set; }

    /// <summary><c>running</c> · <c>exited</c> · <c>restarting</c>.</summary>
    public string? State { get; set; }

    /// <summary>사람이 읽는 가동 문구 (<c>Up 3 days</c>).</summary>
    public string? Status { get; set; }

    public DateTimeOffset? CreatedAt { get; set; }
}

/// <summary>쌓여 있는 이미지 한 개. <c>funeralv2-*</c> 만 담긴다.</summary>
public sealed class DockerImageDto
{
    /// <summary>저장소:태그 전체 이름. 서버가 한 칸으로 보낸다.</summary>
    public string? Name { get; set; }

    public long SizeMb { get; set; }

    /// <summary>지금 어떤 컨테이너가 쓰고 있는가. 거짓이면 지워도 되는 것이다.</summary>
    public bool InUse { get; set; }

    public DateTimeOffset? CreatedAt { get; set; }
}

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


// ── 게이트웨이 상태 ─────────────────────────────────────────

/// <summary>
/// <c>gateway/status</c> 응답.
///
/// **봉투에 <c>result</c> 한 겹이 없다** — <c>data</c> 가 곧 이 객체다.
/// 그래서 <c>GetFlexibleAsync</c> 로 읽는다.
/// </summary>
public sealed class GatewayStatusDto
{
    public ServiceHealth? Gateway { get; set; }

    /// <summary>게이트웨이가 아는 서비스들. 화면이 목록을 들고 있지 않다.</summary>
    public List<ServiceHealth> Services { get; set; } = [];
}

/// <summary>서비스 하나를 눌러 본 결과.</summary>
public sealed class ServiceHealth
{
    /// <summary>게이트웨이의 클러스터 이름(<c>auth-cluster</c>). 없으면 게이트웨이 자신이다.</summary>
    public string? Cluster { get; set; }

    public string? Destination { get; set; }
    public string? Address { get; set; }

    /// <summary><c>UP</c> · <c>DOWN</c>.</summary>
    public string? Status { get; set; }

    public int? HttpStatus { get; set; }

    /// <summary>왕복 시간(ms). 살아 있어도 느린 것을 이것으로 본다.</summary>
    public int? LatencyMs { get; set; }

    public string? Error { get; set; }
    public string? Reason { get; set; }

    /// <summary>그 서비스가 스스로 보고한 의존성(DB · 메시지 큐).</summary>
    public List<ServiceDependency> Dependencies { get; set; } = [];

    /// <summary>표에 보여 줄 이름. 클러스터 이름에서 접미사를 뗀다.</summary>
    public string Name => Cluster is { Length: > 0 } c
        ? (c.EndsWith("-cluster", StringComparison.OrdinalIgnoreCase) ? c[..^8] : c)
        : "gateway";
}

/// <summary>서비스가 보고한 의존성 하나.</summary>
public sealed class ServiceDependency
{
    public string? Name { get; set; }

    /// <summary><c>Healthy</c> · <c>Degraded</c> · <c>Unhealthy</c>.</summary>
    public string? Status { get; set; }

    public string? Description { get; set; }
    public double? DurationMs { get; set; }
}


// ── 역할 범위 (회사 · 부서 · 사람) ──────────────────────────
//
// 역할은 **세 단계에 걸 수 있고 합쳐서 적용된다.** 덮어쓰지 않는다.
//
//     회사에 걸면   그 회사 모든 사람에게
//     부서에 걸면   그 부서 사람에게
//     사람에 걸면   그 사람에게만
//
// 그래서 「사람에서 뺐는데 왜 아직 있지」가 생긴다 — 부서나 회사에서 온
// 것이기 때문이다. 화면이 **어디서 왔는지**를 함께 보여 줘야 하는 이유다.

/// <summary>역할을 걸 수 있는 대상 한 칸. 회사·부서·사람이 같은 모양이다.</summary>
public sealed class RoleScopeNodeDto
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;

    /// <summary>계정인 경우의 로그인 아이디.</summary>
    public string? LoginId { get; set; }

    /// <summary><c>company</c> · <c>department</c> · <c>account</c>.</summary>
    public string Kind { get; set; } = string.Empty;

    /// <summary>이 대상에 <b>직접</b> 걸린 역할. 물려받은 것은 없다.</summary>
    public List<string> RoleIds { get; set; } = [];

    public List<RoleScopeNodeDto> Children { get; set; } = [];

    /// <summary>이 부서(또는 회사) 소속 사람.</summary>
    public List<RoleScopeNodeDto> Accounts { get; set; } = [];
}

/// <summary>회사 하나의 조직 나무.</summary>
public sealed class RoleScopeTreeDto
{
    public RoleScopeNodeDto Company { get; set; } = new();
}

/// <summary>어떤 계정에 실제로 적용되는 역할과, 각 역할이 어느 단계에서 왔는지.</summary>
public sealed class EffectiveRolesDto
{
    public List<string> RoleIds { get; set; } = [];

    /// <summary><see cref="RoleIds"/> 와 같은 순서의 표시 이름.</summary>
    public List<string> RoleNames { get; set; } = [];

    /// <summary>
    /// 역할 식별자 → 그 역할이 온 단계들(<c>company</c> · <c>department</c> · <c>account</c>).
    ///
    /// 한 역할이 여러 단계에 걸려 있을 수 있어 목록이다.
    /// </summary>
    public Dictionary<string, List<string>> Sources { get; set; } = [];
}

/// <summary>어떤 계정이 볼 수 있는 메뉴와 볼 수 없는 메뉴.</summary>
public sealed class AccountMenuAccessDto
{
    public List<AccountMenuItemDto> Assigned { get; set; } = [];
    public List<AccountMenuItemDto> Unassigned { get; set; } = [];
}

/// <summary>메뉴 한 칸과, 그 메뉴를 열어 준 역할.</summary>
public sealed class AccountMenuItemDto
{
    public string Id { get; set; } = string.Empty;
    public string Path { get; set; } = string.Empty;

    /// <summary>제목. 다국어 키일 수 있다.</summary>
    public string? Title { get; set; }

    public string Type { get; set; } = string.Empty;

    /// <summary>상위 메뉴를 이어 붙인 길. 어느 업무의 메뉴인지 이것으로 안다.</summary>
    public string? Breadcrumb { get; set; }

    /// <summary>이 메뉴를 열어 준 역할들. 닫힌 메뉴면 비어 있다.</summary>
    public List<string> GrantedBy { get; set; } = [];
}

/// <summary>검색용 사람 한 칸. 회사·부서 이름까지 담아 한 줄로 찾게 한다.</summary>
public sealed class AccountPickDto
{
    public string Id { get; set; } = string.Empty;
    public string LoginId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;

    public string? CompanyId { get; set; }
    public string? CompanyName { get; set; }
    public string? DepartmentId { get; set; }
    public string? DepartmentName { get; set; }

    /// <summary>
    /// 프로필 사진 주소. <b>없는 쪽이 정상이다</b> — 화면은 이름 첫 글자로
    /// 대신 그리고, 사진이 없는 것을 오류처럼 보이게 하지 않는다.
    /// </summary>
    public string? Avatar { get; set; }

    /// <summary>검색에 쓰는 한 줄. 아이디·부서·회사 어느 것으로 쳐도 걸린다.</summary>
    public string SearchText => $"{Name} {LoginId} {DepartmentName} {CompanyName}";
}

/// <summary>역할 배정·해제 요청.</summary>
public sealed class RoleAssignRequest
{
    /// <summary><c>company</c> · <c>department</c> · <c>account</c>.</summary>
    public string Kind { get; set; } = string.Empty;

    public string TargetId { get; set; } = string.Empty;
    public string RoleId { get; set; } = string.Empty;
}
