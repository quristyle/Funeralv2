using JSini.Web.Abstractions;
using JSini.Web.Http;
using JSini.Web.Models;
using JSini.Web.Components.Data;
using Microsoft.Extensions.Logging;

namespace JSini.Web.Components.Layout;

/// <summary>
/// 화면을 열 때마다 <b>한 줄씩 적어 둔다</b> — 포털관리의 「메뉴 사용기록」이 보는 그 표다.
/// </summary>
/// <remarks>
/// <para>
/// [왜 서버 로그로는 안 되나]
/// </para>
///
/// <para>
/// 포털은 한 회로 안에서 라우팅만 바뀐다. 화면을 옮겨도 <b>HTTP 요청이 한 건도
/// 나가지 않으므로</b> 접근 로그에는 아무것도 안 남는다 — 나중에 뒤져도 거기에는
/// 로그인 한 번과 WebSocket 하나뿐이다. 「누가 어떤 화면을 보았나」를 아는 자리는
/// <b>레이아웃 하나뿐</b>이고, 그래서 거기서 적어 보낸다.
/// </para>
///
/// <para>
/// [언제 적나 — 레이아웃이 메뉴를 알아낸 그 자리]
/// </para>
///
/// <para>
/// <c>MainLayout.Track</c> 이 주소로 메뉴를 찾아 탭을 여는 자리에 얹는다. 거기까지
/// 왔다는 것은 <b>메뉴에 있는 화면이고 볼 권한도 있다</b>는 뜻이라, 로그인 화면·
/// 오류 화면·「준비 중」이 저절로 빠진다.
/// </para>
///
/// <para>
/// [같은 이동을 두 번 적지 않는다]
/// </para>
///
/// <para>
/// 그 자리는 한 번 옮길 때 <b>두 번 이상</b> 불린다 — 주소가 바뀔 때 한 번
/// (<c>OnLocationChanged</c>), 늦게 도착한 메뉴·권한표가 브레드크럼을 다시
/// 셈할 때 또 한 번(<c>OnMenusChanged</c>). 그대로 두면 화면 하나를 연 것이
/// 기록에는 둘로 남는다.
/// </para>
///
/// <para>
/// 그렇다고 「같은 주소면 안 적는다」로 두면 <b>같은 메뉴를 다시 누른 것</b>이
/// 영영 안 적힌다 — 그것은 사람이 실제로 한 일이다. 그래서 가르는 기준을
/// <b>시간</b>으로 둔다(<see cref="DedupeWindow"/>): 같은 주소라도 그 사이를
/// 넘겨 다시 오면 새 열람이다.
/// </para>
///
/// <para>
/// [기다리지 않는다]
/// </para>
///
/// <para>
/// 부르는 자리가 <b>화면을 그리기 직전</b>이다. 거기서 게이트웨이 왕복을
/// 기다리면 화면 전환이 그만큼 늦어진다 — 기록은 곁다리고 화면이 본일이다.
/// 떼어 보내고 바로 돌아오며, <b>모든 예외를 삼킨다.</b> 기록을 못 적어서
/// 화면이 안 열리면 본말이 뒤집힌다(<c>AiUsageLog</c> 와 같은 선이다).
/// </para>
///
/// <para>
/// <b>그래서 몇 건은 잃을 수 있다.</b> 회로가 그 사이에 끊기면 그 줄은 없다.
/// 이 숫자는 회계가 아니라 「누가 무엇을 보나」를 사람이 보는 것이 목적이라
/// 그 정도로 충분하다.
/// </para>
///
/// <para>
/// scoped 다 — 회로 하나가 곧 사용자 한 명의 창 하나다. 직전에 무엇을 보았는지
/// (<see cref="_lastPath"/>)를 들고 있어야 하는데, 싱글턴으로 두면 남이 옮긴
/// 자리가 내 기록의 「어디서 왔나」로 적힌다.
/// </para>
/// </remarks>
public sealed class MenuUsageRecorder(GatewayClient gateway, ILogger<MenuUsageRecorder> log)
{
    /// <summary>게이트웨이 경로. AuthServer 의 <c>/menu-usage</c> 다.</summary>
    private const string Endpoint = "auth/menu-usage";

    /// <summary>
    /// 같은 주소를 이 사이 안에 다시 받으면 <b>같은 이동</b>으로 본다.
    /// </summary>
    /// <remarks>
    /// 한 번 옮길 때 레이아웃이 여러 번 셈하는데(머리말) 그 사이는 밀리초
    /// 단위다. 2초면 그 모두를 덮으면서, 사람이 같은 메뉴를 다시 누르는
    /// 간격(빨라도 1초가 넘는다)과는 겹치지 않는다.
    /// </remarks>
    private static readonly TimeSpan DedupeWindow = TimeSpan.FromSeconds(2);

    /// <summary>마지막으로 적은 주소와 그때.</summary>
    private string? _lastHref;
    private DateTime _lastAtUtc = DateTime.MinValue;

    /// <summary>
    /// 직전에 본 화면의 <b>메뉴 경로</b>. 다음 줄의 「어디서 왔나」가 된다.
    /// </summary>
    /// <remarks>
    /// 주소가 아니라 경로인 것은 조회가 경로로 묶기 때문이다 — 주소로 적어
    /// 두면 같은 화면이 옛 주소와 새 주소 둘로 갈린다(<c>RouteAliases</c>).
    /// </remarks>
    private string? _lastPath;

    /// <summary>
    /// 이 화면을 열었다고 적는다. <b>기다리지 않고 던지지도 않는다.</b>
    /// </summary>
    /// <param name="node">레이아웃이 주소로 찾아낸 메뉴.</param>
    /// <param name="href">실제로 열린 주소. 질의 문자열은 뗀 것이다.</param>
    public void Record(MenuNode node, string href)
    {
        if (string.IsNullOrWhiteSpace(node.Path)) return;

        var now = AppTime.UtcNow;

        // 한 번 옮긴 것을 두 번 적지 않는다(머리말).
        if (string.Equals(_lastHref, href, StringComparison.OrdinalIgnoreCase)
            && now - _lastAtUtc < DedupeWindow)
        {
            return;
        }

        var record = new MenuUsageRecordDto
        {
            MenuPath = node.Path,
            RouteKey = node.RouteKey,
            MenuTitle = node.Title,
            Href = href,

            // **적기 전의 값**이다. 아래에서 덮어쓰므로 순서를 바꾸면 모든 줄의
            // 「어디서 왔나」가 자기 자신이 된다.
            FromPath = _lastPath,
        };

        _lastHref = href;
        _lastAtUtc = now;
        _lastPath = node.Path;

        _ = SendAsync(record);
    }

    private async Task SendAsync(MenuUsageRecordDto record)
    {
        try
        {
            await gateway.PostAsync(Endpoint, record);
        }
        catch (Exception ex)
        {
            // 삼킨다(머리말). 다만 흔적은 남긴다 — 화면이 비어 있을 때
            // 「아무도 안 썼다」와 「못 적고 있다」를 가를 곳이 여기뿐이다.
            log.LogDebug(ex, "메뉴 사용기록을 적지 못했다: {Path}", record.MenuPath);
        }
    }
}
