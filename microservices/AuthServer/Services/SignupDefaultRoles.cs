using AuthServer.Data;
using AuthServer.Entities;
using Microsoft.EntityFrameworkCore;

namespace AuthServer.Services;

/// <summary>
/// 가입이 승인되는 순간 붙이는 <b>기본 역할</b> (<c>Auth:Signup:DefaultRoles</c>).
/// </summary>
/// <remarks>
/// <para>
/// 기본값은 <c>ALL_USERS</c>(모든사용자) 하나다. 시스템의 모든 사람이 가져야
/// 하는 권한이라, 승인해 놓고 역할을 따로 붙이는 것을 잊으면 그 사람은
/// 로그인은 되는데 <b>메뉴가 하나도 없는</b> 화면을 만난다.
/// </para>
/// <para>
/// 부서·업무 역할은 여전히 승인하는 사람이 계정 관리에서 붙인다(신청자가
/// 스스로 권한을 고르지 못하게 하려는 가입 신청의 원칙 그대로다). 여기서
/// 붙이는 것은 <b>누구나 받는 것</b>뿐이다.
/// </para>
/// <para>
/// 부르는 곳이 둘이다 — 관리자의 승인(<see cref="SignupService.ApproveAsync"/>)과
/// 소셜 자동 승인(<c>Auth:Social:AutoApprove</c>). 한 곳에만 두면 다른 길로 들어온
/// 사람만 역할이 빠진다.
/// </para>
/// </remarks>
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

        // 없는 역할을 붙이면 외래키로 저장 전체가 깨진다. 오타 난 설정 하나
        // 때문에 승인이 안 되는 일이 없도록 있는 것만 붙이고 나머지는 알린다.
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
