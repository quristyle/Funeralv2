namespace JSini.Web.Admin.Api;

// 공지 DTO 는 여기 없다. **레이아웃과 로그인 화면도 쓰기 때문에**
// JSini.Web.Models 로 올렸다(`Notice.cs`) — 셸은 업무 모듈을 이름으로 알지
// 못하므로 이 모듈에 두면 관리 화면만 쓸 수 있다.

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

    /// <summary>
    /// 프로필 사진 주소. 계정 확장 속성(<c>account_profile_details</c> 의
    /// <c>Avatar</c>)이라 <b>DB 에 <c>/api/file/…</c> 상대경로로 들어 있다</b> —
    /// 화면에 걸 때 <see cref="JSini.Web.Components.Data.FileDownload.RelayUrl"/>
    /// 를 씌운다. 조직도가 사람 노드에 쓴다.
    /// </summary>
    public string? Avatar { get; set; }

    /// <summary>가진 역할의 식별자. 편집 폼이 이 값으로 역할을 고른다.</summary>
    public List<string> RoleIds { get; set; } = [];

    /// <summary>생일. 생활과환경의 생일 화면이 이 값을 읽는다.</summary>
    public DateOnly? BirthDate { get; set; }

    public bool BirthDateIsLunar { get; set; }

    /// <summary>축하 대상인가. 끄면 생일 목록에 나오지 않는다.</summary>
    public bool BirthdayCelebrated { get; set; } = true;

    /// <summary>
    /// 등록할 때 서버가 발급한 첫 비밀번호. <b>등록 응답에만 담긴다</b> —
    /// 목록에는 언제나 <c>null</c> 이다.
    ///
    /// <para>
    /// 서버는 해시만 들고 있으므로 <b>이 값을 놓치면 다시 알 방법이 없다.</b>
    /// 화면이 등록 직후 한 번 띄워서 사람이 옮겨 적게 한다.
    /// </para>
    /// </summary>
    public string? InitialPassword { get; set; }

    /// <summary>표에 한 칸으로 보여 줄 역할 이름들.</summary>
    public string RoleText => string.Join(", ", RoleNames);

    /// <summary>
    /// 편집 폼의 역할 고르개가 묶이는 자리. <see cref="RoleIds"/> 와 같은 값이다.
    ///
    /// <para>
    /// [왜 같은 값을 두 번 두는가 — 팝업이 열리지 않았다]
    /// </para>
    ///
    /// <para>
    /// DevExpress 편집기는 팝업 안에서 검증 표현식(<c>ValuesExpression</c>)을
    /// 요구하는데, 그 식이 <b>순수한 멤버 접근이어야 한다.</b> 형이 안 맞아
    /// <c>(IEnumerable&lt;string&gt;)a.RoleIds</c> 로 적으면 식에 형변환
    /// 마디가 끼어 이렇게 거절한다 —
    /// </para>
    ///
    /// <code>
    /// The provided expression contains a UnaryExpression which is not supported.
    /// </code>
    ///
    /// <para>
    /// <b>화면에는 아무 표시도 나지 않는다.</b> 등록 단추를 눌러도 팝업이
    /// 그냥 안 열리고, 원인은 브라우저 콘솔에만 있다. 형이 맞는 창을
    /// 하나 내면 화면은 <c>@@bind-Values="a.Roles"</c> 한 줄로 끝난다.
    /// </para>
    /// </summary>
    public IEnumerable<string> Roles
    {
        get => RoleIds;
        set => RoleIds = [.. value];
    }

    /// <summary>
    /// 편집 폼의 생일 칸이 묶이는 자리. <see cref="BirthDate"/> 와 같은 값이다.
    ///
    /// <para>
    /// 자료는 <c>DateOnly</c> 인데 <c>DxDateEdit</c> 은 <c>DateTime?</c> 을
    /// 다룬다. 옮기는 일을 화면에서 메서드로 하면 <see cref="Roles"/> 와 같은
    /// 이유로 거절당한다 — 메서드 호출도 멤버 접근이 아니다.
    /// </para>
    /// </summary>
    public DateTime? BirthDateTime
    {
        get => BirthDate?.ToDateTime(TimeOnly.MinValue);
        set => BirthDate = value is null ? null : DateOnly.FromDateTime(value.Value);
    }
}

