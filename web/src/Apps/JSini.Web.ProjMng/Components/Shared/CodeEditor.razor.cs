using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using JSini.Web.Components.Layout;
using BlazorMonaco;
using BlazorMonaco.Editor;

namespace JSini.Web.ProjMng.Components.Shared;

public partial class CodeEditor
{
    [Inject] private ThemeSize Size { get; set; } = default!;

    [Inject] private IJSRuntime Js { get; set; } = default!;

    private StandaloneCodeEditor? _editor;

    /// <summary>Monaco 스크립트를 받았나. 받기 전에는 편집기를 그리지 않는다. 셸 머리에 두면 3MB 를 모든 화면이 받는다.</summary>
    private bool _loaded;

    /// <summary>스크립트를 받아 오는 쪽. 화면을 벗어나면 놓는다.</summary>
    private IJSObjectReference? _loader;

    /// <summary>편집기 DOM 요소 id. 한 화면에 편집기가 여럿일 수 있어 겹치지 않게 만든다.</summary>
    private readonly string _id = $"pm-editor-{Guid.NewGuid():N}";

    /// <summary>입력 자리의 안내 한 줄. Monaco 에는 자리표시 글자가 없어 편집기 위에 적는다.</summary>
    [Parameter] public string? Placeholder { get; set; }

    /// <summary>편집기 우측 상단 액션(복사 · 자동 줄바꿈 · 줄 번호)을 보일지. 기본은 켜짐.</summary>
    [Parameter] public bool ShowActions { get; set; } = true;

    /// <summary>하위 호환성을 위한 별칭. 값이 주어지면 ShowActions 에 우선한다.</summary>
    [Parameter] public bool? ShowToolbar { get; set; }

    private bool EffectiveShowActions => ShowToolbar ?? ShowActions;

    /// <summary>편집기가 스스로 낸 마지막 값. 되먹임을 가르는 기준이다.</summary>
    private string? _lastPushed;

    /// <summary>대기 중인 debounce. 새 입력이 오면 이전 것을 취소한다.</summary>
    private CancellationTokenSource? _debounce;

    private bool _ready;

    /// <summary>자동 줄바꿈 기본값. 켜짐(true)으로 시작한다.</summary>
    [Parameter] public bool WordWrap { get; set; } = true;

    /// <summary>자동 줄바꿈 상태. 아이콘 버튼과 Monaco 옵션을 함께 조종한다.</summary>
    private bool _wordWrap = true;

    /// <summary>줄 번호를 보일지. 기본은 숨김.</summary>
    [Parameter] public bool ShowLineNumbers { get; set; }

    /// <summary>줄 번호 상태. 「…」 메뉴의 스위치와 Monaco 옵션을 함께 조종한다.</summary>
    private bool _lineNumbers;

    /// <summary>편집기 글자 크기(px). 본문과 같은 크기를 따른다. 사다리를 CSS 로 못 걸어 재서 넣는다 — Monaco 는 fontSize 를 숫자로만 받고 커서와 줄 간격을 그 숫자로 잰다.</summary>
    private double _fontPx = 14;

    protected override void OnInitialized()
    {
        _wordWrap = WordWrap;
        _lineNumbers = ShowLineNumbers;

        // 서랍에서 크기를 바꾸면 따라가게 듣는다. 옵션은 만들 때 한 번만 평가되어 여기서 안 들으면 떠 있는 편집기만 옛 크기로 남는다.
        Size.Changed += OnThemeSizeChanged;
    }

    /// <summary>테마 서랍에서 온 알림. 곧바로 await 하면 회로 문맥 밖에서 JS 를 부르므로 InvokeAsync 로 넘긴다.</summary>
    private void OnThemeSizeChanged() => _ = InvokeAsync(ApplyFontSizeAsync);

    /// <summary>본문 글자 크기를 다시 재서 편집기에 민다. 알림은 새 data-dx-size 가 그려지기 전에 오므로 재는 것을 한 박자 미룬다.</summary>
    private async Task ApplyFontSizeAsync()
    {
        if (_loader is not { } loader || _editor is not { } editor)
        {
            return;
        }

        try
        {
            // 브라우저가 새 글자 크기로 한 번 그린 뒤에 잰다.
            await Task.Delay(50);

            var px = await loader.InvokeAsync<double>("baseFontPx");

            if (Math.Abs(px - _fontPx) < 0.5)
            {
                return;
            }

            _fontPx = px;

            await editor.UpdateOptions(new EditorUpdateOptions { FontSize = (int)Math.Round(px) });
        }
        catch (JSDisconnectedException)
        {
            // 회로가 끊겼다. 밀어 넣을 곳이 없다.
        }
        catch (ObjectDisposedException)
        {
            // 화면을 떠났다(_loader 가 이미 놓였다).
        }
    }

