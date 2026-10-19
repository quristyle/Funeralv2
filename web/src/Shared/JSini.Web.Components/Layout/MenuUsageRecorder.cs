using JSini.Web.Abstractions;
using JSini.Web.Http;
using JSini.Web.Models;
using JSini.Web.Components.Data;
using Microsoft.Extensions.Logging;

namespace JSini.Web.Components.Layout;

/// <summary>화면을 열 때마다 한 줄씩 적어 둔다 — 포털관리의 메뉴 사용기록이 본다. 포털은 한 회로 안에서 라우팅만 바뀌어 화면을 옮겨도 HTTP 요청이 안 나가 서버 접근 로그에는 남지 않으므로 레이아웃에서 적는다.
/// MainLayout.Track 이 메뉴를 찾아 탭을 여는 자리에 얹는다 — 거기까지 왔으면 메뉴에 있고 볼 권한도 있어 로그인·오류·준비 중이 저절로 빠진다. 한 번 옮길 때 두 번 이상 불리므로(주소 변경 · 늦게 온 메뉴 셈) 시간으로 같은 이동을 가려낸다 — 같은 주소를 막으면 같은 메뉴를 다시 누른 것이 영영 안 적힌다. 기다리지 않고 모든 예외를 삼킨다 — 기록을 못 적어 화면이 안 열리면 본말이 뒤집힌다. scoped — 직전 경로를 들고 있어야 하므로 싱글턴이면 남이 옮긴 자리가 내 기록의 어디서 왔나로 적힌다.</summary>
public sealed class MenuUsageRecorder(GatewayClient gateway, ILogger<MenuUsageRecorder> log)
{
    /// <summary>게이트웨이 경로. AuthServer 의 <c>/menu-usage</c> 다.</summary>
    private const string Endpoint = "auth/menu-usage";

    /// <summary>같은 주소를 이 사이 안에 다시 받으면 같은 이동으로 본다. 한 번 옮길 때 레이아웃이 여러 번 셈하는 간격은 밀리초 단위이고, 사람이 같은 메뉴를 다시 누르는 간격은 빨라도 1초를 넘는다 — 2초가 그 사이를 덮는다.</summary>
    private static readonly TimeSpan DedupeWindow = TimeSpan.FromSeconds(2);

    /// <summary>마지막으로 적은 주소와 그때.</summary>
    private string? _lastHref;
    private DateTime _lastAtUtc = DateTime.MinValue;

    /// <summary>직전에 본 화면의 메뉴 경로. 다음 줄의 「어디서 왔나」가 된다 — 주소가 아니라 경로인 것은 조회가 경로로 묶기 때문이다(주소로 적으면 같은 화면이 옛 주소와 새 주소로 갈린다).</summary>
    private string? _lastPath;

    /// <summary>이 화면을 열었다고 적는다. 기다리지 않고 던지지도 않는다.</summary>
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
