namespace AuthServer.DTOs;

/// <summary>
/// 역할을 걸 수 있는 대상 한 칸. 회사·부서·사람이 같은 모양을 쓴다 —
/// 화면이 트리 하나로 그리고, 어디에 놓든 같은 방식으로 처리하기 위해서다.
/// </summary>
public class RoleScopeNodeDto
{
    /// <summary>대상 식별자 (회사·부서·계정 ID)</summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>화면에 보일 이름</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>계정인 경우의 로그인 아이디</summary>
    public string? LoginId { get; set; }

    /// <summary><c>company</c> · <c>department</c> · <c>account</c></summary>
    public string Kind { get; set; } = string.Empty;

    /// <summary>이 대상에 **직접** 걸린 역할. 물려받은 것은 포함하지 않는다.</summary>
    public List<string> RoleIds { get; set; } = new();

    /// <summary>하위 부서</summary>
    public List<RoleScopeNodeDto> Children { get; set; } = new();

    /// <summary>이 부서(또는 회사) 소속 사람</summary>
    public List<RoleScopeNodeDto> Accounts { get; set; } = new();
}

/// <summary>회사 하나의 조직 트리와 각 단계에 걸린 역할.</summary>
public class RoleScopeTreeDto
{
    /// <summary>루트인 회사 노드</summary>
    public RoleScopeNodeDto Company { get; set; } = new();
}

/// <summary>
/// 어떤 계정에 실제로 적용되는 역할과, 각 역할이 어느 단계에서 왔는지.
/// </summary>
public class EffectiveRolesDto
{
    /// <summary>적용되는 역할 식별자 (회사 + 부서 + 사람을 모두 합친 것)</summary>
    public List<string> RoleIds { get; set; } = new();

    /// <summary>적용되는 역할 표시 이름. <see cref="RoleIds"/> 와 같은 순서다</summary>
    public List<string> RoleNames { get; set; } = new();

    /// <summary>
    /// 역할 식별자 → 그 역할이 온 단계들 (<c>company</c> · <c>department</c> · <c>account</c>).
    ///
    /// <para>
    /// 한 역할이 여러 단계에 걸려 있을 수 있어 목록이다. 화면이 "이건 부서에서 온 것" 이라고
    /// 알려 주고, 사람 단계에서 빼도 남는 이유를 설명할 때 쓴다.
    /// </para>
    /// </summary>
    public Dictionary<string, List<string>> Sources { get; set; } = new();
}

/// <summary>
/// 어떤 계정이 볼 수 있는 메뉴와 볼 수 없는 메뉴.
/// "이 사람이 무슨 권한을 갖는가" 를 눈으로 확인하는 화면이 쓴다.
/// </summary>
public class AccountMenuAccessDto
{
    /// <summary>역할로 열려 있는 메뉴</summary>
    public List<AccountMenuItemDto> Assigned { get; set; } = new();

    /// <summary>열려 있지 않은 메뉴</summary>
    public List<AccountMenuItemDto> Unassigned { get; set; } = new();
}

/// <summary>메뉴 한 칸과, 그 메뉴를 열어 준 역할.</summary>
public class AccountMenuItemDto
{
    /// <summary>메뉴 식별자</summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>메뉴 경로</summary>
    public string Path { get; set; } = string.Empty;

    /// <summary>화면에 보일 제목 (다국어 키일 수 있다)</summary>
    public string? Title { get; set; }

    /// <summary>메뉴 유형 (CATALOG · MENU · EMBEDDED …)</summary>
    public string Type { get; set; } = string.Empty;

    /// <summary>상위 메뉴 경로를 이어 붙인 것. 목록에서 어디 있는 메뉴인지 알아보기 위해서다</summary>
    public string? Breadcrumb { get; set; }

    /// <summary>이 메뉴를 열어 준 역할 식별자들 (열려 있는 경우에만 채워진다)</summary>
    public List<string> GrantedBy { get; set; } = new();
}

/// <summary>역할 배정·해제 요청.</summary>
public class RoleAssignRequest
{
    /// <summary><c>company</c> · <c>department</c> · <c>account</c></summary>
    public string Kind { get; set; } = string.Empty;

    /// <summary>대상 식별자</summary>
    public string TargetId { get; set; } = string.Empty;

    /// <summary>역할 식별자</summary>
    public string RoleId { get; set; } = string.Empty;
}

/// <summary>
/// 왼쪽 사람 목록의 한 칸. 회사·부서 이름까지 담아 **한 줄 안에서 검색**할 수 있게 한다.
/// 이름·아이디·부서·회사 어느 것으로 쳐도 걸리는 것이 이 화면의 목적이다.
/// </summary>
public class AccountPickDto
{
    /// <summary>계정 식별자 (scom.accounts.id)</summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>로그인 아이디</summary>
    public string LoginId { get; set; } = string.Empty;

    /// <summary>표시 이름 (실명 우선)</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>소속 회사 식별자</summary>
    public string? CompanyId { get; set; }

    /// <summary>소속 회사명</summary>
    public string? CompanyName { get; set; }

    /// <summary>소속 부서 식별자</summary>
    public string? DepartmentId { get; set; }

    /// <summary>소속 부서명</summary>
    public string? DepartmentName { get; set; }

    /// <summary>
    /// 프로필 사진 주소. 없으면 <c>null</c> — 화면이 <b>이름 첫 글자</b>로 대신 그린다.
    /// </summary>
    /// <remarks>
    /// 43명 중 사진이 있는 사람은 지금 한 명뿐이다. 그래서 <b>없는 쪽이 정상</b>이고,
    /// 화면은 사진이 없는 것을 오류처럼 보이게 하면 안 된다.
    /// 값이 <c>/api/file/download/...</c> 꼴이면 화면이 <c>/api/file/thumbnail/...</c> 로
    /// 바꿔 쓴다 — 목록에 원본을 그대로 받으면 무겁다.
    /// </remarks>
    public string? Avatar { get; set; }
}