/// <summary>권한 그룹.</summary>
public sealed class RoleDto
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Remark { get; set; }
    public int Status { get; set; } = 1;
    public List<string> Permissions { get; set; } = [];

    /// <summary>
    /// 편집 폼의 「사용」 스위치가 묶이는 자리.
    /// <c>JSini.Web.Models.NoticeDto.IsActive</c> 와 같은 이유다.
    /// </summary>
    public bool IsActive
    {
        get => Status == 1;
        set => Status = value ? 1 : 0;
    }
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

    // ── 사용자 정의 권한 여덟 개 ────────────────────────────
    //
    // **이 칸들이 없어서 저장할 때마다 지워지고 있었다.** 저장은 목록을
    // 통째로 덮어쓰는데, 읽을 때 버린 값은 보낼 때 false 가 된다.

    public bool CanCust1 { get; set; }
    public bool CanCust2 { get; set; }
    public bool CanCust3 { get; set; }
    public bool CanCust4 { get; set; }
    public bool CanCust5 { get; set; }
    public bool CanCust6 { get; set; }
    public bool CanCust7 { get; set; }
    public bool CanCust8 { get; set; }

    // ── 이 메뉴가 쓰는 권한 항목 (읽기 전용) ─────────────────
    //
    // 메뉴가 안 쓴다고 지정한 항목은 역할에 켜 두어도 효과가 없다 —
    // 서버가 메뉴의 `use_*` 와 AND 로 묶는다. 켜도 아무 일이 없는 칸을
    // 보여 주면 「켰는데 왜 안 되지」로 헤매게 된다.

    public bool UseView { get; set; }
    public bool UseSearch { get; set; }
    public bool UseCreate { get; set; }
    public bool UseUpdate { get; set; }
    public bool UseDelete { get; set; }
    public bool UsePrint { get; set; }
    public bool UseExcel { get; set; }
    public bool UseCust1 { get; set; }
    public bool UseCust2 { get; set; }
    public bool UseCust3 { get; set; }
    public bool UseCust4 { get; set; }
    public bool UseCust5 { get; set; }
    public bool UseCust6 { get; set; }
    public bool UseCust7 { get; set; }
    public bool UseCust8 { get; set; }

    /// <summary>사용자 정의 항목의 이름. 메뉴 관리 화면에서 붙인다.</summary>
    public string? Cust1Name { get; set; }
    public string? Cust2Name { get; set; }
    public string? Cust3Name { get; set; }
    public string? Cust4Name { get; set; }
    public string? Cust5Name { get; set; }
    public string? Cust6Name { get; set; }
    public string? Cust7Name { get; set; }
    public string? Cust8Name { get; set; }
}

// ── 메뉴 기준 권한 현황 (`auth/system/menu-role/{menuId}`) ──────
//
// 역할 관리는 **역할**에서 출발한다 — 「이 역할은 어떤 메뉴를 쓰나」.
// 이쪽은 반대다 — **「이 메뉴는 누가 쓸 수 있나」**. 같은 자료를 거꾸로 훑는다.
//
// 읽기 전용이다. 저장은 이미 있는 경로를 그대로 쓴다
// (`role-permission/.../menus/save` · `role-scope/remove`) —
// 같은 일을 하는 저장 경로를 둘로 만들면 한쪽에만 규칙이 붙는다.

/// <summary>메뉴 하나를 기준으로 본 권한 현황.</summary>
public sealed class MenuRoleDto
{
    public string MenuId { get; set; } = string.Empty;
    public string MenuName { get; set; } = string.Empty;
    public string? MenuPath { get; set; }

    /// <summary>이 메뉴가 실제로 쓰는 권한 항목.</summary>
    public MenuUsedPermissionDto Used { get; set; } = new();

    /// <summary>
    /// 모든 역할과 이 메뉴에 대한 권한.
    ///
    /// <b>걸려 있지 않은 역할도 담겨 온다.</b> 화면에서 새로 켜 줄 수 있어야
    /// 하는데 걸린 것만 주면 「없는 역할」을 고를 방법이 없다.
    /// </summary>
    public List<MenuRoleGrantDto> Roles { get; set; } = [];

