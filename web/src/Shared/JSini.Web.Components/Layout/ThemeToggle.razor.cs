using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

namespace JSini.Web.Components.Layout;

public partial class ThemeToggle
{
    [Inject] private IJSRuntime Js { get; set; } = default!;
    [Inject] private ThemeSize Size { get; set; } = default!;
    [Inject] private ThemeDrawer Drawer { get; set; } = default!;
    [Inject] private PortalBoot Boot { get; set; } = default!;
    [Inject] private ILogger<ThemeToggle> Log { get; set; } = default!;

    /// <summary>theme.js 가 알려 주는 목록.</summary>
    private sealed record Catalog(
        IReadOnlyList<ModeInfo> Modes,
        IReadOnlyList<AccentInfo> Accents,
        IReadOnlyList<ClassicInfo> Classic,
        IReadOnlyList<ClassicInfo> Bootstrap,
        IReadOnlyList<SizeInfo> Sizes);

    private sealed record ModeInfo(string Id, string Name, bool Dark);
    private sealed record AccentInfo(string Id, string Name, string Swatch);

    /// <summary>크기 하나. DevExpress 가 주는 셋뿐이다 (Small · Medium · Large).</summary>
    private sealed record SizeInfo(string Id, string Name);

    /// <summary>한 장짜리 테마. Classic 과 Bootstrap 이 같은 모양이라 함께 쓴다.</summary>
    private sealed record ClassicInfo(string Id, string Name, bool Dark, string Swatch);

    /// <summary>지금 고른 것. theme.js 의 모양 그대로다.</summary>
    private sealed record Current(
        string Family, string? Mode, string? Accent, string? Custom,
        string? Classic, string? Bootstrap, string? Size);

    private Catalog _catalog = new([], [], [], [], []);

    /// <summary>목록을 이미 받았는가. 서랍을 처음 열 때 한 번만 받는다.</summary>
    private bool _catalogLoaded;

    private bool _open;
    private string _family = "fluent";
    private string _mode = "dark";
    private string _accent = "blue";
    private string? _custom;
    private string? _classic;
    private string? _bootstrap;

    private bool IsFluentMode(string id) => _family == "fluent" && _mode == id;
    private bool IsFluentAccent(string id) => _family == "fluent" && _custom is null && _accent == id;
    private bool IsClassic(string id) => _family == "classic" && _classic == id;
    private bool IsBootstrap(string id) => _family == "bootstrap" && _bootstrap == id;

    // 크기는 테마 묶음과 따로 논다 — Classic 을 골라도 크기는 그대로다.
    // **단계로 견준다.** Size.Current(SizeMode)로 견주면 아주작게·작게·조금작게가
    // 모두 Small 이라 세 칸에 동시에 표시가 붙는다.
    private bool IsSize(string id) => Size.Step == id;

    /// <summary>
    /// 사용자 메뉴의 「환경설정」이 이 서랍을 연다. 그 부품이 여기를 직접
    /// 잡지 않고 서비스를 거치는 이유는 <see cref="ThemeDrawer"/> 머리말에 있다.
    /// </summary>
    protected override void OnInitialized() => Drawer.OpenRequested += OnOpenRequested;

    public void Dispose() => Drawer.OpenRequested -= OnOpenRequested;

    /// <summary>
    /// 사용자 메뉴가 서랍을 열라고 할 때. 단추를 누른 것과 <b>같은 길</b>로
    /// 보낸다 — 여기서 <c>_open</c> 을 바로 세우면 목록을 받지 않은 채 열려
    /// 빈 서랍이 뜬다.
    /// </summary>
    private void OnOpenRequested(bool open) => InvokeAsync(async () =>
    {
        if (open)
        {
            await EnsureCatalogAsync();
        }

        _open = open;
        StateHasChanged();
    });

    /// <summary>
    /// 지금 고른 테마를 서버 쪽에 맞춘다.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>고를 수 있는 목록(<c>catalog</c>)은 여기서 받지 않는다.</b> 스물둘의
    /// 이름과 색이 전부 실려 오는데 <b>서랍을 열지 않으면 한 줄도 안 쓴다</b> —
    /// 게다가 포털은 업무를 넘나들 때마다 레이아웃을 새로 만들어서 그 짐이
    /// 화면 전환마다 다시 실렸다. 이제 <see cref="ToggleAsync"/> 가 <b>처음
    /// 열 때</b> 한 번 받는다.
    /// </para>
    ///
    /// <para>
    /// 지금 고른 것(<c>current</c>)은 반대로 여기서 받아야 한다. 서랍과 무관하게
    /// <see cref="Read"/> 가 <c>Size.Set</c> 을 부르는데, 그게 <b>쿠키를 막아 둔
    /// 브라우저에서 크기를 맞추는 유일한 자리</b>다. 다만 왕복을 따로 내지는
    /// 않는다 — <see cref="PortalBoot"/> 가 잠금 표시·공지 표시·고정 탭과 함께
    /// 한 번에 실어 온다.
    /// </para>
    /// </remarks>
    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (!firstRender)
        {
            return;
        }

