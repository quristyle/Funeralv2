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

    public async Task<IEnumerable<CompanyDto>> GetAllCompaniesAsync(string? usageLocation = null)
    {
        var query = _context.Companies.AsQueryable();

        // 사용처로 좁힌다. 장례식장 관리시스템 화면들이 자기 시스템에 배정된 회사만
        // 보려고 쓴다(BizSelect 의 `funeralCompany` 타입). 비우면 전부 준다.
        var wanted = usageLocation?.Trim();
        if (!string.IsNullOrEmpty(wanted))
        {
            query = query.Where(c => _context.CompanyUsageLocations
                .Any(u => !u.IsDeleted && u.CompanyId == c.Id && u.CodeValue == wanted));
        }

        var companies = await query
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

        // 사용처도 같은 이유로 한 번에 묶어 온다(회사마다 물어보면 N+1 이다).
        var usageMap = await LoadUsageLocationsAsync(companies.Select(c => c.Id));

        foreach (var company in companies)
        {
            company.UserCount = userCounts.GetValueOrDefault(company.Id);
            company.DeptCount = deptCounts.GetValueOrDefault(company.Id);
            company.UsageLocations = usageMap.GetValueOrDefault(company.Id) ?? new List<string>();
        }

        return companies;
    }

    /// <summary>
    /// 회사별 사용처(<c>COMPANY_USAGE_LOCATION</c> 코드값) 목록을 한 번에 읽는다.
    /// </summary>
    private async Task<Dictionary<string, List<string>>> LoadUsageLocationsAsync(
        IEnumerable<string> companyIds)
    {
        var ids = companyIds.Distinct().ToList();
        if (ids.Count == 0) return new Dictionary<string, List<string>>();

        var rows = await _context.CompanyUsageLocations
            .Where(u => !u.IsDeleted && ids.Contains(u.CompanyId))
            .Select(u => new { u.CompanyId, u.CodeValue })
            .ToListAsync();

        return rows
            .GroupBy(r => r.CompanyId)
            .ToDictionary(g => g.Key, g => g.Select(r => r.CodeValue).ToList());
    }

    /// <summary>
    /// 회사의 사용처를 요청한 목록으로 맞춘다.
    /// </summary>
    /// <remarks>
    /// <paramref name="codeValues"/> 가 <c>null</c> 이면 <b>아무것도 하지 않는다</b> —
    /// 일부 항목만 보내는 호출자(목록 화면의 셀 편집)가 사용처를 지우지 않게 하려는 것이다.
    /// 빈 목록은 '전부 해제' 다.
    ///
    /// <para>
    /// 있던 것을 지우고 다시 넣지 않고 <b>차이만</b> 반영한다. 그래야 바꾸지 않은 행의
    /// 등록 정보(<c>created_at</c> · <c>created_by</c>)가 남는다.
    /// 지울 때는 행을 실제로 지운다 — <c>(company_id, code_value)</c> 에 유일 색인이 걸려 있어
    /// 지운 표시만 남기면 같은 코드를 다시 넣을 때 부딪힌다.
    /// </para>
    /// </remarks>
    private async Task ApplyUsageLocationsAsync(string companyId, List<string>? codeValues)
    {
        if (codeValues == null) return;

        var wanted = codeValues
            .Select(v => (v ?? string.Empty).Trim())
            .Where(v => v.Length > 0)
            .Distinct(StringComparer.Ordinal)
            .ToList();

        var current = await _context.CompanyUsageLocations
            .Where(u => u.CompanyId == companyId)
            .ToListAsync();

        var toRemove = current
            .Where(u => !wanted.Contains(u.CodeValue, StringComparer.Ordinal))
            .ToList();
        if (toRemove.Count > 0) _context.CompanyUsageLocations.RemoveRange(toRemove);

        var kept = current.Except(toRemove).Select(u => u.CodeValue).ToHashSet(StringComparer.Ordinal);
        foreach (var code in wanted.Where(code => !kept.Contains(code)))
        {
            _context.CompanyUsageLocations.Add(new CompanyUsageLocation
            {
                CompanyId = companyId,
                CodeValue = code
            });
        }
    }

    public async Task<CompanyDto?> GetCompanyByIdAsync(string id)
    {
        var company = await _context.Companies.FindAsync(id);
        if (company == null) return null;

        var dto = company.Adapt<CompanyDto>();
        var usageMap = await LoadUsageLocationsAsync(new[] { id });
        dto.UsageLocations = usageMap.GetValueOrDefault(id) ?? new List<string>();
        return dto;
    }

    public async Task<CompanyDto> CreateCompanyAsync(CompanyCreateDto createDto)
    {
        var company = createDto.Adapt<Company>();

        if (company.ApprovalDate.HasValue && company.ApprovalDate.Value.Kind == DateTimeKind.Unspecified)
        {
            company.ApprovalDate = DateTime.SpecifyKind(company.ApprovalDate.Value, DateTimeKind.Utc);
        }

        _context.Companies.Add(company);
        // 회사 행이 있어야 사용처가 외래키를 걸 수 있다. 먼저 저장한다.
        await _context.SaveChangesAsync();

        await ApplyUsageLocationsAsync(company.Id, createDto.UsageLocations);
        await _context.SaveChangesAsync();

        var dto = company.Adapt<CompanyDto>();
        dto.UsageLocations = createDto.UsageLocations ?? new List<string>();
        return dto;
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

        // 값을 실어 보낸 요청만 사용처를 바꾼다(위 주석 참고).
        await ApplyUsageLocationsAsync(id, updateDto.UsageLocations);

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
