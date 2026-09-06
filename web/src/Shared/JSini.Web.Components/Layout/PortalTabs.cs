using JSini.Web.Abstractions;

namespace JSini.Web.Components.Layout;

/// <summary>
/// 열어 둔 업무 화면 목록 — vben 의 탭 바를 옮긴 것.
///
/// [한 프로세스가 되면서 오히려 쉬워졌다]
///
/// 업무 앱이 각자 프로세스이던 시절에는 탭을 만들 수 없었다. 업무를 옮길 때마다
/// 브라우저가 문서를 새로 받아서 탭 상태를 들고 있을 자리가 없었기 때문이다.
/// 지금은 한 회로 안에서 라우팅만 바뀌므로 이 서비스가 그대로 살아 있는다.
///
/// [탭이 라우팅을 하지 않는다]
///
/// 탭을 누르면 <c>NavigationManager</c> 로 이동하고, <b>이동한 결과를 보고</b>
/// 탭이 켜진다. 반대로 만들면(탭이 화면을 직접 갈아 끼우면) 주소와 화면이
/// 어긋나서 새로 고침·뒤로 가기·즐겨찾기가 전부 깨진다.
///
/// [화면 상태는 탭에 남지 않는다]
///
/// vben 은 <c>keep-alive</c> 로 떠난 화면의 컴포넌트를 살려 두었다. Blazor 에서
/// 같은 일을 하려면 라우터가 화면을 감춰 두어야 하는데, 그러면 화면 서른 개가
/// 회로 하나에 살아 있게 되고 그중 자동 갱신하는 화면(빈소현황·SM 모니터링)이
/// 보이지 않는 채로 계속 서버를 부른다. <b>탭은 주소 목록일 뿐</b>이고 돌아가면
/// 화면은 다시 그려진다.
///
/// scoped 다 — 회로 하나가 곧 사용자 한 명의 창 하나다.
/// </summary>
public sealed class PortalTabs
{
    /// <summary>
    /// 한 번에 열어 둘 수 있는 탭 수.
    ///
    /// 넘으면 <b>가장 오래 안 본 탭</b>부터 닫는다. 무한정 열어 두면 탭 줄이
    /// 두세 줄이 되어 본문이 좁아지고, 그 상태에서는 탭이 오히려 방해가 된다.
    /// vben 도 같은 이유로 상한을 두었다.
    /// </summary>
    private const int MaxTabs = 12;

    private readonly List<PortalTab> _tabs = [];

    /// <summary>열린 탭들. 왼쪽부터 열린 순서다.</summary>
    public IReadOnlyList<PortalTab> Items => _tabs;

    /// <summary>지금 보고 있는 탭의 주소.</summary>
    public string? ActiveHref { get; private set; }

    /// <summary>탭이 바뀌었다. 탭 줄이 다시 그린다.</summary>
    public event Action? Changed;

    /// <summary>
    /// 지금 주소를 탭으로 만든다. 이미 있으면 그것을 켠다.
    /// </summary>
    /// <param name="href">전체 경로 (<c>/projmng/proj/wbs</c>). 질의 문자열은 뗀 것.</param>
    /// <param name="title">탭에 보일 이름. 메뉴 제목이다.</param>
    public void Open(string href, string title)
    {
        if (string.IsNullOrWhiteSpace(href))
        {
            return;
        }

        ActiveHref = href;

        var existing = _tabs.FirstOrDefault(t => Same(t.Href, href));

        if (existing is not null)
        {
            // 이름이 늦게 정해질 수 있다. 메뉴를 아직 못 읽었을 때 주소로 열면
            // 이름이 경로였다가 메뉴가 오면 제목으로 바뀐다.
            if (!string.IsNullOrWhiteSpace(title) && existing.Title != title)
            {
                existing.Title = title;
            }

            existing.LastSeen = DateTime.UtcNow;
            Changed?.Invoke();
            return;
        }

        _tabs.Add(new PortalTab
        {
            Href = href,
            Title = string.IsNullOrWhiteSpace(title) ? href : title,
            LastSeen = DateTime.UtcNow,
        });

        Trim();
        Changed?.Invoke();
    }

    /// <summary>탭 하나를 닫는다.</summary>
    /// <returns>닫은 뒤 옮겨 갈 주소. 옮길 필요가 없으면 <c>null</c>.</returns>
    public string? Close(string href)
    {
        var index = _tabs.FindIndex(t => Same(t.Href, href));
        if (index < 0)
        {
            return null;
        }

        // 고정한 탭은 닫지 않는다.
        if (_tabs[index].Pinned)
        {
            return null;
        }

        var wasActive = Same(ActiveHref, href);
        _tabs.RemoveAt(index);
        Changed?.Invoke();

        if (!wasActive)
        {
            return null;
        }

        // 닫은 자리의 옆 탭으로 간다. 오른쪽이 없으면 왼쪽, 그것도 없으면 첫 화면.
        if (_tabs.Count == 0)
        {
            ActiveHref = null;
            return "/";
        }

        var next = _tabs[Math.Min(index, _tabs.Count - 1)];
        ActiveHref = next.Href;
        return next.Href;
    }

