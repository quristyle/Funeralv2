using AuthServer.Data;
using AuthServer.DTOs;
using AuthServer.Entities;
using Microsoft.EntityFrameworkCore;

namespace AuthServer.Services;

/// <summary>
/// 접속 기록 서비스 인터페이스
/// </summary>
public interface ILoginLogService
{
    /// <summary>
    /// 로그인 시도를 남긴다. 실패해도 예외를 밖으로 내보내지 않는다 —
    /// 기록은 로그인의 부수 효과일 뿐이고, 기록이 안 됐다고 로그인을 막을 이유가 없다.
    /// </summary>
    Task WriteAsync(
        string? accountId, string loginId, bool success,
        string? failReason, string? ip, string? userAgent);

    /// <summary>
    /// 계정 정보 화면에 보여 줄 활동 정보.
    /// </summary>
    /// <param name="userId">게이트웨이가 넘긴 로그인 아이디</param>
    /// <param name="limit">최근 기록을 몇 줄까지 볼지</param>
    Task<AccountActivityDto> GetActivityAsync(string userId, int limit);
}

/// <summary>
/// 접속 기록 서비스 구현체
/// </summary>
public class LoginLogService : ILoginLogService
{
    /// <summary>'최근 실패' 를 셀 기간</summary>
    private const int FailWindowDays = 30;

    private readonly AppDbContext _db;
    private readonly ILogger<LoginLogService> _logger;

    public LoginLogService(AppDbContext db, ILogger<LoginLogService> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task WriteAsync(
        string? accountId, string loginId, bool success,
        string? failReason, string? ip, string? userAgent)
    {
        try
        {
            _db.AccountLoginLogs.Add(new AccountLoginLog
            {
                AccountId = accountId,
                LoginId = loginId,
                Success = success,
                FailReason = failReason,
                Ip = ip,
                // 아주 긴 값을 보내는 클라이언트가 있다. 화면에 쓰는 만큼만 남긴다.
                UserAgent = Truncate(userAgent, 512),
                CreatedAt = DateTime.UtcNow,
                CreatedBy = loginId
            });

            await _db.SaveChangesAsync();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "접속 기록 저장 실패: {LoginId}", loginId);
        }
    }

    public async Task<AccountActivityDto> GetActivityAsync(string userId, int limit)
    {
        if (limit < 1 || limit > 100) limit = 10;

        // 게이트웨이가 넘기는 값은 로그인 아이디다. 계정 키로 바꿔야 기록을 찾을 수 있다.
        var account = await _db.Accounts
            .Where(a => !a.IsDeleted && (a.UserId == userId || a.Id == userId))
            .Select(a => new { a.Id, a.CreatedAt })
            .FirstOrDefaultAsync();

        if (account is null) return new AccountActivityDto();

        var logs = _db.AccountLoginLogs.Where(l => l.AccountId == account.Id && !l.IsDeleted);

        var successes = await logs
            .Where(l => l.Success)
            .OrderByDescending(l => l.CreatedAt)
            .Take(limit + 1)   // 지금 이 접속 + 지난번을 함께 보려면 한 줄 더 필요하다
            .ToListAsync();

        var since = DateTime.UtcNow.AddDays(-FailWindowDays);

        var result = new AccountActivityDto
        {
            LoginCount = await logs.CountAsync(l => l.Success),
            RecentFailCount = await logs.CountAsync(l => !l.Success && l.CreatedAt >= since),
            AccountAgeDays = Math.Max(0, (int)(DateTime.UtcNow - account.CreatedAt).TotalDays),
            // 최근 기록은 성공·실패를 섞어 보여 준다. 실패만 따로 찾아 들어가지 않아도 된다.
            Recent = await logs
                .OrderByDescending(l => l.CreatedAt)
                .Take(limit)
                .Select(l => ToDto(l))
                .ToListAsync()
        };

        // 지금 이 접속이 첫 줄이므로 그 다음 줄이 '지난번' 이다.
        if (successes.Count > 1) result.PreviousLogin = ToDto(successes[1]);

        var lastFail = await logs
            .Where(l => !l.Success)
            .OrderByDescending(l => l.CreatedAt)
            .FirstOrDefaultAsync();

        if (lastFail is not null) result.LastFail = ToDto(lastFail);

        // 사람이 읽을 기기 이름은 메모리에서 만든다(문자열 처리라 SQL 로 옮길 수 없다).
        foreach (var row in result.Recent) row.Device = DescribeDevice(row.UserAgent);
        if (result.PreviousLogin is not null)
            result.PreviousLogin.Device = DescribeDevice(result.PreviousLogin.UserAgent);
        if (result.LastFail is not null)
            result.LastFail.Device = DescribeDevice(result.LastFail.UserAgent);

        return result;
    }

    private static LoginLogDto ToDto(AccountLoginLog l) => new()
    {
        At = l.CreatedAt,
        Success = l.Success,
        FailReason = l.FailReason,
        Ip = l.Ip,
        UserAgent = l.UserAgent
    };

    private static string? Truncate(string? value, int max)
        => value is null || value.Length <= max ? value : value[..max];

    /// <summary>
    /// User-Agent 를 `Chrome · Windows` 처럼 줄인다.
    /// </summary>
    /// <remarks>
    /// 원문은 길고 대부분이 잡음이라 화면에 그대로 쓰면 표가 무너진다.
    /// 정확한 판별이 목적이 아니다 — "내가 쓰는 그 브라우저가 맞는지" 만 알면 된다.
    /// 원문도 함께 내려주므로 필요하면 화면에서 마우스를 올려 볼 수 있다.
    /// </remarks>
    private static string? DescribeDevice(string? userAgent)
    {
        if (string.IsNullOrWhiteSpace(userAgent)) return null;

        var ua = userAgent;

        // 브라우저는 뒤에 오는 것이 우선이다 — Edge·Whale 도 Chrome 을 함께 적는다.
        var browser =
            ua.Contains("Edg/", StringComparison.OrdinalIgnoreCase) ? "Edge" :
            ua.Contains("Whale", StringComparison.OrdinalIgnoreCase) ? "Whale" :
            ua.Contains("OPR/", StringComparison.OrdinalIgnoreCase) ? "Opera" :
            ua.Contains("SamsungBrowser", StringComparison.OrdinalIgnoreCase) ? "Samsung Internet" :
            ua.Contains("Firefox", StringComparison.OrdinalIgnoreCase) ? "Firefox" :
            ua.Contains("Chrome", StringComparison.OrdinalIgnoreCase) ? "Chrome" :
            ua.Contains("Safari", StringComparison.OrdinalIgnoreCase) ? "Safari" :
            null;

        var os =
            ua.Contains("Windows", StringComparison.OrdinalIgnoreCase) ? "Windows" :
            ua.Contains("Android", StringComparison.OrdinalIgnoreCase) ? "Android" :
            ua.Contains("iPhone", StringComparison.OrdinalIgnoreCase) ? "iPhone" :
            ua.Contains("iPad", StringComparison.OrdinalIgnoreCase) ? "iPad" :
            ua.Contains("Mac OS", StringComparison.OrdinalIgnoreCase) ? "macOS" :
            ua.Contains("Linux", StringComparison.OrdinalIgnoreCase) ? "Linux" :
            null;

        return (browser, os) switch
        {
            (null, null) => null,
            (null, _) => os,
            (_, null) => browser,
            _ => $"{browser} · {os}"
        };
    }
}