    [Parameter] public string? Value { get; set; }

    [Parameter] public EventCallback<string?> ValueChanged { get; set; }

    /// <summary>구문 종류 (<c>pgsql</c> · <c>json</c> · <c>csharp</c> …).</summary>
    [Parameter] public string Language { get; set; } = "pgsql";

    [Parameter] public bool ReadOnly { get; set; }

    /// <summary>CSS 높이. <c>100%</c> 를 주려면 부모에 높이가 정해져 있어야 한다.</summary>
    [Parameter] public string Height { get; set; } = "240px";

    /// <summary>입력이 이만큼(ms) 멈춘 뒤에 <see cref="ValueChanged"/> 를 올린다.</summary>
    [Parameter] public int DebounceMs { get; set; } = 300;

    /// <summary>Ctrl+Enter 키를 눌렀을 때 불린다.</summary>
    [Parameter] public EventCallback OnCtrlEnter { get; set; }

    private async Task HandleKeyDownAsync(KeyboardEvent e)
    {
        if (e.CtrlKey && e.KeyCode == KeyCode.Enter)
        {
            if (OnCtrlEnter.HasDelegate)
            {
                await OnCtrlEnter.InvokeAsync();
            }
        }
    }

    /// <summary>편집기 옵션. ReadOnly 편집기는 DomReadOnly 와 EditContext=false 로 자판을 막는다 — 숨은 칸이 초점을 받으면 휴대폰이 자판을 올린다.</summary>
    private StandaloneEditorConstructionOptions BuildOptions(StandaloneCodeEditor editor) => new()
    {
        Language = ToMonacoLanguage(Language),
        ReadOnly = ReadOnly,
        DomReadOnly = ReadOnly ? true : null,
        EditContext = ReadOnly ? false : null,
        AutomaticLayout = true,
        FontSize = (int)Math.Round(_fontPx),
        LineNumbers = _lineNumbers ? "on" : "off",
        Minimap = new EditorMinimapOptions { Enabled = false },
        ScrollBeyondLastLine = false,
        TabSize = 2,
        WordWrap = _wordWrap ? "on" : "off",
        RenderLineHighlight = "line",
        SmoothScrolling = true,

        // 막대를 Monaco 가 div 로 그려 app.css 의 3px 규칙이 닿지 않는다 — 화면의 다른 막대와 같은 3px 로 맞춘다.
        Scrollbar = new EditorScrollbarOptions
        {
            VerticalScrollbarSize = 3,
            HorizontalScrollbarSize = 3,
        },
    };

    /// <summary>자동 줄바꿈을 켜거나 끈다. updateOptions 로 실시간 반영한다.</summary>
    private async Task SetWordWrapAsync(bool on)
    {
        _wordWrap = on;

        if (_editor is { } editor)
        {
            await editor.UpdateOptions(new EditorUpdateOptions
            {
                WordWrap = _wordWrap ? "on" : "off",
            });
        }
    }

    /// <summary>줄 번호를 켜거나 끈다. 자동 줄바꿈과 같은 길이다.</summary>
    private async Task SetLineNumbersAsync(bool on)
    {
        _lineNumbers = on;

        if (_editor is { } editor)
        {
            await editor.UpdateOptions(new EditorUpdateOptions
            {
                LineNumbers = _lineNumbers ? "on" : "off",
            });
        }
    }