    public List<MenuRoleTargetDto> Companies { get; set; } = [];
    public List<MenuRoleTargetDto> Departments { get; set; } = [];
    public List<MenuRoleTargetDto> Accounts { get; set; } = [];

    /// <summary>
    /// 이 메뉴를 실제로 열람할 수 있는 사람 수.
    ///
    /// 목록의 건수를 더한 값과 <b>다를 수 있다</b> — 한 사람이 회사와 부서
    /// 양쪽에서 걸릴 수 있어 사람 단위로 중복을 없앤 값이다.
    /// </summary>
    public int EffectiveUserCount { get; set; }
}

/// <summary>메뉴가 실제로 쓰는 권한 항목 (<c>system_menus.use_*</c>).</summary>
public sealed class MenuUsedPermissionDto
{
    public bool View { get; set; }
    public bool Search { get; set; }
    public bool Create { get; set; }
    public bool Update { get; set; }
    public bool Delete { get; set; }
    public bool Print { get; set; }
    public bool Excel { get; set; }
    public bool Cust1 { get; set; }
    public bool Cust2 { get; set; }
    public bool Cust3 { get; set; }
    public bool Cust4 { get; set; }
    public bool Cust5 { get; set; }
    public bool Cust6 { get; set; }
    public bool Cust7 { get; set; }
    public bool Cust8 { get; set; }

    public string? Cust1Name { get; set; }
    public string? Cust2Name { get; set; }
    public string? Cust3Name { get; set; }
    public string? Cust4Name { get; set; }
    public string? Cust5Name { get; set; }
    public string? Cust6Name { get; set; }
    public string? Cust7Name { get; set; }
    public string? Cust8Name { get; set; }
}

/// <summary>역할 하나가 이 메뉴에 대해 가진 권한.</summary>
public sealed class MenuRoleGrantDto
{
    public string RoleId { get; set; } = string.Empty;
    public string RoleName { get; set; } = string.Empty;

    /// <summary>이 역할에 이 메뉴 권한이 한 줄이라도 걸려 있는가.</summary>
    public bool Granted { get; set; }

    public bool CanView { get; set; }
    public bool CanSearch { get; set; }
    public bool CanCreate { get; set; }
    public bool CanUpdate { get; set; }
    public bool CanDelete { get; set; }
    public bool CanPrint { get; set; }
    public bool CanExcel { get; set; }
    public bool CanCust1 { get; set; }
    public bool CanCust2 { get; set; }
    public bool CanCust3 { get; set; }
    public bool CanCust4 { get; set; }
    public bool CanCust5 { get; set; }
    public bool CanCust6 { get; set; }
    public bool CanCust7 { get; set; }
    public bool CanCust8 { get; set; }

    /// <summary>이 역할이 걸린 회사·부서·사람 수. 「이 역할을 끄면 몇이 영향받나」다.</summary>
    public int CompanyCount { get; set; }
    public int DepartmentCount { get; set; }
    public int AccountCount { get; set; }
}

/// <summary>이 메뉴에 닿는 대상 하나 (회사 · 부서 · 사람).</summary>
public sealed class MenuRoleTargetDto
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;

    /// <summary>부서·사람이면 소속 회사명. 같은 이름이 여러 회사에 있다.</summary>
    public string? CompanyName { get; set; }

    /// <summary>사람이면 로그인 아이디.</summary>
    public string? LoginId { get; set; }

    /// <summary>어느 역할 때문에 닿는지. 여럿일 수 있다.</summary>
    public List<string> ViaRoleNames { get; set; } = [];

    /// <summary>어느 역할 때문에 닿는지 (식별자). 해제 요청에 쓴다.</summary>
    public List<string> ViaRoleIds { get; set; } = [];

    /// <summary>이 대상에 딸린 사람 수. 회사면 회사 인원, 사람이면 1.</summary>
    public int UserCount { get; set; }
}

