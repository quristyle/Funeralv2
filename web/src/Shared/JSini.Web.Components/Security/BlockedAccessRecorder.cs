using JSini.Web.Components.Data;
using JSini.Web.Http;
using JSini.Web.Models;
using Microsoft.Extensions.Logging;

namespace JSini.Web.Components.Security;

/// <summary>
/// 막은 화면을 한 줄씩 적어 둔다(포털관리 「차단 접근기록」). 포털은 회로 안에서 라우팅만 바뀌어 HTTP 로그에 남지 않으므로, 라우터 앞의 문에서 적는다.
/// 사용기록(열린 화면)과 짝이다. 문은 한 번 들어올 때 여러 번 다시 그려지므로 같은 주소는 <see cref="DedupeWindow"/> 안에서 한 번만 적고, 사람이 다시 두드린 것은 적는다.
/// 기다리지 않고 모든 예외를 삼킨다. 기록을 못 적었다고 차단이 풀리면 안 된다. 막는 일과 적는 일은 갈라져 있다.
/// scoped 다. 직전에 무엇을 적었는지를 회로마다 들고 있어야 한다.
/// </summary>
public sealed class BlockedAccessRecorder(
    GatewayClient gateway,
    ClientAddress client,
    ILogger<BlockedAccessRecorder> log)
{
    /// <summary>게이트웨이 경로. AuthServer 의 <c>/blocked-access</c> 다.</summary>
    private const string Endpoint = "auth/blocked-access";

    /// <summary>같은 주소를 이 사이 안에 다시 받으면 같은 차단으로 본다. 문은 밀리초 단위로 여러 번 다시 그리므로 2초면 그것을 덮고, 사람의 재두드림과는 겹치지 않는다.</summary>
    private static readonly TimeSpan DedupeWindow = TimeSpan.FromSeconds(2);

    private string? _lastHref;
    private DateTime _lastAtUtc = DateTime.MinValue;

    /// <summary>직전에 보던 화면. 다음 줄의 「어디서 왔나」가 된다.</summary>
    private string? _lastPath;

    /// <summary>
    /// 막은 화면을 적는다. <b>기다리지 않고 던지지도 않는다.</b>
    /// </summary>
    /// <param name="verdict">문이 내린 판정. 통과면 아무것도 안 한다.</param>
    /// <param name="href">사람이 실제로 연 주소.</param>
    public void Record(ScreenVerdict verdict, string href)
    {
        if (verdict.Allowed || string.IsNullOrWhiteSpace(href))
        {
            return;
        }

        var now = AppTime.UtcNow;

        if (string.Equals(_lastHref, href, StringComparison.OrdinalIgnoreCase)
            && now - _lastAtUtc < DedupeWindow)
        {
            return;
        }

        var record = new BlockedAccessRecordDto
        {
            Href = href,
            RouteKey = verdict.RouteKey,
            ScreenType = verdict.ScreenType,
            MenuPath = verdict.MenuPath,
            Reason = verdict.Reason,

            // 적기 전의 값이다. 아래에서 덮어쓰므로 순서를 바꾸면 모든 줄의 「어디서 왔나」가 자기 자신이 된다.
            FromPath = _lastPath,
            ClientIp = client.Current,
        };

        _lastHref = href;
        _lastAtUtc = now;
        _lastPath = href;

        _ = SendAsync(record);
    }

    /// <summary>막히지 않고 지나간 화면도 자리만 기억해 둔다. 다음 막힘의 「어디서 눌러 들어왔나」를 적으려면 직전 화면이 필요하다.</summary>
    public void Passed(string href)
    {
        if (!string.IsNullOrWhiteSpace(href))
        {
            _lastPath = href;
        }
    }

    private async Task SendAsync(BlockedAccessRecordDto record)
    {
        try
        {
            await gateway.PostAsync(Endpoint, record);
        }
        catch (Exception ex)
        {
            // 삼킨다(머리말). 흔적은 남긴다. 화면이 비어 있을 때 「아무도 안 막혔다」와 「못 적고 있다」를 가를 곳이 여기뿐이다.
            log.LogDebug(ex, "차단 기록을 적지 못했다: {Href}", record.Href);
        }
    }
}
