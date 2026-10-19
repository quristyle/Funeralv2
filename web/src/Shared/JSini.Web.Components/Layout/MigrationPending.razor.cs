using Microsoft.AspNetCore.Components;
using JSini.Web.Abstractions;
using JSini.Web.Components.Menu;

namespace JSini.Web.Components.Layout;

public partial class MigrationPending
{
    [Inject] private IReadOnlyList<IPortalModule> Modules { get; set; } = default!;
    [Inject] private NavigationManager Navigation { get; set; } = default!;
    [Inject] private IMenuProvider Menus { get; set; } = default!;

    /// <summary>끼워 넣는 메뉴(EMBEDDED)의 옛 주소로 들어오면 공용 화면으로 보낸다. 그런 메뉴는 화면을 하나씩 갖고 있었고 그 주소가 즐겨찾기와 띄워 둔 탭에 남아 있다 — 그대로 두면 모니터를 띄워 둔 사람에게 준비 중이 뜬다. 업무 모듈의 포괄 라우트가 모두 이 부품으로 떨어지므로 여기에 둔다. 메뉴는 이미 브라우저 안에 있어 돌 일이 없는 조회다.</summary>
    protected override void OnParametersSet()
    {
        if (Moved.TryGetValue(CurrentPath, out var moved))
        {
            Navigation.NavigateTo(moved, replace: true);
            return;
        }

        if (Embedded(Menus.AllMenus) is { } node)
        {
            Navigation.NavigateTo(EmbeddedRoute.HrefFor(node.Path), replace: true);
        }
    }

    /// <summary>합쳐서 없어진 화면의 옛 주소 → 지금 그 일을 하는 화면. 지운 화면의 주소는 즐겨찾기·띄워 둔 탭·이미 보낸 푸시에 그대로 남아서 지운 것인지 고장인지 구분할 길이 없다. 리다이렉트만 하는 껍데기 라우트를 남기면 그 열쇠를 메뉴가 다시 가리킬 수 있게 되므로 여기에 둔다. 대소문자를 가리지 않고 찾는다 — 물어보는 값은 CurrentPath 라 ? 와 # 뒤는 이미 떨어져 있다.</summary>
    private static readonly IReadOnlyDictionary<string, string> Moved =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            // 포털관리의 「알림 설정」. 장례식장 「환경설정」이 같은 판을 열고
            // 있어서 사이드바 「설정」 묶음에 거의 같은 화면 둘이 나란히 걸려
            // 있었다 — 환경설정 하나로 합쳤다.
            ["/admin/push/setting"] = "/funeral/setting/environment",
        };

    /// <summary>지금 주소를 자기 경로로 가진 끼워 넣기 메뉴. 없으면 <c>null</c>.</summary>
    private MenuNode? Embedded(IReadOnlyList<MenuNode> nodes)
    {
        foreach (var node in nodes)
        {
            // 옛 주소가 두 갈래다: DB 경로 그대로 열리던 것(/projmng/external/jsini)과 별칭이 옮겨 주던 것(/system/server-status/jin114 → /admin/status/jin114). 둘 다 사이드바가 실제로 걸던 주소라 어느 쪽이든 탭에 남아 있다.
            if (node.IsEmbedded
                && !string.IsNullOrWhiteSpace(node.IframeSrc)
                && (string.Equals(node.Path, CurrentPath, StringComparison.OrdinalIgnoreCase)
                    || string.Equals(RouteAliases.Resolve(node.Path), CurrentPath, StringComparison.OrdinalIgnoreCase)))
            {
                return node;
            }

            if (Embedded(node.Children) is { } found)
            {
                return found;
            }
        }

        return null;
    }

    private string CurrentPath => "/" + Navigation
        .ToBaseRelativePath(Navigation.Uri)
        .Split('?', '#')[0]
        .TrimStart('/');

    /// <summary>이 주소를 소유한 모듈. 접두사가 가장 긴 것을 고른다 — 접두사가 겹치는 모듈은 아키텍처 테스트가 막지만 고르는 규칙 자체는 겹쳐도 옳게 동작하는 쪽으로 둔다.</summary>
    private IPortalModule? Module => Modules
        .Where(m => CurrentPath.StartsWith(m.RoutePrefix, StringComparison.OrdinalIgnoreCase))
        .MaxBy(m => m.RoutePrefix.Length);
}
