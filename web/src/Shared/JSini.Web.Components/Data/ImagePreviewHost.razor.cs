using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

namespace JSini.Web.Components.Data;

public partial class ImagePreviewHost : IAsyncDisposable
{
    [Inject] private ImagePreview Preview { get; set; } = default!;
    [Inject] private IJSRuntime Js { get; set; } = default!;

    private const string ModulePath = "./_content/JSini.Web.Components/js/image-preview.js";

    private IJSObjectReference? _module;
    private DotNetObjectReference<ImagePreviewHost>? _self;

    private ElementReference _stage;
    private ElementReference _img;

    /// <summary>
    /// 지금 JS 에 붙여 둔 그림의 주소. <b>이것이 바뀌면 다시 붙인다</b> —
    /// 다음 장으로 넘어갔는데 확대율과 회전이 남아 있으면, 세로 사진 뒤의
    /// 가로 사진이 옆으로 누운 채 세 배로 뜬다.
    /// </summary>
    private string? _attached;

    /// <summary>문서에 문지기를 세웠나. 한 회로에 한 번이면 된다.</summary>
    private bool _watching;

    /// <summary>도구띠를 그리려고 받아 둔 사본. 정본은 브라우저에 있다.</summary>
    private ImagePreviewState _state = ImagePreviewState.Fresh;

    protected override void OnInitialized() => Preview.Changed += OnPreviewChanged;

    private void OnPreviewChanged() => _ = InvokeAsync(StateHasChanged);

    private static string TitleOf(ImagePreviewItem item) =>
        string.IsNullOrWhiteSpace(item.Title) ? "그림" : item.Title!;

    private string ZoomText => $"{Math.Round(_state.Zoom * 100)}%";

    /// <summary>
    /// 밝기·대비 표시. <b>기본값이면 아무것도 안 적는다</b> — 늘 「100% · 100%」가
    /// 서 있으면 도구띠 한 칸이 언제나 뜻 없이 차지된다.
    /// </summary>
    private string LevelText
    {
        get
        {
            if (_state.Brightness == 100 && _state.Contrast == 100)
            {
                return string.Empty;
            }

            return $"밝기 {_state.Brightness}% · 대비 {_state.Contrast}%";
        }
    }

    /// <summary>
    /// 도구띠의 단추 하나.
    /// </summary>
    /// <param name="Name">JS 쪽 <c>command()</c> 가 아는 이름.</param>
    /// <param name="Label">단추에 쓰는 글자.</param>
    /// <param name="Hint">마우스를 올렸을 때. 단축키도 여기 적는다.</param>
    /// <param name="Divider">앞에 가르는 줄을 하나 세울지.</param>
    private sealed record Tool(string Name, string Label, string Hint, bool Divider = false);

    /// <summary>
    /// 확대·축소를 뺀 나머지 단추들. <b>목록으로 두는 이유</b>는 마크업에서
    /// 글자 상수를 쓰지 않기 위해서다 — Razor 는 큰따옴표로 연 속성 안의
    /// 큰따옴표를 속성의 끝으로 읽는다.
    /// </summary>
    private static readonly Tool[] Tools =
    [
        new("fit", "맞춤", "화면에 맞춘다 (0 키)", Divider: true),
        new("actual", "1:1", "원래 크기로 본다 (1 키)"),

        new("rotateLeft", "↺", "왼쪽으로 90° 돌린다 (Shift+R)", Divider: true),
        new("rotateRight", "↻", "오른쪽으로 90° 돌린다 (R)"),

        new("flipX", "⇋", "좌우를 뒤집는다", Divider: true),
        new("flipY", "⇅", "상하를 뒤집는다"),

        new("gray", "흑백", "흑백으로 본다 (G)", Divider: true),
        new("invert", "반전", "색을 반전한다"),

        new("darker", "밝기−", "어둡게", Divider: true),
        new("brighter", "밝기+", "밝게"),

        new("less", "대비−", "대비를 낮춘다", Divider: true),
        new("more", "대비+", "대비를 높인다"),

        new("reset", "원래대로", "처음 모습으로 되돌린다", Divider: true),
    ];

    /// <summary>켜진 단추는 눌린 채로 보인다. 끌 수 있다는 것을 그림으로 알린다.</summary>
    private string ClassOf(Tool tool) => tool.Name switch
    {
        "flipX" when _state.FlipX => "jsini-imgview__btn jsini-imgview__btn--on",
        "flipY" when _state.FlipY => "jsini-imgview__btn jsini-imgview__btn--on",
        "gray" when _state.Gray => "jsini-imgview__btn jsini-imgview__btn--on",
        "invert" when _state.Invert => "jsini-imgview__btn jsini-imgview__btn--on",
        _ => "jsini-imgview__btn",
    };

