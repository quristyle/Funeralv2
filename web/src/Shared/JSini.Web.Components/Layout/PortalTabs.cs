using JSini.Web.Abstractions;

namespace JSini.Web.Components.Layout;

/// <summary>열어 둔 업무 화면 목록(vben 의 탭 바를 옮긴 것). 탭은 주소 목록일 뿐이라 화면 상태를 들고 있지 않는다 — 떠난 화면을 살려 두면 자동 갱신 화면이 보이지 않는 채 서버를 계속 부른다. 메뉴에서 「화면 유지」를 켠 화면만 KeepAliveScreens 가 탭이 열린 동안 살려 둔다. 탭이 화면을 직접 갈아 끼우지 않고 라우팅 결과를 보고 켠다. scoped — 회로 하나가 곧 창 하나다.</summary>
public sealed class PortalTabs
{
    /// <summary>한 번에 열어 둘 탭 수. 넘으면 가장 오래 안 본 탭부터 닫는다 — 무한정 열면 탭 줄이 여러 줄이 되어 본문이 좁아진다.</summary>
    private const int MaxTabs = 12;

    private readonly List<PortalTab> _tabs = [];

    /// <summary>열린 탭들. 왼쪽부터 열린 순서다.</summary>
    public IReadOnlyList<PortalTab> Items => _tabs;

    /// <summary>지금 보고 있는 탭의 주소.</summary>
    public string? ActiveHref { get; private set; }

    /// <summary>지금 보고 있는 활성 탭. 못 찾으면 첫 탭이고, 탭이 없으면 <c>null</c> 이다.</summary>
    public PortalTab? ActiveTab =>
        _tabs.FirstOrDefault(t => Same(t.Href, ActiveHref)) ?? _tabs.FirstOrDefault();

    /// <summary>탭이 바뀌었다. 탭 줄이 다시 그린다.</summary>
    public event Action? Changed;

    /// <summary>이 주소를 탭으로 둬도 되는가 — 볼 권한이 있는 화면인가. 레이아웃이 채운다. 고정 탭은 브라우저에 적어 둔 주소에서 되살아나므로 권한이 빠진 뒤에도 남는다 — 여기서 거르지 않으면 볼 수 없는 화면이 탭 줄에 섰다. 판정 규칙은 이 클래스가 모르고 비어 있으면 거르지 않는다.</summary>
    public Func<string, bool>? CanShow { get; set; }

    private bool Allowed(string href) => CanShow?.Invoke(href) ?? true;

    /// <summary>볼 수 없게 된 탭을 걷는다. 고정한 탭도 걷는다 — 고정은 늘 열어 둔다는 뜻이지 권한을 넘는다는 뜻이 아니다. 권한표가 늦게 오므로 되살린 뒤에도 한 번 더 부른다. 걷은 고정 탭은 저장본에서도 빠진다.</summary>
    public void Prune()
    {
        if (CanShow is null)
        {
            return;
        }

        var removed = _tabs.RemoveAll(t => !Allowed(t.Href));

        if (removed == 0)
        {
            return;
        }

        if (ActiveHref is not null && !_tabs.Any(t => Same(t.Href, ActiveHref)))
        {
            ActiveHref = null;
        }

        Changed?.Invoke();
        PinsChanged?.Invoke();
    }

    /// <summary>지금 주소를 탭으로 만든다. 이미 있으면 그것을 켠다.</summary>
    /// <param name="href">전체 경로(질의 문자열은 뗀 것).</param>
    /// <param name="title">탭에 보일 이름. 메뉴 제목이다.</param>
    /// <param name="standalone">메뉴에 없는 화면인가. 참이면 탭 줄이 메뉴에 딸린 즐겨찾기 항목을 내놓지 않는다(PortalTab.Standalone).</param>
    public void Open(string href, string title, bool standalone = false)
    {
        if (string.IsNullOrWhiteSpace(href))
        {
            return;
        }

        ActiveHref = href;

        var existing = _tabs.FirstOrDefault(t => Same(t.Href, href));

        if (existing is not null)
        {
            // 메뉴를 아직 못 읽었으면 이름이 경로로 열렸다가 메뉴가 오면 제목으로 바뀐다.
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
            Standalone = standalone,
            LastSeen = DateTime.UtcNow,
        });