        if ((await Boot.ReadAsync()).Theme is { } theme)
        {
            Read(new Current(
                theme.Family, theme.Mode, theme.Accent, theme.Custom,
                theme.Classic, theme.Bootstrap, theme.Size));

            StateHasChanged();
        }
    }

    /// <summary>
    /// 서랍을 여닫는다. <b>처음 열 때</b> 고를 수 있는 목록을 받아 온다.
    /// </summary>
    /// <remarks>
    /// 받아 온 것은 들고 있는다 — 목록은 theme.js 안에 박힌 상수라 바뀌지 않는다.
    /// 그래서 두 번째부터는 왕복이 없다.
    /// </remarks>
    private async Task ToggleAsync()
    {
        if (_open)
        {
            _open = false;
            return;
        }

        await EnsureCatalogAsync();
        _open = true;
    }

    private async Task EnsureCatalogAsync()
    {
        if (_catalogLoaded)
        {
            return;
        }

        try
        {
            _catalog = await Js.InvokeAsync<Catalog>("jsiniTheme.catalog");
            _catalogLoaded = true;
        }
        catch (JSException ex)
        {
            // theme.js 가 안 실린 경우다. 서랍은 비어서 열린다 — 여기서
            // 던지면 회로가 내려가고 화면 전체가 굳는다.
            Log.LogDebug(ex, "테마 목록을 받지 못했다. 서랍이 비어서 열린다.");
        }
    }

    private async Task SetFluentAsync(string mode, string accent)
    {
        // 프리셋을 고르면 사용자 지정 색은 물러난다. 둘을 함께 켜 두면
        // 어느 쪽이 이겼는지 화면만 보고는 알 수 없다.
        Read(await Js.InvokeAsync<Current>("jsiniTheme.setFluent", mode, accent, null));
    }

    private async Task SetCustomAsync(string? hex)
    {
        var trimmed = hex?.Trim();

        // 여섯 자리 색이 아니면 무시한다. 반쯤 친 값마다 테마를 바꾸면
        // 글자 칸에 타이핑하는 동안 화면이 요동친다.
        if (!string.IsNullOrEmpty(trimmed) && !IsHex(trimmed))
        {
            return;
        }

        Read(await Js.InvokeAsync<Current>("jsiniTheme.setFluent", _mode, _accent, trimmed));
    }

    /// <summary>
    /// 크기를 바꾼다. <b>두 곳을 함께 움직여야 한다.</b>
    ///
    /// · theme.js — 우리 CSS 의 글자 크기 사다리(<c>--jsini-fs-*</c>)와 저장·쿠키
    /// · <see cref="ThemeSize"/> — DevExpress 부품이 받는 SizeMode
    ///
    /// 한쪽만 바꾸면 글자만 커지고 단추·그리드는 그대로이거나(또는 그 반대)
    /// 한 화면에 두 크기가 된다. 저장은 theme.js 쪽만 한다 — 서버는 다음
    /// 새로고침 때 그 쿠키를 읽는다.
    /// </summary>
    private async Task SetSizeAsync(string id)
    {
        Read(await Js.InvokeAsync<Current>("jsiniTheme.setSize", id));
        Size.Set(id);
    }

    private async Task SetClassicAsync(string id) =>
        Read(await Js.InvokeAsync<Current>("jsiniTheme.setClassic", id));

    private async Task SetBootstrapAsync(string id) =>
        Read(await Js.InvokeAsync<Current>("jsiniTheme.setBootstrap", id));

    private void Read(Current? c)
    {
        if (c is null) return;

        _family = c.Family;
        _mode = c.Mode ?? "dark";
        _accent = c.Accent ?? "blue";
        _custom = c.Custom;
        _classic = c.Classic;
        _bootstrap = c.Bootstrap;

        // 브라우저가 아는 크기를 서버 쪽에도 맞춘다.
        //
        // 보통은 이미 같다 — 서버가 첫 그림을 그릴 때 같은 값을 쿠키에서
        // 읽었기 때문이다. 다른 경우는 쿠키를 막아 둔 브라우저다. 그때는
        // 브라우저 쪽(localStorage)이 정답이므로 여기서 한 번 맞춰 준다.
        Size.Set(c.Size);
    }

    private static bool IsHex(string value)
    {
        var body = value.StartsWith('#') ? value[1..] : value;
        return body.Length == 6 && body.All(Uri.IsHexDigit);
    }
}
