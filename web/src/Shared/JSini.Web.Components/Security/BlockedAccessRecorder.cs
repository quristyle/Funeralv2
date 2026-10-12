using JSini.Web.Components.Data;
using JSini.Web.Http;
using JSini.Web.Models;
using Microsoft.Extensions.Logging;

namespace JSini.Web.Components.Security;

/// <summary>
/// 막은 화면을 <b>한 줄씩 적어 둔다</b> — 포털관리의 「차단 접근기록」이 보는 그 표다.
/// </summary>
/// <remarks>
/// <para>
/// [왜 서버 로그로는 안 되나]
/// </para>
///
/// <para>
/// 포털은 한 회로 안에서 라우팅만 바뀐다. 주소를 쳐서 들어와도 <b>HTTP 요청이
/// 한 건도 나가지 않으므로</b> nginx 접근 로그에는 로그인 한 번과 WebSocket
/// 하나뿐이다 — 막혔다는 사실을 아는 자리는 라우터 앞의 문 하나뿐이고, 그래서
/// 거기서 적어 보낸다(<c>MenuUsageRecorder</c> 와 같은 까닭이다).
/// </para>
///
/// <para>
/// [사용기록과 짝이다 — 한쪽만으로는 반쪽이다]
/// </para>
///
/// <para>
/// <c>menu_usage_logs</c> 는 <b>열린 화면</b>만 적는다(그쪽은 넣는 길에 권한을
/// 묻지 않는다 — 「권한 없이 열린 화면이 기록에서 빠지면 안 된다」). 이제 그
/// 화면들이 실제로 막히므로, 막힌 줄은 그 표에 영영 안 남는다. 그 자리를
/// 이 표가 받는다.
/// </para>
///
/// <para>
/// [같은 차단을 두 번 적지 않는다]
/// </para>
///
/// <para>
/// 문은 한 번 들어올 때 여러 번 다시 그려진다 — 늦게 도착한 권한표·메뉴가
/// 판정을 다시 돌리기 때문이다. 그대로 두면 한 번 막힌 것이 기록에는 서넛으로
/// 남는다. 그렇다고 「같은 주소면 안 적는다」로 두면 <b>같은 주소를 다시
/// 두드린 것</b>이 영영 안 적히는데, 그것이야말로 이 표가 보고 싶은 줄이다.
/// 그래서 가르는 기준이 <b>시간</b>이다(<see cref="DedupeWindow"/>).
/// </para>
///
/// <para>
/// [기다리지 않는다]
/// </para>
///
/// <para>
/// 부르는 자리가 <b>차단 안내를 그리기 직전</b>이다. 거기서 게이트웨이 왕복을
/// 기다리면 안내가 그만큼 늦게 뜬다. 떼어 보내고 바로 돌아오며 <b>모든 예외를
/// 삼킨다</b> — 기록을 못 적어서 차단이 풀리면 본말이 뒤집힌다.
/// <b>막는 일과 적는 일은 갈라져 있다</b>: 적는 쪽이 통째로 죽어도 막는 쪽은 돈다.
/// </para>
///
/// <para>
/// scoped 다 — 회로 하나가 곧 사용자 한 명의 창 하나다. 직전에 무엇을 적었는지를
/// 들고 있어야 하는데, 싱글턴으로 두면 남이 막힌 자리가 내 가려내기에 걸린다.
/// </para>
/// </remarks>
public sealed class BlockedAccessRecorder(
    GatewayClient gateway,
    ClientAddress client,
    ILogger<BlockedAccessRecorder> log)
{
    /// <summary>게이트웨이 경로. AuthServer 의 <c>/blocked-access</c> 다.</summary>
    private const string Endpoint = "auth/blocked-access";

    /// <summary>같은 주소를 이 사이 안에 다시 받으면 <b>같은 차단</b>으로 본다.</summary>
    /// <remarks>
    /// 한 번 들어올 때 문이 여러 번 다시 그려지는데 그 사이는 밀리초 단위다.
    /// 2초면 그 모두를 덮으면서, 사람이 같은 주소를 다시 두드리는 간격과는
    /// 겹치지 않는다(<c>MenuUsageRecorder.DedupeWindow</c> 와 같은 값이다).
    /// </remarks>
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

            // **적기 전의 값**이다. 아래에서 덮어쓰므로 순서를 바꾸면 모든 줄의
            // 「어디서 왔나」가 자기 자신이 된다.
            FromPath = _lastPath,
            ClientIp = client.Current,
        };

        _lastHref = href;
        _lastAtUtc = now;
        _lastPath = href;

        _ = SendAsync(record);
    }

    /// <summary>
    /// 막히지 않고 지나간 화면도 <b>자리만</b> 기억해 둔다.
    /// </summary>
    /// <remarks>
    /// 다음에 막힐 때 「어디서 눌러 들어왔나」를 적으려면 직전 화면을 알아야
    /// 한다. 막힌 줄만 기억하면 그 칸이 늘 <c>null</c> 이거나 <b>지난번에 막힌
    /// 주소</b>가 되어, 실제로는 거기서 온 적이 없는데 그렇게 읽힌다.
    /// </remarks>
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
            // 삼킨다(머리말). 다만 흔적은 남긴다 — 화면이 비어 있을 때
            // 「아무도 안 막혔다」와 「못 적고 있다」를 가를 곳이 여기뿐이다.
            log.LogDebug(ex, "차단 기록을 적지 못했다: {Href}", record.Href);
        }
    }
}