    /// <summary>복사할 글을 편집기에서 직접 읽는다. Value 는 debounce 만큼 뒤처져 방금 친 글자가 빠지기 때문.</summary>
    private async Task<string?> ReadEditorTextAsync()
        => _editor is { } editor ? await editor.GetValue() : Value;

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (firstRender && !_loaded)
        {
            var loader = await Js.InvokeAsync<IJSObjectReference>(
                "import", "./_content/JSini.Web.ProjMng/js/monaco-loader.js");
            _loader = loader;

            // 스크립트를 받는 사이 화면이 치워질 수 있다. 그때 null 로 이어 부르면 회로가 끊긴다.
            try
            {
                await loader.InvokeVoidAsync("ensure");

                // 편집기를 그리기 전에 잰다. 옵션은 만들 때 한 번만 평가된다.
                _fontPx = await loader.InvokeAsync<double>("baseFontPx");
            }
            catch (Exception e) when (e is JSDisconnectedException or ObjectDisposedException)
            {
                return;
            }

            if (_loader is null)
            {
                return;
            }

            _loaded = true;
            StateHasChanged();

            // 이번 렌더에는 편집기가 아직 없다. 다음 렌더에서 이어 간다.
            return;
        }

        if (_editor is not { } editor)
        {
            return;
        }

        // 편집기가 방금 생겼나. firstRender 는 스크립트를 받던 렌더라 조건으로 쓸 수 없다.
        var born = !_ready;

        if (born)
        {
            // 테마는 문서의 data-theme 를 따라간다(theme.js).
            var dark = await Js.InvokeAsync<bool>("jsiniTheme.isDark");
            await Global.SetTheme(Js, dark ? "vs-dark" : "vs");
            _ready = true;
        }

        // 바깥에서 바뀐 값만 민다. 디바운스 대기 중에 밀면 방금 친 내용을 옛 값이 되덮는다.
        var incoming = Value ?? string.Empty;
        var pushed = false;

        if (_debounce is null && incoming != (_lastPushed ?? string.Empty))
        {
            _lastPushed = incoming;
            await editor.SetValue(incoming);
            pushed = true;
        }

        if (born || pushed)
        {
            // 값을 밀 때마다 Layout 을 직접 부른다. AutomaticLayout 은 Blazor Server 에서 제때 걸리지 않아 첫 렌더에 줄이 안 그려지고 값 교체 때 옛 내용이 남는다.
            await editor.Layout();
        }
    }

    /// <summary>편집기 변경 신호. 입력이 DebounceMs 만큼 멈춘 뒤에만 값을 올린다.</summary>
    private async Task OnEditorChangedAsync(ModelContentChangedEvent _)
    {
        _debounce?.Cancel();
        _debounce?.Dispose();
        var cts = _debounce = new CancellationTokenSource();

        try
        {
            await Task.Delay(DebounceMs, cts.Token);

            if (_editor is null)
            {
                return;
            }

            var text = await _editor.GetValue();
            _lastPushed = text;

            // 올리기 전에 대기 표식을 지운다. 부모 렌더가 대기 중으로 보고 새 값 반영을 건너뛰면 안 된다.
            if (ReferenceEquals(_debounce, cts))
            {
                _debounce = null;
            }

            await ValueChanged.InvokeAsync(text);
        }
        catch (OperationCanceledException)
        {
            // 더 새 입력이 왔다. 그쪽 대기가 값을 올린다.
        }
        catch (JSDisconnectedException)
        {
            // 회로가 끊겼다. 올릴 곳이 없다.
        }
        finally
        {
            cts.Dispose();
        }
    }

    /// <summary>대기 중인 debounce 를 끊는다. 편집기 자신은 BlazorMonaco 가 자기 Dispose 에서 치운다.</summary>
    public async ValueTask DisposeAsync()
    {
        Size.Changed -= OnThemeSizeChanged;

        _debounce?.Cancel();
        _debounce = null;

        if (_loader is not null)
        {
            try
            {
                await _loader.DisposeAsync();
            }
            catch (JSDisconnectedException)
            {
                // 회로가 이미 끊긴 뒤다(사용자가 탭을 닫았다). 놓을 것이 없다.
            }

            _loader = null;
        }
    }

    /// <summary>화면이 넘기는 언어 이름을 Monaco 언어 id 로 바꾼다. 모르는 이름은 plaintext 로 떨어뜨린다.</summary>
    private static string ToMonacoLanguage(string? language) =>
        (language ?? string.Empty).Trim().ToLowerInvariant() switch
        {
            "" => "plaintext",
            "c#" or "cs" => "csharp",
            "mssql" => "sql",
            "postgres" or "postgresql" => "pgsql",
            "text" or "txt" => "plaintext",
            var name => name,
        };
}
