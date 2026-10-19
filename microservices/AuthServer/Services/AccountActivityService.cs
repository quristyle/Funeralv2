using AuthServer.Data;
using JSini.Shared.Infrastructure.Time;
using Microsoft.EntityFrameworkCore;

namespace AuthServer.Services;

/// <summary>「이 사람이 지금 쓰고 있다」를 계정에 찍는다. 찍는 값은 <c>scom.accounts.last_seen_at</c> 하나뿐이고, 이력은 account_login_logs · menu_usage_logs 가 이미 쌓는다.</summary>
public interface IAccountActivityService
{
    /// <summary>로그인 아이디로 last_seen_at 을 지금으로 민다. 실패해도 던지지 않는다 — 부르는 로그인·사용기록 자리가 이 값 때문에 막히면 안 된다.</summary>
    /// <returns>실제로 밀었으면 <c>true</c>. 간격 안이면 <c>false</c>.</returns>
    Task<bool> TouchAsync(string? loginId, CancellationToken ct = default);
}

/// <inheritdoc />
public sealed class AccountActivityService(
    AppDbContext db,
    ILogger<AccountActivityService> logger) : IAccountActivityService
{
    /// <summary>이 간격 안에 또 불리면 건너뛴다. 1분이다 — 화면을 수십 번 옮길 때마다 계정 줄을 고치면 쓰기가 잦은 표에 쓸데없는 갱신이 쌓인다.</summary>
    public static readonly TimeSpan TouchInterval = TimeSpan.FromMinutes(1);

    /// <inheritdoc />
    public async Task<bool> TouchAsync(string? loginId, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(loginId)) return false;

        var id = loginId.Trim();
        var now = AppTime.UtcNow;
        var cutoff = now - TouchInterval;

        try
        {
            // 읽고 고치지 않고 한 문장으로 간다 — 간격 판정을 메모리에 두면 컨테이너가 여럿일 때 서로 모르고, 미리 읽으면 화면을 옮길 때마다 조회가 한 번 는다. 조건을 WHERE 에 넣는다.
            var changed = await db.Accounts
                .Where(a => a.UserId == id
                    && !a.IsDeleted
                    && (a.LastSeenAt == null || a.LastSeenAt < cutoff))
                .ExecuteUpdateAsync(s => s.SetProperty(a => a.LastSeenAt, now), ct);

            return changed > 0;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "마지막 사용시각을 적지 못했습니다: {LoginId}", id);
            return false;
        }
    }
}
