using AuthServer.Data;
using AuthServer.Entities;
using Microsoft.EntityFrameworkCore;

namespace AuthServer.Services;

/// <summary>가입이 승인되는 순간 붙이는 <b>기본 역할</b>(<c>Auth:Signup:DefaultRoles</c>).</summary>
/// 기본값은 ALL_USERS 하나다 — 승인 뒤 역할을 잊으면 로그인은 되는데 메뉴가 하나도 없다.
/// 부르는 곳은 관리자 승인과 소셜 자동 승인 둘이라 한 곳에만 두면 다른 길로 들어온 사람만 역할이 빠진다.
public static class SignupDefaultRoles
{
    /// <summary>설정이 비어 있을 때 쓰는 값.</summary>
    public static readonly string[] Fallback = ["ALL_USERS"];

    /// <summary>설정에서 기본 역할 목록을 읽는다.</summary>
    public static IReadOnlyList<string> From(IConfiguration configuration)
    {
        var configured = configuration.GetSection("Auth:Signup:DefaultRoles").Get<string[]>();

        return configured is { Length: > 0 }
            ? [.. configured.Where(r => !string.IsNullOrWhiteSpace(r)).Select(r => r.Trim()).Distinct()]
            : Fallback;
    }

    /// <summary>
    /// 기본 역할을 <b>추가해 둔다</b>(저장은 부르는 쪽이 한다).
    /// 이미 가진 역할과 DB 에 없는 역할은 건너뛴다.
    /// </summary>
    /// <returns>실제로 붙인 역할.</returns>
    public static async Task<List<string>> AddAsync(
        AppDbContext db, IConfiguration configuration, ILogger logger,
        string accountId, string grantedBy, CancellationToken ct = default)
    {
        var wanted = From(configuration);

                // 없는 역할을 붙이면 외래키로 저장 전체가 깨진다. 설정 오타 때문에 승인이 막히지 않도록 있는 것만 붙인다.
        var existing = await db.Roles
            .Where(r => wanted.Contains(r.Id) && !r.IsDeleted)
            .Select(r => r.Id)
            .ToListAsync(ct);

        var missing = wanted.Except(existing).ToList();
        if (missing.Count > 0)
        {
            logger.LogWarning(
                "가입 승인 기본 역할 중 DB 에 없는 것이 있어 건너뛴다: {Roles}", string.Join(",", missing));
        }

        var already = await db.RoleAccounts
            .Where(ra => ra.AccountId == accountId && !ra.IsDeleted && existing.Contains(ra.RoleId))
            .Select(ra => ra.RoleId)
            .ToListAsync(ct);

        var added = existing.Except(already).ToList();
        foreach (var roleId in added)
        {
            db.RoleAccounts.Add(new RoleAccount
            {
                AccountId = accountId,
                RoleId = roleId,
                CreatedBy = grantedBy,
            });
        }

        return added;
    }
}
