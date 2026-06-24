using AuthServer.DTOs;

namespace AuthServer.Services;

public interface ICompanyService
{
    Task<IEnumerable<CompanyDto>> GetAllCompaniesAsync();
    Task<CompanyDto?> GetCompanyByIdAsync(string id);
    Task<CompanyDto> CreateCompanyAsync(CompanyCreateDto createDto);
    Task<bool> UpdateCompanyAsync(string id, CompanyCreateDto updateDto);
    Task<bool> DeleteCompanyAsync(string id);
    Task<IEnumerable<AccountDto>> GetCompanyUsersAsync(string companyId);
    Task<IEnumerable<AccountDto>> GetEligibleUsersAsync();
    Task<bool> AssignUsersToCompanyAsync(string companyId, List<string> userIds);
    Task<bool> RemoveUsersFromCompanyAsync(List<string> userIds);
}
