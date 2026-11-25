using AuthServer.Data;
using AuthServer.DTOs;
using AuthServer.Entities;
using Microsoft.EntityFrameworkCore;

namespace AuthServer.Services;

/// <summary>회사 사용처의 처리 담당자 — 회사 × 사용처 × 사람.</summary>
public sealed class CompanyUsageManagerService(AppDbContext db)
{
    /// <summary>한 회사의 담당자 전부. 사용처를 주면 그것만 준다.</summary>
    public async Task<List<CompanyUsageManagerDto>> ListAsync(string companyId, string? codeValue, CancellationToken ct)
    {
        var query = db.CompanyUsageManagers.AsNoTracking()
            .Where(m => !m.IsDeleted && m.CompanyId == companyId);

        if (!string.IsNullOrWhiteSpace(codeValue))
        {
            var code = codeValue.Trim();
            query = query.Where(m => m.CodeValue == code);
        }

        var rows = await query
            .Where(m => m.Account != null && !m.Account.IsDeleted)
            .OrderBy(m => m.CodeValue)
            .ThenBy(m => m.Account!.UserName)
            .Select(m => new
            {
                m.Id,
                m.CompanyId,
                m.CodeValue,
                m.AccountId,
                LoginId = m.Account!.UserId,
                UserName = m.Account.UserName,
                CompanyName = m.Account.Company != null ? m.Account.Company.Name : null,
                DeptName = m.Account.Department != null ? m.Account.Department.Name : null,
                Email = m.Account.ProfileDetails!
                    .Where(p => p.DetailType == "Email")
                    .Select(p => p.Content)
                    .FirstOrDefault(),
                m.CreatedAt,
            })
            .ToListAsync(ct);

        return [.. rows.Select(r => new CompanyUsageManagerDto
        {
            Id = r.Id,
            CompanyId = r.CompanyId,
            CodeValue = r.CodeValue,
            AccountId = r.AccountId,
            LoginId = r.LoginId,
            UserName = r.UserName ?? string.Empty,
            CompanyName = r.CompanyName,
            DeptName = r.DeptName,
            Email = r.Email,
            CreatedAt = r.CreatedAt,
        })];
    }

    /// <summary>담당자를 넣는다. 이미 있는 사람은 건너뛴다. 넣은 수를 준다.</summary>
    /// <exception cref="InvalidOperationException">회사가 없거나 그 사용처가 회사에 걸려 있지 않다.</exception>
    public async Task<int> AddAsync(CompanyUsageManagerAddDto body, CancellationToken ct)
    {
        var companyId = body.CompanyId?.Trim() ?? string.Empty;
        var code = body.CodeValue?.Trim() ?? string.Empty;

        if (!await db.Companies.AnyAsync(c => c.Id == companyId, ct))
        {
            throw new InvalidOperationException("회사를 찾을 수 없습니다.");
        }

        // 걸려 있지 않은 사용처에 담당자를 두면 화면에서 안 보이는 줄이 쌓인다.
        if (!await db.CompanyUsageLocations.AnyAsync(u => !u.IsDeleted && u.CompanyId == companyId && u.CodeValue == code, ct))
        {
            throw new InvalidOperationException("그 회사에 걸려 있지 않은 사용처입니다.");
        }

        var wanted = (body.AccountIds ?? [])
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .Distinct(StringComparer.Ordinal)
            .ToList();
        if (wanted.Count == 0) return 0;

        var existing = await db.Accounts
            .Where(a => wanted.Contains(a.Id) && !a.IsDeleted)
            .Select(a => a.Id)
            .ToListAsync(ct);

        var current = await db.CompanyUsageManagers
            .Where(m => m.CompanyId == companyId && m.CodeValue == code && existing.Contains(m.AccountId))
            .ToListAsync(ct);

        var added = 0;
        foreach (var accountId in existing)
        {
            var row = current.FirstOrDefault(m => m.AccountId == accountId);
            if (row is null)
            {
                db.CompanyUsageManagers.Add(new CompanyUsageManager
                {
                    CompanyId = companyId,
                    CodeValue = code,
                    AccountId = accountId,
                });
                added++;
            }
            else if (row.IsDeleted)
            {
                row.IsDeleted = false;
                added++;
            }
        }

        await db.SaveChangesAsync(ct);
        return added;
    }

    /// <summary>담당자를 뺀다. 줄을 실제로 지운다 — 유일 색인 때문에 지운 표시만 남기면 다시 넣을 때 부딪힌다(되살리는 길도 있지만 남길 까닭이 없다).</summary>
    public async Task<int> RemoveAsync(IReadOnlyCollection<int> ids, CancellationToken ct)
    {
        if (ids.Count == 0) return 0;

        return await db.CompanyUsageManagers
            .Where(m => ids.Contains(m.Id))
            .ExecuteDeleteAsync(ct);
    }

    /// <summary>한 회사에서 그 사용처를 맡은 사람들의 로그인 아이디. 역할을 주면 그 역할을 가진 사람만 준다. 사용처가 회사에서 풀렸으면 빈 목록이다.</summary>
    public async Task<List<string>> ManagerLoginIdsAsync(
        string companyId, string codeValue, string? roleId, CancellationToken ct)
    {
        var code = codeValue.Trim();
        var role = roleId?.Trim();

        return await db.CompanyUsageManagers.AsNoTracking()
            .Where(m => !m.IsDeleted
                && m.CompanyId == companyId
                && m.CodeValue == code
                && m.Account != null && !m.Account.IsDeleted
                && db.CompanyUsageLocations.Any(u => !u.IsDeleted && u.CompanyId == m.CompanyId && u.CodeValue == code)
                && (string.IsNullOrEmpty(role)
                    || db.RoleAccounts.Any(ra => !ra.IsDeleted && ra.AccountId == m.AccountId && ra.RoleId == role)))
            .Select(m => m.Account!.UserId)
            .Distinct()
            .ToListAsync(ct);
    }

    /// <summary>이 로그인 아이디가 그 사용처의 담당자로 걸린 회사들. 사용처가 지금도 회사에 걸려 있는 것만 준다.</summary>
    public async Task<List<string>> MyCompanyIdsAsync(string loginId, string codeValue, CancellationToken ct)
    {
        var code = codeValue.Trim();

        return await db.CompanyUsageManagers.AsNoTracking()
            .Where(m => !m.IsDeleted
                && m.CodeValue == code
                && m.Account != null && m.Account.UserId == loginId && !m.Account.IsDeleted
                && db.CompanyUsageLocations.Any(u => !u.IsDeleted && u.CompanyId == m.CompanyId && u.CodeValue == code))
            .Select(m => m.CompanyId)
            .Distinct()
            .ToListAsync(ct);
    }
}
