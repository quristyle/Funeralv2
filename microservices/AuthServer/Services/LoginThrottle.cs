using AuthServer.Data;
using AuthServer.Entities;
using Microsoft.EntityFrameworkCore;

namespace AuthServer.Services;

/// <summary>아이디 단위 로그인 잠금. 연속으로 틀릴수록 기다리는 시간이 늘어난다.</summary>
/// 게이트웨이의 시도 제한은 IP 당 분당 10회라 IP 를 여럿 돌리면 안 걸린다. 그래서 「무엇을 두드렸나」(아이디)로 한 번 더 센다.
/// 따로 표를 두지 않고 접속 기록(<c>account_login_logs</c>)의 연속 실패를 센다 — 성공 한 번이 사슬을 끊는다.
/// 없는 아이디도 똑같이 잠근다. 있는 아이디만 잠그면 잠김 응답으로 아이디가 있는지 갈린다.
public sealed class LoginThrottle(AppDbContext db, IConfiguration config)
{
    /// <summary>이만큼은 그냥 틀려도 된다.</summary>
    private int FreeAttempts => Math.Max(config.GetValue<int?>("Auth:Lockout:FreeAttempts") ?? 5, 1);

    /// <summary>한 번에 잠그는 최대 길이(분).</summary>
    private int MaxLockMinutes => Math.Max(config.GetValue<int?>("Auth:Lockout:MaxMinutes") ?? 30, 1);

    /// <summary>이보다 오래된 실패는 세지 않는다.</summary>
    private static readonly TimeSpan LookBack = TimeSpan.FromHours(24);

    /// <summary>잠김으로 세는 실패 이유. 계정 상태(NOT_ACTIVE)와 잠긴 채 들어온 시도(LOCKED)는 세지 않는다 — 잠김 중의 시도를 세면 공격이 이어지는 동안 주인도 영영 못 들어온다.</summary>
    private static readonly string[] CountedReasons =
    [
        LoginFailReason.NotFound, LoginFailReason.BadPassword, LoginFailReason.BadPasswordRecheck,
    ];

    /// <summary>이 아이디가 지금 잠겨 있으면 남은 시간을, 아니면 null 을 준다.</summary>
    public async Task<TimeSpan?> RemainingLockAsync(string loginId, CancellationToken ct = default)
    {
        if (string.IsNullOrEmpty(loginId))
        {
            return null;
        }

        var now = DateTime.UtcNow;
        var since = now - LookBack;
        var cap = FreeAttempts + 10;

        var recent = await db.AccountLoginLogs
            .AsNoTracking()
            .Where(l => l.LoginId == loginId && l.CreatedAt >= since
                && (l.Success || CountedReasons.Contains(l.FailReason!)))
            .OrderByDescending(l => l.CreatedAt)
            .Take(cap)
            .Select(l => new { l.Success, l.CreatedAt })
            .ToListAsync(ct);

        var failures = 0;
        DateTime? lastFailure = null;
        foreach (var row in recent)
        {
            if (row.Success)
            {
                break;
            }

            failures++;
            lastFailure ??= row.CreatedAt;
        }

        var wait = LockFor(failures, FreeAttempts, MaxLockMinutes);
        if (wait is null || lastFailure is null)
        {
            return null;
        }

        var until = DateTime.SpecifyKind(lastFailure.Value, DateTimeKind.Utc) + wait.Value;
        return until > now ? until - now : null;
    }

    /// <summary>연속 실패 수에 따른 잠금 길이. 다섯 번째부터 1 · 2 · 4 · 8 · 16 · 30분.</summary>
    public static TimeSpan? LockFor(int failures, int freeAttempts, int maxMinutes)
    {
        if (failures < freeAttempts)
        {
            return null;
        }

        var step = Math.Min(failures - freeAttempts, 10);
        return TimeSpan.FromMinutes(Math.Min(1 << step, maxMinutes));
    }

    /// <summary>잠김 안내. 남은 시간을 분 단위로 올려 알려 준다.</summary>
    public static string Message(TimeSpan remaining) =>
        $"로그인 시도가 너무 많아 잠시 막았습니다. {Math.Max(1, (int)Math.Ceiling(remaining.TotalMinutes))}분 뒤에 다시 시도해 주십시오.";
}
