using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

namespace JSini.Web.Components.Layout;

/// <summary>
/// 테마 서랍. 고르는 것은 Tabler 의 Customize 판과 같다 — 밝기 · 강조색 ·
/// 바탕 톤 · 모서리, 그리고 우리가 더한 크기. 값을 적용하고 저장하는 일은
/// 전부 theme.js 가 한다. 여기는 목록을 그리고 고른 것을 넘길 뿐이다.
/// </summary>
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
        IReadOnlyList<ColorInfo> Colors,
        IReadOnlyList<ColorInfo> Bases,
        IReadOnlyList<Choice> Radii,
        IReadOnlyList<Choice> Sizes);

    private sealed record ModeInfo(string Id, string Name, bool Dark);
    private sealed record ColorInfo(string Id, string Name, string Swatch);
    private sealed record Choice(string Id, string Name);

    /// <summary>지금 고른 것. theme.js 의 모양 그대로다.</summary>
    private sealed record Current(string? Mode, string? Color, string? Base, string? Radius, string? Size);

    private Catalog _catalog = new([], [], [], [], []);

    /// <summary>목록을 이미 받았는가. 서랍을 처음 열 때 한 번만 받는다.</summary>
    private bool _catalogLoaded;

    private bool _open;
    private string _mode = "light";
    private string _color = "blue";
    private string _base = "neutral";
    private string _radius = "1";

    // 크기는 **단계로 견준다.** SizeMode 로 견주면 아주작게·작게·조금작게가
    // 모두 Small 이라 세 칸에 동시에 표시가 붙는다.
    private bool IsSize(string id) => Size.Step == id;

    protected override void OnInitialized() => Drawer.OpenRequested += OnOpenRequested;

    public void Dispose() => Drawer.OpenRequested -= OnOpenRequested;

    /// <summary>
    /// 사용자 메뉴가 서랍을 열라고 할 때. 단추를 누른 것과 같은 길로 보낸다 —
    /// 목록을 받지 않은 채 열면 빈 서랍이 뜬다.
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
    /// 지금 고른 테마를 서버 쪽에 맞춘다. 왕복을 따로 내지 않는다 —
    /// <see cref="PortalBoot"/> 가 다른 표시들과 함께 한 번에 실어 온다.
    /// </summary>
    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (!firstRender)
        {
            return;
        }

        if ((await Boot.ReadAsync()).Theme is { } theme)
        {
            Read(new Current(theme.Mode, theme.Color, theme.Base, theme.Radius, theme.Size));
            StateHasChanged();
        }
    }

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
            // theme.js 가 안 실린 경우다. 여기서 던지면 회로가 내려간다.
            Log.LogDebug(ex, "테마 목록을 받지 못했다. 서랍이 비어서 열린다.");
        }
    }

    private async Task SetAsync(string setter, string id) =>
        Read(await Js.InvokeAsync<Current>("jsiniTheme." + setter, id));

    /// <summary>
    /// 크기를 바꾼다. theme.js(글자 사다리·저장·쿠키)와 <see cref="ThemeSize"/>
    /// (DevExpress SizeMode)를 <b>함께</b> 움직인다 — 한쪽만 바꾸면 한 화면에 두 크기가 된다.
    /// </summary>
    private async Task SetSizeAsync(string id)
    {
        await SetAsync("setSize", id);
        Size.Set(id);
    }

    private void Read(Current? c)
    {
        if (c is null) return;

        _mode = c.Mode ?? "light";
        _color = c.Color ?? "blue";
        _base = c.Base ?? "neutral";
        _radius = c.Radius ?? "1";

        // 쿠키를 막아 둔 브라우저에서는 브라우저 쪽이 정답이라 여기서 맞춘다.
        Size.Set(c.Size);
    }
}
