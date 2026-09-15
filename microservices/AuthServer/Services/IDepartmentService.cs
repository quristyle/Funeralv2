using AuthServer.DTOs;

namespace AuthServer.Services;

public interface IDepartmentService
{
    /// <summary>
    /// 부서 목록 (트리).
    /// </summary>
    /// <param name="companyId">조회할 회사. 비우면 요청한 사람의 회사로 좁혀진다.</param>
    /// <param name="userContext">게이트웨이가 넘긴 신원</param>
    /// <param name="allCompanies">
    /// 모든 회사의 부서를 함께 볼지. <b>회사 인자를 비우는 것으로는 '전체' 를 표현할 수 없다</b> —
    /// 비우면 요청한 사람의 회사로 좁혀지기 때문이다.
    /// </param>
    Task<List<DepartmentDto>> GetDeptListAsync(
        string? companyId, UserContext? userContext, bool allCompanies = false);
    Task<DepartmentDto> CreateDeptAsync(CreateDepartmentDto request, UserContext? userContext);
    Task<bool> UpdateDeptAsync(string id, CreateDepartmentDto request, UserContext? userContext);
    Task<bool> DeleteDeptAsync(string id, UserContext? userContext);
    
    Task<IEnumerable<AccountDto>> GetDeptUsersAsync(string departmentId);
    Task<IEnumerable<AccountDto>> GetEligibleUsersAsync(string? companyId);
    Task<bool> AssignUsersToDeptAsync(string departmentId, List<string> userIds);
    Task<bool> RemoveUsersFromDeptAsync(List<string> userIds);
    Task<bool> MoveDeptAsync(string id, string? parentId, UserContext? userContext);

    /// <summary>
    /// 여러 부서의 상위와 순번을 한 번에 반영한다.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>줄 하나가 아니라 배치를 받는다.</b> <see cref="MoveDeptAsync"/> 는 상위만
    /// 바꾸고 순번을 건드리지 않아, 끌어 옮긴 자리가 저장되지 않는다 — 다시 읽으면
    /// 부서가 형제들 사이 제자리로 돌아간다. 화면(트리)이 확정한 배치를 그대로 받아
    /// 한 번의 왕복으로 저장한다.
    /// </para>
    /// <para>
    /// <b>회사를 넘나드는 이동은 막는다.</b> 부서는 회사에 딸린 것이라
    /// (<c>departments.company_id</c>) 남의 회사 부서 아래로 들어가면 그 부서의
    /// 회사와 조상의 회사가 갈린다 — 조직도·생일 목록·역할 범위가 모두 이 나무를
    /// 읽으므로 한 번 갈리면 어디서 틀어졌는지 찾기 어렵다.
    /// </para>
    /// </remarks>
    Task<bool> ReorderDeptsAsync(List<DeptOrderDto> items, UserContext? userContext);
    Task<bool> MoveUserDeptAsync(string accountId, string? departmentId, UserContext? userContext);
}