    /// <summary>고정을 켜고 끈다. 고정한 탭은 닫기와 전체 닫기에서 살아남는다.</summary>
    public void TogglePin(string href)
    {
        var tab = _tabs.FirstOrDefault(t => Same(t.Href, href));
        if (tab is null) return;

        tab.Pinned = !tab.Pinned;
        Changed?.Invoke();
    }

    /// <summary>지금 탭만 남기고 닫는다. 고정한 탭은 남는다.</summary>
    /// <returns>옮겨 갈 주소. 보고 있던 탭이 닫혔을 때만 값이 있다.</returns>
    public string? CloseOthers(string href)
    {
        _tabs.RemoveAll(t => !Same(t.Href, href) && !t.Pinned);
        Changed?.Invoke();

        return AfterBulkClose();
    }

    /// <summary>
    /// 이 탭의 <b>왼쪽</b> 탭들을 닫는다. 고정한 탭은 남는다.
    ///
    /// <para>
    /// 「다른 탭 닫기」와 따로 두는 이유는 쓰는 방식이 다르기 때문이다 —
    /// 왼쪽은 이미 지나온 화면이고 오른쪽은 방금 벌여 둔 화면이다.
    /// 하나로 뭉개면 둘 중 하나는 늘 아까운 것을 함께 닫는다.
    /// </para>
    /// </summary>
    /// <returns>옮겨 갈 주소. 보고 있던 탭이 닫혔을 때만 값이 있다.</returns>
    public string? CloseLeft(string href) => CloseSide(href, left: true);

    /// <summary>이 탭의 <b>오른쪽</b> 탭들을 닫는다. 고정한 탭은 남는다.</summary>
    /// <returns>옮겨 갈 주소. 보고 있던 탭이 닫혔을 때만 값이 있다.</returns>
    public string? CloseRight(string href) => CloseSide(href, left: false);

    private string? CloseSide(string href, bool left)
    {
        var pivot = _tabs.FindIndex(t => Same(t.Href, href));
        if (pivot < 0)
        {
            return null;
        }

        // 자리로 지운다. 지우는 도중에 자리가 밀리므로 뒤에서 앞으로 간다.
        for (var i = _tabs.Count - 1; i >= 0; i--)
        {
            if (i == pivot || _tabs[i].Pinned)
            {
                continue;
            }

            if (left ? i < pivot : i > pivot)
            {
                _tabs.RemoveAt(i);
            }
        }

        Changed?.Invoke();
        return AfterBulkClose();
    }

    /// <summary>
    /// 여럿을 닫은 뒤 보고 있던 탭이 사라졌는지 본다.
    ///
    /// <para>
    /// 사라졌으면 옮겨 갈 주소를 돌려준다. 안 옮기면 주소는 그대로인데 그
    /// 탭만 없는 상태가 되어, 탭 줄과 화면이 어긋난 채로 남는다.
    /// </para>
    /// </summary>
    private string? AfterBulkClose()
    {
        if (_tabs.Any(t => Same(t.Href, ActiveHref)))
        {
            return null;
        }

        if (_tabs.Count == 0)
        {
            ActiveHref = null;
            return "/";
        }

        ActiveHref = _tabs[0].Href;
        return _tabs[0].Href;
    }

    /// <summary>전부 닫는다. 고정한 탭은 남는다.</summary>
    /// <returns>옮겨 갈 주소.</returns>
    public string CloseAll()
    {
        _tabs.RemoveAll(t => !t.Pinned);
        Changed?.Invoke();

        if (_tabs.Count > 0)
        {
            ActiveHref = _tabs[0].Href;
            return _tabs[0].Href;
        }

        ActiveHref = null;
        return "/";
    }

    /// <summary>상한을 넘으면 가장 오래 안 본 탭부터 닫는다. 고정한 탭은 세지 않는다.</summary>
    private void Trim()
    {
        while (_tabs.Count(t => !t.Pinned) > MaxTabs)
        {
            var oldest = _tabs
                .Where(t => !t.Pinned && !Same(t.Href, ActiveHref))
                .OrderBy(t => t.LastSeen)
                .FirstOrDefault();

            if (oldest is null) break;

            _tabs.Remove(oldest);
        }
    }

    private static bool Same(string? a, string? b) =>
        string.Equals(a, b, StringComparison.OrdinalIgnoreCase);
}

/// <summary>열어 둔 화면 하나.</summary>
public sealed class PortalTab
{
    /// <summary>전체 경로. 탭의 신원이다.</summary>
    public required string Href { get; init; }

    /// <summary>탭에 보일 이름.</summary>
    public required string Title { get; set; }

    /// <summary>고정했는가. 고정한 탭은 닫기에서 살아남는다.</summary>
    public bool Pinned { get; set; }

    /// <summary>마지막으로 본 때. 상한을 넘었을 때 무엇을 닫을지 고르는 데 쓴다.</summary>
    public DateTime LastSeen { get; set; }
}