/// <summary>
/// 역할에 걸린 사람 한 줄 (<c>role-permission/roles/{id}/users</c>).
///
/// <para>
/// <b><see cref="AccountDto"/> 로 받으면 안 된다.</b> 이름은 비슷한데 모양이
/// 다르다 — 이쪽 <c>roleNames</c> 는 <b>문자열 하나</b>이고 저쪽은 목록이다.
/// 그대로 받으면 JSON 을 푸는 자리에서 터진다.
/// </para>
/// </summary>
public sealed class RoleUserDto
{
    public string Id { get; set; } = string.Empty;
    public string LoginId { get; set; } = string.Empty;
    public string UserName { get; set; } = string.Empty;
    public string? Email { get; set; }
    public string? Phone { get; set; }
    public string? DeptName { get; set; }
    public string? CompanyName { get; set; }

    /// <summary>가진 역할의 식별자들.</summary>
    public List<string> Roles { get; set; } = [];

    /// <summary>가진 역할의 이름을 이어 붙인 것. <b>목록이 아니라 한 줄이다.</b></summary>
    public string? RoleNames { get; set; }
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

    public DateTime? ApprovalDate { get; set; }

    /// <summary>
    /// 이 회사를 어느 업무 시스템에 노출할지 (<c>COMPANY_USAGE_LOCATION</c> 의 코드값).
    ///
    /// <b>목록을 좁히는 열쇠다.</b> 장례식장 화면들은
    /// <c>FUNERAL_HOME_MANAGEMENT_SYSTEM</c> 이 걸린 회사만 읽는다.
    /// 비워 두면 그 화면들의 회사 드롭다운에서 사라진다.
    /// </summary>
    public List<string> UsageLocations { get; set; } = [];

    /// <summary>
    /// 편집 폼의 사용처 고르개가 묶이는 자리. <see cref="AccountDto.Roles"/> 와 같은 이유다 —
    /// 형이 안 맞으면 팝업이 말없이 안 열린다.
    /// </summary>
    public IEnumerable<string> Usages
    {
        get => UsageLocations;
        set => UsageLocations = [.. value];
    }

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

/// <summary>
/// 로그인한 사람의 정보. <c>GET auth/user/info</c> 가 주는 것 <b>전부</b>다.
/// </summary>
/// <remarks>
/// <para>
/// 한동안 「화면이 쓰는 칸만 담는다」고 열몇 개만 적어 두었다. 그 판단이
/// 여기서는 틀렸다 — 이 응답은 <b>프로필 화면 하나가 통째로 쓰는 자료</b>고,
/// 빠뜨린 칸이 곧 옮기지 못한 기능이었다. 보안 설정 넷 · 알림 설정 셋 ·
/// 비밀번호 만료 다섯 · 역할 식별자 · 이관 출처 · 사진 그룹이 전부 그렇게
/// 빠져 있었다.
/// </para>
/// <para>
/// 서버 쪽 정본은 AuthServer 의 <c>DTOs/UserInfoDto.cs</c> 다.
/// </para>
/// </remarks>
public sealed class UserInfoDto
{
    public string? Id { get; set; }
    public string? UserId { get; set; }
    public string? Username { get; set; }
    public string? RealName { get; set; }
    public string? CompanyName { get; set; }
    public string? DeptName { get; set; }
    public string? Desc { get; set; }

    /// <summary>로그인 뒤 갈 기본 화면. 서버가 정해 준다.</summary>
    public string? HomePath { get; set; }

    /// <summary>역할 식별자(<c>ADMINISTRATOR</c>). 사람에게 보여 줄 값이 아니다.</summary>
    public List<string> Roles { get; set; } = [];

    /// <summary>역할 이름(<c>관리자</c>). 화면에는 이것을 쓴다.</summary>
    public List<string> RoleNames { get; set; } = [];

    /// <summary>
    /// 이 계정이 어느 MSA 레코드에서 왔는지(<c>helpdesk:admin:4</c>).
    /// 이관으로 만들어진 계정만 값이 있다.
    /// </summary>
    public string? MsaSource { get; set; }

    // ── 고칠 수 있는 것들 ────────────────────────────────

