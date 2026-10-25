using AIAgentServer.Data;

using Microsoft.EntityFrameworkCore;

namespace AIAgentServer.Services.Portal;

/// <summary>질문 속 이름 하나가 가리킬 수 있는 포털 사용자 한 명. 회사·부서는 계정에 안 적혀 있으면 <c>null</c> 이다.</summary>
public sealed record PortalPerson(string UserId, string Name, string? CompanyName, string? Dept);

/// <summary>질문의 사람 이름("홍길동")을 포털 계정(<c>user_id</c>)으로 바꾼다. <c>scom.accounts</c> 를 읽기만 한다(정본은 AuthServer).
/// 동명이인을 만나도 <b>고르지 않는다</b> — 이 화면은 남의 자료를 건너 보여 주므로, 하나를 멋대로 골랐다가 틀리면
/// 엉뚱한 사람 이름으로 남의 기록이 나간다. 그 판단은 <see cref="Describe"/> 의 글을 받아 모델이 사용자에게 되묻는다.</summary>
public sealed class UserDirectory(IServiceScopeFactory scopes)
{
    /// <summary>후보가 아무리 많아도 이만큼만 돌려준다 — 나머지는 이름을 좁혀서 다시 물으라는 뜻이다.</summary>
    private const int MaxCandidates = 10;

    /// <summary>이름이 들어맞는 계정을 찾는다. 완전히 같은 이름을 먼저, 이름을 품은 계정을 그다음에 담는다.
    /// 지운 계정은 빼고, 회사는 가리지 않는다(이 비서는 회사 경계를 넘나드는 것이 목적이다).</summary>
    public async Task<IReadOnlyList<PortalPerson>> FindAsync(string name, CancellationToken ct = default)
    {
        var trimmed = name.Trim();
        if (trimmed.Length == 0) return [];

        using var scope = scopes.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AiUsageDbContext>();

        var matched = await db.Accounts
            .AsNoTracking()
            .Where(a => !a.IsDeleted && a.UserName != null && a.UserName.Contains(trimmed))
            .ToListAsync(ct);

        var ordered = matched
            .OrderByDescending(a => a.UserName == trimmed) // 완전일치가 앞선다.
            .ThenBy(a => a.UserName, StringComparer.Ordinal)
            .Take(MaxCandidates)
            .ToList();

        return await AttachOrgAsync(db, ordered, ct);
    }

    /// <summary>로그인 아이디 하나로 정확히 찾는다. <b>이름과 달리 아이디는 유일하므로</b> 동명이인 걱정 없이
    /// 바로 한 사람으로 정해진다 — search_schedules 가 아이디만 받았을 때 프로젝트관리 쪽에 넘길 이름을
    /// 채우려고 쓴다. 못 찾으면 <c>null</c>(지운 계정이거나 포털에 없는 아이디) — 부르는 쪽이 아이디만으로 물러선다.</summary>
    public async Task<PortalPerson?> FindByIdAsync(string userId, CancellationToken ct = default)
    {
        var trimmed = userId.Trim();
        if (trimmed.Length == 0) return null;

        using var scope = scopes.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AiUsageDbContext>();

        var account = await db.Accounts
            .AsNoTracking()
            .FirstOrDefaultAsync(a => !a.IsDeleted && a.UserId == trimmed, ct);
        if (account is null) return null;

        var people = await AttachOrgAsync(db, [account], ct);
        return people.Count == 0 ? null : people[0];
    }

    /// <summary>계정 목록에 회사·부서 이름을 붙인다. <see cref="FindAsync"/>·<see cref="FindByIdAsync"/> 가
    /// 같은 식으로 조직 이름을 채우므로 한 곳에 모았다.</summary>
    private static async Task<IReadOnlyList<PortalPerson>> AttachOrgAsync(
        AiUsageDbContext db, IReadOnlyList<AiUsageAccountRow> accounts, CancellationToken ct)
    {
        if (accounts.Count == 0) return [];

        var companyIds = accounts.Select(a => a.CompanyId).Where(id => id != null).Distinct().ToList();
        var departmentIds = accounts.Select(a => a.DepartmentId).Where(id => id != null).Distinct().ToList();

        var companyNames = await db.Companies
            .AsNoTracking()
            .Where(c => companyIds.Contains(c.Id))
            .ToDictionaryAsync(c => c.Id, c => c.Name, ct);

        var departmentNames = await db.Departments
            .AsNoTracking()
            .Where(d => departmentIds.Contains(d.Id))
            .ToDictionaryAsync(d => d.Id, d => d.Name, ct);

        return accounts
            .Select(a => new PortalPerson(
                a.UserId,
                a.UserName ?? a.UserId,
                a.CompanyId != null && companyNames.TryGetValue(a.CompanyId, out var companyName) ? companyName : null,
                a.DepartmentId != null && departmentNames.TryGetValue(a.DepartmentId, out var deptName) ? deptName : null))
            .ToList();
    }

    /// <summary>찾은 사람들을 모델이 읽을 글로 바꾼다. <b>둘 이상이면 절대 하나를 고르지 않고</b>, 후보를 전부
    /// 늘어놓은 뒤 사용자에게 되물으라고 못박는다 — 회사·부서가 없는 계정도 빈 괄호나 떠 있는 구분자 없이 읽혀야 한다.</summary>
    public static string Describe(IReadOnlyList<PortalPerson> people)
    {
        if (people.Count == 0)
            return "찾는 이름의 사용자가 없습니다.";

        if (people.Count == 1)
            return $"{DescribeOne(people[0])} 한 명을 찾았습니다.";

        var lines = people.Select(p => $"- {DescribeOne(p)}");
        return $"같은 이름의 사용자가 {people.Count}명입니다. 고르지 말고 어느 사람인지 사용자에게 되물어야 합니다:\n"
            + string.Join("\n", lines);
    }

    /// <summary>사람 한 명을 "이름(아이디) — 회사 부서" 로 적는다. 회사·부서가 없으면 그 자리를 통째로 뺀다.</summary>
    private static string DescribeOne(PortalPerson p)
    {
        var org = string.Join(" ", new[] { p.CompanyName, p.Dept }.Where(s => !string.IsNullOrWhiteSpace(s)));
        return org.Length == 0
            ? $"{p.Name}({p.UserId})"
            : $"{p.Name}({p.UserId}) — {org}";
    }
}
