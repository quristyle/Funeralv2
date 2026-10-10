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

    private sealed record ModeInfo(string Id, string Name, bool Dark);
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

    private bool IsSize(string id) => Size.Step == id;

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

        Size.Set(c.Size);
    }
}