    public string? Email { get; set; }
    public string? Phone { get; set; }

    /// <summary>한 줄 소개.</summary>
    public string? Introduction { get; set; }

    /// <summary>생년월일 <c>yyyy-MM-dd</c>. 생일 화면이 이 값을 읽는다.</summary>
    public string? BirthDate { get; set; }

    public bool BirthDateIsLunar { get; set; }

    /// <summary>
    /// 프로필 사진 주소. 서버가 값이 없으면 <b>바깥 기본 이미지 주소</b>를
    /// 채워 준다 — 비어 오는 일이 없다.
    /// </summary>
    public string? Avatar { get; set; }

    /// <summary>사진이 담긴 파일 그룹. 「프로필 사진 관리」가 이 그룹을 다룬다.</summary>
    public string? AvatarGroupId { get; set; }

    // ── 보안 설정 (켬·끔) ────────────────────────────────
    //
    // 저장은 `auth/user/settings` 로 한 칸씩 한다. 저장 이름은 여기 속성
    // 이름과 같다(`SecurityPhone` …) — 서버가 그 글자를 DetailType 으로
    // 그대로 쓴다.

    public bool SecurityPhone { get; set; }
    public bool SecurityQuestion { get; set; }
    public bool SecurityEmail { get; set; }
    public bool SecurityMfa { get; set; }

    // ── 알림 설정 (켬·끔) ────────────────────────────────

    public bool SystemMessage { get; set; }
    public bool TodoTask { get; set; }
    public bool AccountPasswordNotify { get; set; }

    // ── 계정 이력 (읽기 전용) ────────────────────────────

    /// <summary>가입일.</summary>
    public DateTime? CreatedAt { get; set; }

    public DateTime? LastLoginAt { get; set; }
    public string? LastLoginIp { get; set; }

    /// <summary>비밀번호를 마지막으로 바꾼 시각.</summary>
    public DateTime? PasswordChangedAt { get; set; }

    /// <summary>비밀번호가 만료되는 시각. 정책이 꺼져 있으면 null.</summary>
    public DateTime? PasswordExpiresAt { get; set; }

    /// <summary>만료 기준 일수(기본 90). <b>null 이면 정책이 꺼져 있다.</b></summary>
    public int? PasswordExpiryDays { get; set; }

    /// <summary>만료까지 남은 일수. 이미 지났으면 0.</summary>
    public int? PasswordDaysRemaining { get; set; }

    public bool PasswordExpired { get; set; }

    /// <summary>만료 정책이 켜져 있는가. 꺼져 있으면 남은 일수 칸 자체가 뜻이 없다.</summary>
    public bool PasswordPolicyOn => PasswordExpiryDays is > 0;

    /// <summary>역할을 보여 줄 글자. 이름이 없으면 식별자를 쓴다.</summary>
    public IReadOnlyList<string> RoleLabels => RoleNames.Count > 0 ? RoleNames : Roles;
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

    /// <summary>대표 사진 주소. 사진 관리 탭이 대표를 바꿀 때만 싣는다.</summary>
    public string? Avatar { get; set; }

    /// <summary>사진 그룹. 처음 올릴 때 서버가 새로 발급해 준다.</summary>
    public string? AvatarGroupId { get; set; }
}

/// <summary>
/// 켬·끔 하나를 저장한다 (<c>POST auth/user/settings</c>).
/// </summary>
/// <remarks>
/// <c>FieldName</c> 이 그대로 <c>account_profile_details.detail_type</c> 이 된다.
/// 그래서 <b>철자가 곧 계약</b>이다 — <c>SecurityPhone</c> 을 <c>securityPhone</c>
/// 으로 보내면 다른 칸이 하나 더 생기고, 조회는 여전히 옛 칸을 보므로
/// <b>저장은 성공하는데 값이 안 바뀐 것처럼 보인다.</b>
/// </remarks>
public sealed class UpdateSettingDto
{
    public string FieldName { get; set; } = string.Empty;
    public bool Value { get; set; }
}

/// <summary>접속 기록 한 줄.</summary>
public sealed class LoginLogDto
{
    /// <summary>기록된 시각. <b>UTC 다</b> — 표에는 <see cref="AtLocal"/> 을 쓴다.</summary>
    public DateTime? At { get; set; }

