using AuthServer.DTOs;

namespace AuthServer.Services;

public interface IDepartmentService
{
    Task<List<DepartmentDto>> GetDeptListAsync(string? companyId, UserContext? userContext);
    Task<DepartmentDto> CreateDeptAsync(CreateDepartmentDto request, UserContext? userContext);
    Task<bool> UpdateDeptAsync(string id, CreateDepartmentDto request, UserContext? userContext);
    Task<bool> DeleteDeptAsync(string id, UserContext? userContext);
}
