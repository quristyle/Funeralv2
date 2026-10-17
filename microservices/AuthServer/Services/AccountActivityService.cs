using AuthServer.Data;
using JSini.Shared.Infrastructure.Time;
using Microsoft.EntityFrameworkCore;

namespace AuthServer.Services;

/// <summary>
/// 「이 사람이 지금 쓰고 있다」를 계정에 찍는 자리.
/// </summary>
/// <remarks>
/// <para>
/// 찍는 값은 <c>scom.accounts.last_seen_at</c> 하나뿐이다. 이력은 이미
/// 둘(<c>account_login_logs</c> · <c>menu_usage_logs</c>)이 쌓고 있으므로
/// 여기서는 <b>마지막 한 점</b>만 들고 있으면 된다 — 계정 목록이 줄마다
/// 기록 표를 집계하지 않아도 되는 것이 이 칸을 두는 까닭이다.
/// </para>
///
/// <para>
/// [부르는 자리]
/// </para>
///
/// <list type="bullet">
///   <item><c>LoginCompletion.CompleteAsync</c> — 들어온 순간.</item>
///   <item><c>MenuUsageService.RecordAsync</c> — 화면을 옮길 때마다.</item>
/// </list>
///
/// <para>
/// 포털은 <b>한 회로 안에서 라우팅만 바뀌어</b> 화면을 옮겨도 HTTP 요청이
/// 안 나간다(<c>docs/menu-usage.md</c>). 그래서 게이트웨이나 미들웨어에
/// 얹을 자리가 없고, 셸이 스스로 적어 보내는 사용기록 길이 「쓰고 있다」를
/// 아는 거의 유일한 신호다.
/// </para>
/// </remarks>
public interface IAccountActivityService
{
    /// <summary>
    /// 로그인 아이디로 <c>last_seen_at</c> 을 지금으로 민다.
    /// </summary>
    /// <remarks>
    /// <b>실패해도 던지지 않는다.</b> 부르는 두 자리(로그인 · 사용기록) 모두
    /// 이 값 때문에 본일이 막히면 안 된다.
    /// </remarks>
    /// <returns>실제로 밀었으면 <c>true</c>. 너무 자주 불려 건너뛰었으면 <c>false</c>.</returns>
    Task<bool> TouchAsync(string? loginId, CancellationToken ct = default);
}

/// <inheritdoc />
public sealed class AccountActivityService(
    AppDbContext db,
    ILogger<AccountActivityService> logger) : IAccountActivityService
{
    /// <summary>
    /// 이 간격 안에 또 불리면 건너뛴다.
    /// </summary>
    /// <remarks>
    /// <para>
    /// 사람 하나가 한 시간에 화면을 수십 번 옮기는데 그때마다 계정 줄을
    /// 고치면 <b>쓰기가 잦은 표</b>(권한·로그인·목록이 모두 읽는다)에
    /// 쓸데없는 갱신이 쌓인다.
    /// </para>
    /// <para>
    /// 1분이다. 이 칸을 보는 물음이 「어제 썼나 · 석 달째 안 쓰나」라
    /// 분 단위 아래의 정확도는 아무도 쓰지 않는다.
    /// </para>
    /// </remarks>
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
            // **읽고 고치지 않고 한 문장으로 간다.** 간격 판정을 메모리에
            // 들고 있으면 컨테이너가 여럿일 때 서로 모르고, 미리 읽으면
            // 화면을 옮길 때마다 조회가 한 번 는다. 조건을 WHERE 에 넣으면
            // 둘 다 없어진다 — 밀 때가 아니면 0줄이 고쳐지고 끝난다.
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
