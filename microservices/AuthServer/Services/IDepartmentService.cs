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
    Task<bool> MoveUserDeptAsync(string accountId, string? departmentId, UserContext? userContext);
}