    /// <summary>
    /// 우리 시간대로 옮긴 시각. <b>표가 거는 칸은 이쪽이다.</b>
    /// </summary>
    /// <remarks>
    /// <c>At</c> 를 그대로 걸었더니 <b>표만 아홉 시간 뒤처져 보였다.</b>
    /// 같은 화면 위쪽의 「최근 로그인」은 <c>ToLocalTime()</c> 을 거쳐 21:20 인데
    /// 표의 같은 줄이 12:20 이었다 — 그리드는 받은 <c>DateTime</c> 을 그대로
    /// 그리고 시간대를 옮겨 주지 않는다. 두 값이 나란히 있어서 눈에 띄었지
    /// 표만 있었으면 못 봤을 종류의 어긋남이다.
    /// </remarks>
    public DateTime? AtLocal => At?.ToLocalTime();

    public bool Success { get; set; }

    /// <summary>실패 이유(<c>BAD_PASSWORD</c> · <c>NOT_FOUND</c>). 성공이면 null.</summary>
    public string? FailReason { get; set; }

    public string? Ip { get; set; }

    /// <summary>브라우저·기기 원문. 길어서 표에는 <see cref="Device"/> 를 쓴다.</summary>
    public string? UserAgent { get; set; }

    /// <summary>사람이 읽게 줄인 기기 이름(<c>Chrome · Windows</c>).</summary>
    public string? Device { get; set; }

    /// <summary>표에 그릴 결과 글자.</summary>
    public string ResultLabel => Success
        ? "성공"
        : FailReason switch
        {
            "BAD_PASSWORD" => "비밀번호 불일치",
            "NOT_FOUND" => "없는 아이디",
            _ => "실패",
        };

    /// <summary>표에 그릴 기기 이름. 원문조차 없으면 「기록 없음」.</summary>
    public string DeviceLabel => string.IsNullOrWhiteSpace(Device)
        ? (string.IsNullOrWhiteSpace(UserAgent) ? "기록 없음" : "알 수 없는 기기")
        : Device;
}

/// <summary>
/// 계정 활동. <c>GET auth/user/activity</c> 가 <b>객체 하나</b>로 준다.
/// </summary>
/// <remarks>
/// <para>
/// <b>배열이 아니다.</b> 한동안 이 자리를 <c>UserActivityDto</c> 목록
/// (<c>Action</c>·<c>Detail</c>·<c>IpAddress</c>)으로 적어 두었는데 서버에는
/// 그런 칸이 없다. 봉투는 객체 하나도 <c>result: [obj]</c> 로 싣기 때문에
/// 목록으로 읽어도 <b>예외가 나지 않는다</b> — 칸이 전부 <c>null</c> 인 줄이
/// 하나 그려질 뿐이라 「기록이 아직 없나 보다」로 읽혔다.
/// </para>
/// <para>
/// 자기 것만 볼 수 있다. 조회할 계정을 요청에 싣지 않고 게이트웨이가 넘긴
/// 신원을 서버가 쓴다.
/// </para>
/// </remarks>
public sealed class AccountActivityDto
{
    /// <summary>로그인 성공 횟수 (기록이 쌓이기 시작한 뒤부터).</summary>
    public int LoginCount { get; set; }

    /// <summary>지난번 접속. 지금 이 접속의 바로 앞이다.</summary>
    public LoginLogDto? PreviousLogin { get; set; }

    /// <summary>최근 30일 안의 로그인 실패 횟수.</summary>
    public int RecentFailCount { get; set; }

    public LoginLogDto? LastFail { get; set; }

    /// <summary>이 계정을 써 온 일수.</summary>
    public int AccountAgeDays { get; set; }

