using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

namespace JSini.Web.Components.Layout;

public partial class ThemeSettingsDrawer
{
    [Inject] private IJSRuntime Js { get; set; } = default!;
    [Inject] private ThemeSize Size { get; set; } = default!;
    [Inject] private ThemeDrawer Drawer { get; set; } = default!;
    [Inject] private PortalBoot Boot { get; set; } = default!;
    [Inject] private ILogger<ThemeSettingsDrawer> Log { get; set; } = default!;

    [Parameter] public bool IsPhone { get; set; }

    private sealed record Catalog(
        IReadOnlyList<ModeInfo> Modes,
        IReadOnlyList<ColorInfo> Colors,
        IReadOnlyList<ColorInfo> Bases,
        IReadOnlyList<Choice> Radii,
        IReadOnlyList<Choice> Sizes,
        IReadOnlyList<Choice>? Fonts);

    /// <summary>
    /// 밝기 한 칸. <paramref name="Icon"/> 는 <c>jsini-icon-</c> 뒤에 붙일 이름이다
    /// (sun · moon · display). <c>시스템</c> 은 밝지도 어둡지도 않아
    /// <paramref name="Dark"/> 로는 그림을 고를 수 없다.
    /// </summary>
    private sealed record ModeInfo(string Id, string Name, bool Dark, string? Icon = null);
    private sealed record ColorInfo(string Id, string Name, string Swatch);
    private sealed record Choice(string Id, string Name);

    private sealed record Current(
        string? Mode, string? Color, string? Base, string? Radius, string? Size, string? Font = null);

    private Catalog _catalog = new([], [], [], [], [], []);

    private bool _catalogLoaded;

    private bool _open;
    private bool _pinned;
    private string _mode = "light";
    private string _color = "blue";
    private string _base = "neutral";
    private string _radius = "1";
    private string _font = "play";
    private string _sizeStep = "medium";

    private bool IsSize(string id) => (Size.Step ?? _sizeStep) == id;

    private int GetSizeMax() => _catalog.Sizes.Count > 0 ? _catalog.Sizes.Count - 1 : 8;

    private int GetSizeIndex(string step)
    {
        var list = _catalog.Sizes;
        if (list.Count == 0) return 4;
        for (var i = 0; i < list.Count; i++)
        {
            if (list[i].Id == step) return i;
        }
        return 4;
    }

    private string GetSizeName(string step)
    {
        var list = _catalog.Sizes;
        for (var i = 0; i < list.Count; i++)
        {
            if (list[i].Id == step) return list[i].Name;
        }
        return "보통";
    }

    private void OnRadiusInput(string? value)
    {
        if (!string.IsNullOrWhiteSpace(value))
        {
            _radius = value;
            StateHasChanged();
        }
    }

    private async Task SetRadiusAsync(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return;
        _radius = value;
        await SetAsync("setRadius", value);
    }

    private void OnSizeInput(string? value)
    {
        if (int.TryParse(value, out var idx) && idx >= 0 && idx < _catalog.Sizes.Count)
        {
            _sizeStep = _catalog.Sizes[idx].Id;
            StateHasChanged();
        }
    }

    private async Task OnSizeChangeAsync(string? value)
    {
        if (int.TryParse(value, out var idx) && idx >= 0 && idx < _catalog.Sizes.Count)
        {
            var id = _catalog.Sizes[idx].Id;
            _sizeStep = id;
            await SetSizeAsync(id);
        }
    }

    /// <summary>
    /// 그 밝기 칸에 세울 그림 이름. 목록이 들고 온 것을 그대로 쓰고,
    /// 없으면 <c>dark</c> 로 해·달을 고른다 — 그림 칸이 없던 때의 목록을
    /// 들고 있는 브라우저(옛 theme.js 가 캐시에 남은 경우)에서도 빈 네모가
    /// 서지 않게.
    /// </summary>
    private static string ModeIcon(ModeInfo mode) =>
        string.IsNullOrEmpty(mode.Icon) ? (mode.Dark ? "moon" : "sun") : mode.Icon;

    protected override void OnInitialized() => Drawer.OpenRequested += OnOpenRequested;

    public void Dispose() => Drawer.OpenRequested -= OnOpenRequested;

    private void OnOpenRequested(bool open) => InvokeAsync(async () =>
    {
        if (open)
        {
            await EnsureCatalogAsync();
        }

        _open = open;
        StateHasChanged();
    });

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (!firstRender)
        {
            return;
        }

        if ((await Boot.ReadAsync()).Theme is { } theme)
        {
            Read(new Current(theme.Mode, theme.Color, theme.Base, theme.Radius, theme.Size, theme.Font));
            StateHasChanged();
        }
        else
        {
            _sizeStep = Size.Step ?? "medium";
        }
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
            Log.LogDebug(ex, "테마 목록을 받지 못했다. 서랍이 비어서 열린다.");
        }
    }

    private async Task SetAsync(string setter, string id) =>
        Read(await Js.InvokeAsync<Current>("jsiniTheme." + setter, id));

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
        _font = c.Font ?? "play";
        _sizeStep = c.Size ?? Size.Step ?? "medium";

        Size.Set(c.Size);
    }
}
