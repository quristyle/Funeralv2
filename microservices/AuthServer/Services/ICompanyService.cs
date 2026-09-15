using AuthServer.DTOs;

namespace AuthServer.Services;

public interface ICompanyService
{
    /// <summary>
    /// 회사 전체 목록.
    /// </summary>
    /// <param name="usageLocation">
    /// 사용처(<c>COMPANY_USAGE_LOCATION</c> 의 <c>code_value</c>)로 좁힌다.
    /// 비우면 전부 준다. 장례식장 관리시스템 화면들이
    /// <c>FUNERAL_HOME_MANAGEMENT_SYSTEM</c> 으로 좁혀 쓴다.
    /// </param>
    Task<IEnumerable<CompanyDto>> GetAllCompaniesAsync(string? usageLocation = null);
    Task<CompanyDto?> GetCompanyByIdAsync(string id);
    Task<CompanyDto> CreateCompanyAsync(CompanyCreateDto createDto);
    Task<bool> UpdateCompanyAsync(string id, CompanyCreateDto updateDto);
    Task<bool> DeleteCompanyAsync(string id);

    /// <summary>
    /// 회사 차례를 다시 매긴다. <paramref name="orderedIds"/> 에 실린 순서가
    /// 그대로 <c>sort_order</c> 1, 2, 3… 이 된다.
    /// </summary>
    /// <remarks>
    /// <b>줄 하나가 아니라 목록을 받는다.</b> 「어느 회사를 몇 번으로」를 받으면
    /// 그 사이 번호들이 그대로 남아 같은 번호가 둘 생기고, 그때 순서는
    /// <c>created_at</c> 이 말없이 정한다 — 화면이 보여 준 자리와 달라진다.
    /// 자리를 통째로 받아 다시 매기면 그 틈이 없다.
    /// </remarks>
    Task<bool> ReorderCompaniesAsync(List<string> orderedIds);
    Task<IEnumerable<AccountDto>> GetCompanyUsersAsync(string companyId);
    Task<IEnumerable<AccountDto>> GetEligibleUsersAsync();
    Task<bool> AssignUsersToCompanyAsync(string companyId, List<string> userIds);
    Task<bool> RemoveUsersFromCompanyAsync(List<string> userIds);
}