    /// <summary>
    /// 창이 열렸으면 JS 를 그림에 붙이고, 닫혔으면 뗀다.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>문지기(<c>watch</c>)는 창이 닫혀 있어도 세운다.</b> 본문 그림을
    /// 눌러 창을 여는 길이 그것이라, 창이 열린 뒤에 세우면 첫 번째 누름이
    /// 아무 일도 하지 않는다.
    /// </para>
    /// <para>
    /// 프리렌더 중에는 여기가 돌지 않는다 — JS 를 부를 수 없는 그 구간을
    /// 이 자리가 이미 지나 있다.
    /// </para>
    /// </remarks>
    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        try
        {
            _module ??= await Js.InvokeAsync<IJSObjectReference>("import", ModulePath);
            _self ??= DotNetObjectReference.Create(this);

            if (!_watching)
            {
                _watching = true;
                await _module.InvokeVoidAsync("watch", _self);
            }

            if (Preview.Current is { } current)
            {
                if (_attached != current.Url)
                {
                    _attached = current.Url;
                    _state = await _module.InvokeAsync<ImagePreviewState>("attach", _stage, _img, _self)
                             ?? ImagePreviewState.Fresh;

                    // 도구띠를 새 상태로 다시 그린다. 여기서 한 번 더 그려도
                    // 다음 차례에는 주소가 같아 이 갈래로 안 들어온다.
                    StateHasChanged();
                }
            }
            else if (_attached is not null)
            {
                _attached = null;
                _state = ImagePreviewState.Fresh;
                await _module.InvokeVoidAsync("detach");
            }
        }
        catch (Exception ex) when (ex is JSException or JSDisconnectedException or ObjectDisposedException)
        {
            // 회로가 닫혔거나 아직 붙지 않았다. 미리보기 하나 때문에 화면이
            // 통째로 끊기는 것이 훨씬 나쁘다.
        }
    }

    private Task ZoomInAsync() => CommandAsync("zoomIn");

    private Task ZoomOutAsync() => CommandAsync("zoomOut");

    /// <summary>도구띠의 단추를 브라우저로 넘기고 돌아온 모습을 받아 둔다.</summary>
    private async Task CommandAsync(string name)
    {
        if (_module is null)
        {
            return;
        }

        try
        {
            _state = await _module.InvokeAsync<ImagePreviewState>("command", name)
                     ?? ImagePreviewState.Fresh;
        }
        catch (Exception ex) when (ex is JSException or JSDisconnectedException or ObjectDisposedException)
        {
            // 위와 같다.
        }
    }

    private void Close() => Preview.Close();

    private void Prev() => Preview.Step(-1);

    private void Next() => Preview.Step(1);

    /// <summary>휠·손가락으로 바뀐 모습을 브라우저가 알려 온다.</summary>
    [JSInvokable]
    public Task OnState(ImagePreviewState? state)
    {
        _state = state ?? ImagePreviewState.Fresh;
        return InvokeAsync(StateHasChanged);
    }

    /// <summary>빈자리를 눌렀거나 Esc 를 눌렀다.</summary>
    [JSInvokable]
    public Task OnBackdrop()
    {
        Preview.Close();
        return Task.CompletedTask;
    }

    /// <summary>← → 로 앞뒤 그림을 부른다.</summary>
    [JSInvokable]
    public Task OnStep(int delta)
    {
        Preview.Step(delta);
        return Task.CompletedTask;
    }

    /// <summary>
    /// 본문 안의 그림을 눌렀다. 문지기가 그 칸의 그림을 통째로 넘겨 준다.
    /// </summary>
    /// <remarks>
    /// 이름은 <c>alt</c> · <c>title</c> 에서 온다. 서식 편집기가 붙인 그림에는
    /// 둘 다 없는 경우가 대부분이라, 없으면 머리띠에 「그림」이라고만 적힌다.
    /// </remarks>
    [JSInvokable]
    public Task OpenFromDom(string[] urls, string[] titles, int index)
    {
        var items = urls
            .Select((url, i) => new ImagePreviewItem(
                url,
                i < titles.Length && !string.IsNullOrWhiteSpace(titles[i]) ? titles[i] : null))
            .ToArray();

        Preview.OpenAll(items, index);
        return InvokeAsync(StateHasChanged);
    }

    public async ValueTask DisposeAsync()
    {
        Preview.Changed -= OnPreviewChanged;

        if (_module is not null)
        {
            try
            {
                await _module.InvokeVoidAsync("unwatch");
                await _module.InvokeVoidAsync("detach");
                await _module.DisposeAsync();
            }
            catch (Exception ex) when (ex is JSException or JSDisconnectedException or ObjectDisposedException)
            {
                // 브라우저가 이미 떠났다. 떼어 낼 것도 함께 사라졌다.
            }
        }

        _self?.Dispose();
    }
}