    /// <summary>최근 접속 기록. 최신 순.</summary>
    public List<LoginLogDto> Recent { get; set; } = [];
}

/// <summary>
/// 파일 그룹 안의 사진 한 장. 「프로필 사진 관리」가 쓴다.
/// </summary>
/// <remarks>
/// <c>DownloadUrl</c> 로 오는 <c>/api/file/download/{id}</c> 는 <b>Vue 시절
/// 주소</b>라 포털 오리진에는 없다. 화면은 이 값을 쓰지 않고
/// <see cref="JSini.Web.Components.Data.FileDownload"/> 로 셸 중계 경로를 만든다.
/// </remarks>
public sealed class GroupFileDto
{
    public string? Id { get; set; }
    public string? OriginalName { get; set; }
    public long Size { get; set; }
    public string? ContentType { get; set; }
    public bool IsImage { get; set; }
    public bool IsRepresentative { get; set; }
    public int SortOrder { get; set; }
    public string? DownloadUrl { get; set; }
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

/// <summary>
/// 이미지 정리 결과 (D17). <c>auth/deploy-status/cleanup</c> 이 돌려준다.
/// </summary>
public sealed class DockerCleanupDto
{
    /// <summary>지우지 않고 목록만 본 것인가. 확인 창이 이 값을 보고 문구를 가른다.</summary>
    public bool DryRun { get; set; }

    /// <summary>
    /// 지웠거나(<see cref="DryRun"/> 이 거짓) 지울(참) 태그들.
    /// <b>이 목록은 서버가 만든다</b> — 화면이 같은 규칙을 다시 적으면
    /// 한쪽만 고치는 날 보여 준 것과 지워지는 것이 달라진다.
    /// </summary>
    public List<string> Removed { get; set; } = [];

    /// <summary>지우다 실패한 것들(<c>이름: 상태코드</c>). 비어 있으면 다 지웠다.</summary>
    public List<string> Errors { get; set; } = [];

    /// <summary>저장소마다 남겨 둔 최근 태그 수. 롤백 여지다.</summary>
    public int KeptRecent { get; set; }

    /// <summary>
    /// 실제로 회수한 용량(MB). <b>미리보기일 때는 회수량이 아니라 상한이다</b> —
    /// 도커의 이미지 크기는 공유 레이어를 저마다 온전히 세기 때문이다.
    /// </summary>
    public long SpaceReclaimedMb { get; set; }
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
    public DateTime? ApprovalDate { get; set; }

    /// <summary>
    /// 사용처 코드값들.
    ///
    /// <b>서버 쪽이 일부러 nullable 이다</b> — 값을 싣지 않은 요청은 사용처를
    /// 건드리지 않는다. 일부 칸만 보내는 호출자가 사용처를 통째로 지우는 일을
    /// 막기 위한 것이다. 여기서는 화면이 늘 전체를 보내므로 빈 목록이
    /// '전부 해제' 가 된다.
    /// </summary>
    public List<string>? UsageLocations { get; set; }
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


// ── AI 제공자 상태 ─────────────────────────────────────────
//
// AI 는 다른 서비스와 성격이 다르다. 컨테이너가 떠 있어도 **제공자 키가
// 없거나 하루 한도를 다 썼으면** 대화가 안 되는데, 그것은 헬스체크로 잡히지
// 않는다. 그래서 서버 상태 화면이 이 값을 따로 본다.

/// <summary><c>ai/providers</c> 응답.</summary>
public sealed class AiProviderStatusDto
{
    /// <summary>기본으로 쓰는 제공자 키.</summary>
    public string? DefaultProvider { get; set; }

    /// <summary>제공자가 죽었을 때 다음으로 넘어가는가.</summary>
    public bool FailoverEnabled { get; set; }

    /// <summary>마지막으로 넘어간 때. 잦으면 기본 제공자가 불안정하다는 뜻이다.</summary>
    public DateTime? LastFailover { get; set; }

    /// <summary>잠시 쉬게 해 둔 모델들. 한도를 넘겼거나 오류가 잦은 것.</summary>
    public List<string> RestingModels { get; set; } = [];

