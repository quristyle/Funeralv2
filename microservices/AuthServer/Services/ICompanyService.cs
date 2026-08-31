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
    Task<IEnumerable<AccountDto>> GetCompanyUsersAsync(string companyId);
    Task<IEnumerable<AccountDto>> GetEligibleUsersAsync();
    Task<bool> AssignUsersToCompanyAsync(string companyId, List<string> userIds);
    Task<bool> RemoveUsersFromCompanyAsync(List<string> userIds);
}
