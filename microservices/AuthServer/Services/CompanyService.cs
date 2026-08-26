using AuthServer.Data;
using AuthServer.DTOs;
using AuthServer.Entities;
using Microsoft.EntityFrameworkCore;
using Mapster;

namespace AuthServer.Services;

/// <summary>
/// 회사 정보를 관리하는 서비스 클래스
/// </summary>
public class CompanyService : ICompanyService
{
    private readonly AppDbContext _context;

    public CompanyService(AppDbContext context)
    {
        _context = context;
    }

    public async Task<IEnumerable<CompanyDto>> GetAllCompaniesAsync()
    {
        var companies = await _context.Companies
            .OrderBy(c => c.SortOrder)
            .ThenByDescending(c => c.CreatedAt)
            .ProjectToType<CompanyDto>()
            .ToListAsync();

        // ── 소속 사용자·부서 수를 함께 채운다 ──────────────────
        //
        // 회사 목록 화면과 사용자 관리 화면(/company/user)이 "어디에 사람이 있는지" 를
        // 바로 보여 주려면 이 숫자가 필요하다. 목록을 받아 놓고 화면에서 회사마다
        // 따로 물어보면 회사 수만큼 요청이 나간다 — 여기서 한 번에 묶어 센다.
        //
        // 지운 계정·부서는 세지 않는다.
        var userCounts = await _context.Accounts
            .Where(a => !a.IsDeleted && a.CompanyId != null)
            .GroupBy(a => a.CompanyId!)
            .Select(g => new { CompanyId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.CompanyId, x => x.Count);

        var deptCounts = await _context.Departments
            .Where(d => !d.IsDeleted && d.CompanyId != null)
            .GroupBy(d => d.CompanyId!)
            .Select(g => new { CompanyId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.CompanyId, x => x.Count);

        foreach (var company in companies)
        {
            company.UserCount = userCounts.GetValueOrDefault(company.Id);
            company.DeptCount = deptCounts.GetValueOrDefault(company.Id);
        }

        return companies;
    }

    public async Task<CompanyDto?> GetCompanyByIdAsync(string id)
    {
        var company = await _context.Companies.FindAsync(id);
        return company?.Adapt<CompanyDto>();
    }

    public async Task<CompanyDto> CreateCompanyAsync(CompanyCreateDto createDto)
    {
        var company = createDto.Adapt<Company>();

        if (company.ApprovalDate.HasValue && company.ApprovalDate.Value.Kind == DateTimeKind.Unspecified)
        {
            company.ApprovalDate = DateTime.SpecifyKind(company.ApprovalDate.Value, DateTimeKind.Utc);
        }

        _context.Companies.Add(company);
        await _context.SaveChangesAsync();

        return company.Adapt<CompanyDto>();
    }

    public async Task<bool> UpdateCompanyAsync(string id, CompanyCreateDto updateDto)
    {
        var company = await _context.Companies.FindAsync(id);
        if (company == null) return false;

        updateDto.Adapt(company);

        if (company.ApprovalDate.HasValue && company.ApprovalDate.Value.Kind == DateTimeKind.Unspecified)
        {
            company.ApprovalDate = DateTime.SpecifyKind(company.ApprovalDate.Value, DateTimeKind.Utc);
        }

        await _context.SaveChangesAsync();
        return true;
    }

    public async Task<bool> DeleteCompanyAsync(string id)
    {
        var company = await _context.Companies.FindAsync(id);
        if (company == null) return false;

        _context.Companies.Remove(company);
        await _context.SaveChangesAsync();
        return true;
    }

    public async Task<IEnumerable<AccountDto>> GetCompanyUsersAsync(string companyId)
    {
        var accounts = await _context.Accounts
            .Include(a => a.Company)
            .Include(a => a.Department)
            .Include(a => a.ProfileDetails)
            .Where(a => a.CompanyId == companyId)
            .ToListAsync();

        var roleAccounts = await _context.RoleAccounts
            .Include(ra => ra.Role)
            .ToListAsync();

        var roleMap = roleAccounts
            .Where(ra => ra.Role != null)
            .GroupBy(ra => ra.AccountId)
            .ToDictionary(
                g => g.Key,
                g => new {
                    RoleIds = g.Select(ra => ra.RoleId).ToList(),
                    RoleNames = g.Select(ra => ra.Role!.Name).ToList()
                }
            );

        return accounts.Select(a => {
            var emailDetail = a.ProfileDetails?.FirstOrDefault(p => p.DetailType == "Email");
            var phoneDetail = a.ProfileDetails?.FirstOrDefault(p => p.DetailType == "Phone");
            var statusDetail = a.ProfileDetails?.FirstOrDefault(p => p.DetailType == "Status");

            roleMap.TryGetValue(a.Id, out var rolesInfo);

            return new AccountDto
            {
                Id = a.Id,
                LoginId = a.UserId,
                UserName = a.UserName ?? string.Empty,
                Email = emailDetail?.Content,
                Phone = phoneDetail?.Content,
                Status = statusDetail?.Content ?? "ACTIVE",
                CompanyId = a.CompanyId,
                CompanyName = a.Company?.Name,
                DeptId = a.DepartmentId,
                DeptName = a.Department?.Name,
                CreatedAt = a.CreatedAt,
                RoleIds = rolesInfo?.RoleIds ?? new List<string>(),
                RoleNames = rolesInfo?.RoleNames ?? new List<string>()
            };
        }).ToList();
    }

    public async Task<IEnumerable<AccountDto>> GetEligibleUsersAsync()
    {
        var accounts = await _context.Accounts
            .Include(a => a.Company)
            .Include(a => a.Department)
            .Include(a => a.ProfileDetails)
            .Where(a => a.CompanyId == null)
            .ToListAsync();

        var roleAccounts = await _context.RoleAccounts
            .Include(ra => ra.Role)
            .ToListAsync();

        var roleMap = roleAccounts
            .Where(ra => ra.Role != null)
            .GroupBy(ra => ra.AccountId)
            .ToDictionary(
                g => g.Key,
                g => new {
                    RoleIds = g.Select(ra => ra.RoleId).ToList(),
                    RoleNames = g.Select(ra => ra.Role!.Name).ToList()
                }
            );

        return accounts.Select(a => {
            var emailDetail = a.ProfileDetails?.FirstOrDefault(p => p.DetailType == "Email");
            var phoneDetail = a.ProfileDetails?.FirstOrDefault(p => p.DetailType == "Phone");
            var statusDetail = a.ProfileDetails?.FirstOrDefault(p => p.DetailType == "Status");

            roleMap.TryGetValue(a.Id, out var rolesInfo);

            return new AccountDto
            {
                Id = a.Id,
                LoginId = a.UserId,
                UserName = a.UserName ?? string.Empty,
                Email = emailDetail?.Content,
                Phone = phoneDetail?.Content,
                Status = statusDetail?.Content ?? "ACTIVE",
                CompanyId = a.CompanyId,
                CompanyName = a.Company?.Name,
                DeptId = a.DepartmentId,
                DeptName = a.Department?.Name,
                CreatedAt = a.CreatedAt,
                RoleIds = rolesInfo?.RoleIds ?? new List<string>(),
                RoleNames = rolesInfo?.RoleNames ?? new List<string>()
            };
        }).ToList();
    }

    public async Task<bool> AssignUsersToCompanyAsync(string companyId, List<string> userIds)
    {
        if (userIds == null || !userIds.Any()) return false;

        var accounts = await _context.Accounts
            .Where(a => userIds.Contains(a.Id))
            .ToListAsync();

        foreach (var account in accounts)
        {
            account.CompanyId = companyId;
        }

        await _context.SaveChangesAsync();
        return true;
    }

    public async Task<bool> RemoveUsersFromCompanyAsync(List<string> userIds)
    {
        if (userIds == null || !userIds.Any()) return false;

        var accounts = await _context.Accounts
            .Where(a => userIds.Contains(a.Id))
            .ToListAsync();

        foreach (var account in accounts)
        {
            account.CompanyId = null;
        }

        await _context.SaveChangesAsync();
        return true;
    }
}