    public List<AiProviderDto> Providers { get; set; } = [];
}

/// <summary>
/// 제공자 하나를 실제로 눌러 본 결과 (<c>ai/health/deep</c> 응답).
///
/// <para>
/// 헬스체크와 다른 것을 본다. 헬스체크는 <b>서비스</b>가 사는지 보고, 이것은
/// <b>그 제공자로 정말 답이 나오는지</b> 본다 — 가장 짧은 질문을 하나 던진다.
/// 자동 전환을 끄고 부르므로 다른 제공자가 대신 답해 '정상' 으로 보이는 일이 없다.
/// </para>
/// <para>
/// <b>실패도 200 으로 온다.</b> 「점검이 실패했다」는 것 자체가 정상적인 응답이고,
/// 화면은 그 이유를 읽어 보여 주어야 한다.
/// </para>
/// </summary>
public sealed class AiDeepCheckDto
{
    public bool Ok { get; set; }

    public string? Provider { get; set; }

    public string? ProviderName { get; set; }

    public string? Model { get; set; }

    public int LatencyMs { get; set; }

    /// <summary>답이 실제로 생성됐는가. 연결은 됐는데 빈 답이 오는 경우가 있다.</summary>
    public bool Generated { get; set; }

    /// <summary>한도 초과인가. <b>고장이 아니다</b> — 화면이 다른 색으로 보여 준다.</summary>
    public bool RateLimited { get; set; }

    public string? Message { get; set; }
}

/// <summary>제공자별로 고를 수 있는 모델 (<c>ai/models</c> 응답).</summary>
public sealed class AiProviderModelsDto
{
    public string Provider { get; set; } = string.Empty;

    /// <summary>사용자가 모델을 고를 수 있는가. 거짓이면 목록은 참고용이다.</summary>
    public bool AllowModelChoice { get; set; }

    /// <summary>무료 모델만 쓰는가.</summary>
    public bool FreeOnly { get; set; }

    public List<string> Models { get; set; } = [];
}

/// <summary>AI 제공자 하나.</summary>
public sealed class AiProviderDto
{
    public string Key { get; set; } = string.Empty;
    public string? DisplayName { get; set; }

    /// <summary>지금 쓰는 모델.</summary>
    public string? Model { get; set; }

    /// <summary>
    /// 키가 설정돼 있는가. <b>거짓이면 그 제공자로는 아무것도 못 부른다</b> —
    /// 컨테이너가 떠 있어도 그렇다.
    /// </summary>
    public bool Configured { get; set; }

    public bool IsDefault { get; set; }

    /// <summary>하루 한도. 0 이면 제한 없음.</summary>
    public int MaxRequestsPerDay { get; set; }

    /// <summary>오늘 쓴 횟수.</summary>
    public int UsedToday { get; set; }

    public int TimeoutSeconds { get; set; }

    /// <summary>기본 모델이 안 될 때 차례로 시도할 것들.</summary>
    public List<string> FallbackModels { get; set; } = [];
}

/// <summary>푸시 도달·열람 요약. 발송 성공과 열람은 다른 이야기다.</summary>
public sealed class PushEngagementDto
{
    /// <summary>보낸 대상 수</summary>
    public int TotalRecipients { get; set; }

    /// <summary>실제로 기기에 닿은 수</summary>
    public int TotalDelivered { get; set; }

    /// <summary>열어 본 수</summary>
    public int TotalRead { get; set; }

    public double DeliveryRate { get; set; }

    /// <summary>닿은 것 중 열어 본 비율</summary>
    public double ReadRate { get; set; }

    /// <summary>보낸 것 중 열어 본 비율</summary>
    public double OpenRate { get; set; }
}

/// <summary>메시지 한 건의 성과.</summary>
public sealed class PushMessageStatDto
{
    public string MessageId { get; set; } = string.Empty;
    public string? Title { get; set; }
    public string? Body { get; set; }
    public DateTime? CreatedAt { get; set; }
    public int RecipientCount { get; set; }
    public int DeliveredCount { get; set; }
    public int ReadCount { get; set; }
    public double ReadRate { get; set; }
}

/// <summary>사람 한 명의 수신·열람.</summary>
public sealed class PushUserStatDto
{
    public string UserId { get; set; } = string.Empty;
    public string? UserType { get; set; }
    public string? UserName { get; set; }
    public int TotalReceived { get; set; }
    public int TotalRead { get; set; }
    public double ReadRate { get; set; }
}