        Trim();
        Changed?.Invoke();
    }

    /// <summary>탭 하나를 닫는다.</summary>
    /// <returns>닫은 뒤 옮겨 갈 주소. 옮길 필요가 없으면 null.</returns>
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
        PinsChanged?.Invoke();
    }

    /// <summary>고정 목록이 바뀌었다. 브라우저에 적어 두는 쪽이 듣는다. Changed 와 따로 두는 것은 울리는 횟수가 달라서다 — 화면을 옮길 때마다 울리는 쪽에 물리면 매번 저장소에 쓴다.</summary>
    public event Action? PinsChanged;

    /// <summary>고정해 둔 탭들. 브라우저에 적어 둘 값이다.</summary>
    public IReadOnlyList<PinnedTab> PinnedTabs =>
        [.. _tabs.Where(t => t.Pinned).Select(t => new PinnedTab(t.Href, t.Title, t.Standalone))];

    /// <summary>적어 둔 고정 탭을 되살린다. 회로가 새로 열릴 때 한 번 부른다 — 탭 목록은 회로에 살아 F5 로 사라지므로 고정은 여기서 되살린다. 되살린 탭은 앞쪽에 적어 둔 순서대로 놓고, 보고 있는 화면은 건드리지 않는다(주소가 바뀌면 새로고침마다 엉뚱한 화면으로 튄다).</summary>
    public void RestorePinned(IEnumerable<PinnedTab> pinned)
    {
        var at = 0;
        var skipped = false;

        foreach (var item in pinned)
        {
            if (string.IsNullOrWhiteSpace(item.Href))
            {
                continue;
            }

            // 볼 권한이 없는 화면은 되살리지 않는다(위 CanShow 머리말).
            if (!Allowed(item.Href))
            {
                skipped = true;
                continue;
            }

            var existing = _tabs.FirstOrDefault(t => Same(t.Href, item.Href));

            if (existing is not null)
            {
                // 지금 보고 있는 화면이 그 탭일 수 있다 — 자리는 두고 고정 표시만 켠다. 옮기면 보던 탭이 눈앞에서 뛴다.
                existing.Pinned = true;
                continue;
            }

            _tabs.Insert(Math.Min(at++, _tabs.Count), new PortalTab
            {
                Href = item.Href,
                Title = string.IsNullOrWhiteSpace(item.Title) ? item.Href : item.Title,
                Pinned = true,

                // 메뉴에 없는 화면이었다는 사실도 함께 적어 둔다 — 안 그러면 되살린 탭에서만 즐겨찾기 항목이 되살아난다.
                Standalone = item.Standalone,
                LastSeen = DateTime.UtcNow,
            });
        }

        Changed?.Invoke();

        // 건너뛴 것은 저장본에서도 뺀다 — 남기면 새로고침마다 다시 거르고 저장본과 화면이 어긋난다.
        if (skipped)
        {
            PinsChanged?.Invoke();
        }
    }

    /// <summary>지금 탭만 남기고 닫는다. 고정한 탭은 남는다.</summary>
    /// <returns>옮겨 갈 주소. 보고 있던 탭이 닫혔을 때만 값이 있다.</returns>
    public string? CloseOthers(string href)
    {
        _tabs.RemoveAll(t => !Same(t.Href, href) && !t.Pinned);
        Changed?.Invoke();

        return AfterBulkClose();
    }

    /// <summary>이 탭의 왼쪽 탭들을 닫는다. 고정한 탭은 남는다. 「다른 탭 닫기」와 따로 두는 것은 왼쪽은 지나온 화면이고 오른쪽은 방금 벌인 화면이라 쓰는 방식이 다르기 때문이다.</summary>
    /// <returns>옮겨 갈 주소. 보고 있던 탭이 닫혔을 때만 값이 있다.</returns>
    public string? CloseLeft(string href) => CloseSide(href, left: true);

    /// <summary>이 탭의 오른쪽 탭들을 닫는다. 고정한 탭은 남는다.</summary>
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

    /// <summary>여럿을 닫은 뒤 보고 있던 탭이 사라졌는지 본다. 사라졌으면 옮겨 갈 주소를 돌려준다 — 안 옮기면 탭 줄과 화면이 어긋난다.</summary>
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

/// <summary>브라우저에 적어 두는 고정 탭 한 줄. 이름까지 적는 것은 되살릴 때 메뉴가 아직 안 와 있기 때문이다.</summary>
/// <param name="Href">탭의 신원인 전체 경로.</param>
/// <param name="Title">탭에 보일 이름.</param>
/// <param name="Standalone">메뉴에 없는 화면인가. 예전에 적어 둔 값에는 이 칸이 없으므로 거짓이 기본이다 — 그때 고정할 수 있던 탭은 전부 메뉴 화면이었다.</param>
public sealed record PinnedTab(string Href, string Title, bool Standalone = false);

/// <summary>열어 둔 화면 하나.</summary>
public sealed class PortalTab
{
    /// <summary>전체 경로. 탭의 신원이다.</summary>
    public required string Href { get; init; }

    /// <summary>탭에 보일 이름.</summary>
    public required string Title { get; set; }

    /// <summary>고정했는가. 고정한 탭은 닫기에서 살아남는다.</summary>
    public bool Pinned { get; set; }

    /// <summary>메뉴에 없는 화면인가 — 자료 번호가 주소에 들어가는 화면(/projmng/ai/task/1234)이 그렇다. 탭 줄은 이 값이 참이면 메뉴에 딸린 즐겨찾기 항목을 감춘다 — 즐겨찾기는 메뉴 경로를 열쇠로 쌓이므로 표에 없는 주소를 담으면 사이드바에서 영영 못 푸는 줄이 생긴다. 고정은 그대로 둔다(탭의 성질이다).</summary>
    public bool Standalone { get; init; }

    /// <summary>마지막으로 본 때. 상한을 넘었을 때 무엇을 닫을지 고르는 데 쓴다.</summary>
    public DateTime LastSeen { get; set; }
}
