using AuthServer.DTOs;

namespace AuthServer.Services;

public interface ICompanyService
{
    /// <summary>회사 전체 목록.</summary>
    /// <param name="usageLocation">사용처 코드로 좁힌다. 비우면 전부 준다.</param>
    Task<IEnumerable<CompanyDto>> GetAllCompaniesAsync(string? usageLocation = null);
    Task<CompanyDto?> GetCompanyByIdAsync(string id);
    Task<CompanyDto> CreateCompanyAsync(CompanyCreateDto createDto);
    Task<bool> UpdateCompanyAsync(string id, CompanyCreateDto updateDto);
    Task<bool> DeleteCompanyAsync(string id);

    /// <summary>회사 차례를 다시 매긴다. orderedIds 순서대로 sort_order 1, 2, 3… 이 된다.</summary>
    /// 줄 하나가 아니라 목록을 받는다 — 한 줄씩 옮기면 같은 번호가 둘 생긴다.
    Task<bool> ReorderCompaniesAsync(List<string> orderedIds);
    Task<IEnumerable<AccountDto>> GetCompanyUsersAsync(string companyId);
    Task<IEnumerable<AccountDto>> GetEligibleUsersAsync();
    Task<bool> AssignUsersToCompanyAsync(string companyId, List<string> userIds);
    Task<bool> RemoveUsersFromCompanyAsync(List<string> userIds);
}
