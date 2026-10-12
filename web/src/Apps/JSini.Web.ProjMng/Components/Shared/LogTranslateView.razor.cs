using BlazorMonaco.Editor;
using JSini.Web.Components.Layout;
using JSini.Web.Http;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

namespace JSini.Web.ProjMng.Components.Shared;

public partial class LogTranslateView
{
    [Inject] private AiLogTranslator Xlat { get; set; } = default!;

    [Inject] private AiLogXlatStore Store { get; set; } = default!;

    [Inject] private IJSRuntime Js { get; set; } = default!;

    [Inject] private Toasts Toasts { get; set; } = default!;

    [Inject] private ThemeSize Size { get; set; } = default!;

    /// <summary>
    /// 볼 로그 줄. <b>부모가 그리는 것과 같은 줄이어야 한다</b> — 원문 칸이
    /// 로그 탭의 글상자와 다른 것을 보여 주면 둘을 오가는 사람이 헷갈린다.
    /// </summary>
    [Parameter] public IReadOnlyList<string> Lines { get; set; } = [];

    /// <summary>
    /// 옮긴 것을 어느 이름으로 들고 있을지. <b>건마다 달라야 한다</b> —
    /// 같으면 다른 지시를 열었을 때 앞 건의 번역이 그대로 보인다.
    /// </summary>
    [Parameter, EditorRequired] public string StateKey { get; set; } = string.Empty;

    /// <summary>
    /// 지금 보이고 있나. <b>거짓이 되면 하던 번역을 멈춘다</b> — 탭을 떠난
    /// 사람 때문에 로컬 LLM 이 계속 돌 까닭이 없다.
    /// </summary>
    [Parameter] public bool Active { get; set; } = true;

    /// <summary>
    /// 열자마자 옮기기 시작할지. <b>기본은 참</b> — 이 판은 「비교해서 보겠다」를
    /// 누른 사람에게만 서므로, 단추를 한 번 더 누르게 할 까닭이 없다.
    ///
    /// <para>
    /// 탭을 건너갔다 돌아왔을 때도 이것이 <b>멈춘 자리에서 이어 가게</b> 한다
    /// (<see cref="AiLogXlatStore"/>).
    /// </para>
    /// </summary>
    [Parameter] public bool AutoStart { get; set; } = true;

    /// <summary>고를 수 있는 말.</summary>
    private sealed record LangOption(string Code, string Name);

    private static readonly LangOption[] Languages =
    [
        new("ko", "한국어"),
        new("en", "English"),
    ];

    /// <summary>
    /// 한 덩이에 실어 보내는 글자 수의 상한.
    /// </summary>
    /// <remarks>
    /// 서버의 상한(<c>LLMService.MaxTranslateInput</c> = 12000)보다 넉넉히
    /// 낮게 둔다. 로그에는 한 줄이 수백 글자인 것이 섞여 있어, 줄 수만 세면
    /// 스무 줄이 상한을 넘겨 <b>그 덩이만 조용히 실패한다.</b>
    /// </remarks>
    private const int ChunkChars = 6000;

    /// <summary>
    /// 이보다 긴 줄은 <b>혼자서도 한 덩이를 넘긴다.</b> 옮기지 않고 건너뛴다 —
    /// 잘라 보내면 번역문이 문장 가운데서 끊겨 원문보다 읽기 나쁘다.
    /// </summary>
    private const int MaxLineChars = 6000;

    /// <summary>편집기 DOM 요소의 id. 한 화면에 둘이 설 수 있어 겹치지 않게 짓는다.</summary>
    private readonly string _id = $"pm-xlat-{Guid.NewGuid():N}";

    /// <summary>Monaco 스크립트를 다 받았나. <b>받기 전에는 편집기를 그리지 않는다.</b></summary>
    private bool _loaded;

    private IJSObjectReference? _loader;

    private StandaloneDiffEditor? _diff;

    /// <summary>왼쪽(원문) · 오른쪽(번역) 모델. 둘 다 만들어야 비교가 선다.</summary>
    private TextModel? _original;
    private TextModel? _modified;

    /// <summary>모델을 편집기에 걸었나.</summary>
    private bool _modelsSet;

    /// <summary>
    /// 옮긴 것. <b>화면 밖에 있다</b> — 로그 탭의 알맹이는 다른 탭으로
    /// 건너가는 순간 버려지는데(<c>DxTabs</c>) 번역은 살아 있어야 한다
    /// (<see cref="AiLogXlatStore"/>).
    /// </summary>
    private AiLogXlatState _state = new();

    /// <summary>편집기에 마지막으로 밀어 넣은 글. 같은 글을 두 번 밀지 않는다.</summary>
    private string _pushedOriginal = string.Empty;
    private string _pushedModified = string.Empty;

    private bool _wrap;
    private bool _busy;

    /// <summary>하던 번역. 화면을 떠나거나 「중지」를 누르면 끊는다.</summary>
    private CancellationTokenSource? _work;

    private double _fontPx = 14;

    /// <summary>아직 안 옮긴 줄 수.</summary>
    private int Remaining => _state.Remaining;

    /// <summary>
    /// 번역 단추의 글자. <b>세 가지가 다르다</b> — 처음 · 멈춘 자리에서 이어
    /// 하기 · 다 옮긴 뒤.
    /// </summary>
    /// <remarks>
    /// 다 옮긴 뒤에 「이어서 번역 (0줄)」을 회색으로 두면 사람은 그것이 무엇을
    /// 세는 숫자인지 읽지 못한다. 그 자리에는 <b>그때 할 수 있는 일</b>을
    /// 적는다 — 번역이 마음에 안 들 때 다시 시키는 것.
    /// </remarks>
    private string RunText => (_state.Done, Remaining) switch
    {
        (0, _) => "번역",
        (_, 0) => "다시 번역",
        _ => $"이어서 번역 ({Remaining}줄)",
    };

    /// <summary>머리띠 오른쪽 한 줄 — 어디까지 왔나 · 누가 답했나 · 무엇이 걸렸나.</summary>
    private string StatusText
    {
        get
        {
            if (_state.Error is { Length: > 0 } why)
            {
                return why;
            }

            if (_state.Source.Count == 0)
            {
                return "옮길 줄이 없습니다.";
            }

            var head = _busy
                ? $"옮기는 중… {_state.Done}/{_state.Source.Count}줄"
                : $"{_state.Done}/{_state.Source.Count}줄";

            return _state.Who is { Length: > 0 } who ? $"{head} · {who}" : head;
        }
    }

    protected override void OnInitialized()
    {
        // 서랍에서 테마 크기를 바꾸면 다시 재서 밀어 넣는다. 옵션은 편집기를
        // 만들 때 한 번만 평가되므로(`BuildOptions`) 여기서 안 들으면
        // **이미 떠 있는 편집기만** 옛 크기로 남는다.
        Size.Changed += OnThemeSizeChanged;
    }

    protected override void OnParametersSet()
    {
        _state = Store.Use(StateKey);

        SyncSource();
    }

    /// <summary>
    /// 부모가 준 줄을 들여온다.
    /// </summary>
    /// <remarks>
    /// <para>
    /// 로그는 <b>실행 중에 바닥으로 자란다.</b> 늘어난 줄만 뒤에 붙이고 이미
    /// 옮긴 것은 그대로 둔다 — 자랄 때마다 처음부터 다시 옮기면 끝나지 않는다.
    /// </para>
    /// <para>
    /// <b>앞부분이 달라졌으면 통째로 버린다.</b> 꼬리만 그리는 상한에 걸려
    /// 앞이 잘려 나간 경우인데, 그대로 이어 붙이면 번역이 <b>엉뚱한 줄에
    /// 붙는다</b> — 틀린 티가 안 나는 종류라 특히 나쁘다.
    /// </para>
    /// </remarks>
    private void SyncSource()
    {
        var incoming = Lines as IList<string> ?? [.. Lines];

        if (incoming.Count == _state.Source.Count && SharesHead(incoming, _state.Source.Count))
        {
            return;
        }

        if (incoming.Count > _state.Source.Count && SharesHead(incoming, _state.Source.Count))
        {
            // 자란 만큼만 뒤에 붙인다. **자리는 그대로**라 옮기던 덩이가
            // 돌아와 제자리에 꽂힌다.
            for (var i = _state.Source.Count; i < incoming.Count; i++)
            {
                var line = incoming[i] ?? string.Empty;

                _state.Source.Add(line);
                _state.Out.Add(line);
            }

            return;
        }

        // 앞부분이 달라졌다 — 꼬리만 그리는 상한에 걸려 앞이 잘려 나갔다.
        // 옮겨 둔 것을 버린다. 이어 붙이면 번역이 엉뚱한 줄에 붙는다.
        _state.Reset(incoming.Select(line => line ?? string.Empty));
    }

    /// <summary>들어온 줄의 앞 <paramref name="count"/> 줄이 들고 있던 것과 같은가.</summary>
    private bool SharesHead(IList<string> incoming, int count)
    {
        if (count > incoming.Count)
        {
            return false;
        }

        for (var i = 0; i < count; i++)
        {
            if (!string.Equals(incoming[i] ?? string.Empty, _state.Source[i], StringComparison.Ordinal))
            {
                return false;
            }
        }

        return true;
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (firstRender && !_loaded)
        {
            await LoadMonacoAsync();
            return;
        }

        if (_diff is not { } diff)
        {
            return;
        }

        if (!_modelsSet)
        {
            await BuildModelsAsync(diff);
            return;
        }

        await PushValuesAsync();

        // 탭을 떠나면 하던 번역을 거둔다. 돌아와서 「이어서 번역」을 누르면
        // 멈춘 자리부터 간다.
        if (!Active && _busy)
        {
            Stop();
        }
    }

    /// <summary>
    /// Monaco 를 받아 온다. <b>받기 전에 편집기를 그리면 회로가 끊긴다</b> —
    /// 그 화면만 깨지는 것이 아니라 그때부터 아무 단추도 안 눌린다
    /// (<c>CodeEditor</c> 와 같은 까닭·같은 길).
    /// </summary>
    private async Task LoadMonacoAsync()
    {
        IJSObjectReference loader;

        try
        {
            loader = await Js.InvokeAsync<IJSObjectReference>(
                "import", "./_content/JSini.Web.ProjMng/js/monaco-loader.js");
        }
        catch (Exception ex) when (ex is JSException or JSDisconnectedException
            or ObjectDisposedException or TaskCanceledException)
        {
            return;
        }

        _loader = loader;

        try
        {
            await loader.InvokeVoidAsync("ensure");

            // **편집기를 그리기 전에 잰다.** 옵션은 만들 때 한 번만 평가되므로
            // 여기서 놓치면 첫 그림이 기본 크기로 뜬다.
            _fontPx = await loader.InvokeAsync<double>("baseFontPx");
        }
        catch (Exception ex) when (ex is JSException or JSDisconnectedException
            or ObjectDisposedException or TaskCanceledException)
        {
            return;
        }

        if (_loader is null)
        {
            // 받아 오는 사이에 화면이 치워졌다.
            return;
        }

        _loaded = true;
        StateHasChanged();
    }

    /// <summary>
    /// 좌우 모델을 만들어 편집기에 건다.
    /// </summary>
    /// <remarks>
    /// <b>글자를 편집기가 아니라 모델에 넣는다.</b> 비교 편집기는 모델 둘을
    /// 짝지어 받고, 짝을 맞추는 일(같은 줄을 같은 높이에 세우기)도 모델을
    /// 보고 한다 — 편집기에 글을 직접 밀면 그 짝이 다시 계산되지 않는다.
    /// </remarks>
    private async Task BuildModelsAsync(StandaloneDiffEditor diff)
    {
        try
        {
            // 주소가 모델의 열쇠다. 한 화면에 둘이 설 수 있어 겹치지 않게 짓는다.
            _original = await Global.CreateModel(
                Js, string.Join('\n', _state.Source), "plaintext",
                $"inmemory://jsini/{_id}/original");

            _modified = await Global.CreateModel(
                Js, string.Join('\n', _state.Out), "plaintext",
                $"inmemory://jsini/{_id}/modified");

            _pushedOriginal = string.Join('\n', _state.Source);
            _pushedModified = string.Join('\n', _state.Out);

            await diff.SetModel(new DiffEditorModel { Original = _original, Modified = _modified });

            // 테마는 문서의 data-theme 를 따라간다(theme.js 가 정한다). 흰 바탕에
            // 밝은 편집기가 얹히면 그 부분만 튀어 보인다.
            var dark = await Js.InvokeAsync<bool>("jsiniTheme.isDark");
            await Global.SetTheme(Js, dark ? "vs-dark" : "vs");

            _modelsSet = true;

            // **값을 넣은 뒤에는 직접 레이아웃을 부른다.** AutomaticLayout 을
            // 켜 두어도 Blazor Server 에서는 제때 걸리지 않아, 편집기는 만들어지고
            // 값도 들어갔는데 줄이 하나도 안 그려지는 일이 난다.
            await diff.OriginalEditor.Layout();
            await diff.ModifiedEditor.Layout();
        }
        catch (Exception ex) when (ex is JSException or JSDisconnectedException
            or ObjectDisposedException or TaskCanceledException)
        {
            return;
        }

        // **여기서 기다리지 않는다.** 번역은 로컬 LLM 왕복이 여러 번이라 몇
        // 분이 걸릴 수 있고, 그것을 그리기 뒷정리 안에서 붙들고 있으면 화면이
        // 그 사이의 StateHasChanged 를 늦게 받는다.
        if (AutoStart && !_busy && Remaining > 0)
        {
            _ = InvokeAsync(TranslateAsync);
        }
    }

    /// <summary>
    /// 바뀐 쪽만 모델에 민다. <b>같은 글을 두 번 밀지 않는다</b> — 미는 일마다
    /// 회로가 글 전체를 실어 나른다.
    /// </summary>
    private async Task PushValuesAsync()
    {
        var left = string.Join('\n', _state.Source);
        var right = string.Join('\n', _state.Out);

        if (left == _pushedOriginal && right == _pushedModified)
        {
            return;
        }

        try
        {
            // **읽던 자리를 지킨다.** `setValue` 는 내용을 통째로 갈아 끼우므로
            // 그냥 밀면 한 덩이가 올 때마다 화면이 맨 위로 튄다 — 로그를
            // 내려 읽던 사람에게는 그것이 고장으로 보인다.
            var top = _diff is { } d ? await d.ModifiedEditor.GetScrollTop() : 0;

            if (left != _pushedOriginal && _original is { } original)
            {
                await original.SetValue(left);
                _pushedOriginal = left;
            }

            if (right != _pushedModified && _modified is { } modified)
            {
                await modified.SetValue(right);
                _pushedModified = right;
            }

            if (top > 0 && _diff is { } diff)
            {
                await diff.ModifiedEditor.SetScrollTop((int)top);
            }
        }
        catch (Exception ex) when (ex is JSException or JSDisconnectedException
            or ObjectDisposedException or TaskCanceledException)
        {
            // 떠났거나 회로가 끊겼다. 밀어 넣을 곳이 없다.
        }
    }

    /// <summary>
    /// 남은 줄을 <b>스무 줄씩 끊어</b> 옮긴다. 한 덩이가 올 때마다 그 자리를
    /// 갈아 끼우므로 오른쪽 칸이 위에서부터 채워진다.
    /// </summary>
    private async Task TranslateAsync()
    {
        if (_busy || _state.Source.Count == 0)
        {
            return;
        }

        if (Remaining == 0)
        {
            // 다 옮긴 뒤에 다시 눌렀다. 처음부터 간다.
            _state.Rewind();
        }

        StopWork();

        var cts = _work = new CancellationTokenSource();

        _busy = true;
        _state.Error = null;
        StateHasChanged();

        try
        {
            while (!cts.IsCancellationRequested && _state.Done < _state.Source.Count)
            {
                var (start, count) = NextChunk();

                // 혼자서도 한 덩이를 넘기는 줄은 건너뛴다. 원문이 그대로 남는다.
                if (count == 1 && _state.Source[start].Length > MaxLineChars)
                {
                    _state.Done = start + 1;
                    continue;
                }

                var gen = _state.Generation;
                var slice = _state.Source.GetRange(start, count);

                var answer = await Xlat.TranslateAsync(slice, _state.Lang, cts.Token);

                if (cts.IsCancellationRequested)
                {
                    return;
                }

                if (_state.Generation != gen)
                {
                    // 기다리는 사이에 로그가 통째로 갈렸다(앞이 잘려 나갔다).
                    // 이 답은 **이제 없는 줄의 것**이다. 버리고 새 자료의
                    // 처음부터 다시 센다.
                    continue;
                }

                if (answer?.Lines is { Count: > 0 } got)
                {
                    for (var i = 0; i < count && i < got.Count; i++)
                    {
                        _state.Out[start + i] = got[i] ?? _state.Source[start + i];
                    }

                    _state.Who = Who(answer);
                }

                _state.Done = start + count;

                await PushValuesAsync();
                StateHasChanged();
            }
        }
        catch (OperationCanceledException)
        {
            // 「중지」를 눌렀거나 화면을 떠났다. 옮긴 데까지는 그대로 남는다.
        }
        catch (ApiException ex)
        {
            // **`Done` 을 올리지 않는다.** 실패한 덩이는 손대지 않은 것이므로
            // 「이어서 번역」이 바로 그 자리부터 다시 간다 — 올려 두면 그
            // 줄들만 영영 원문으로 남고, 그것이 오류로 드러나지도 않는다.
            _state.Error = ex.Message is { Length: > 0 } message
                ? $"번역이 멈췄습니다 — {message}"
                : "번역이 멈췄습니다. 잠시 뒤에 다시 눌러 보십시오.";

            Toasts.Show(_state.Error, NoticeTone.Warning);
        }
        catch (Exception ex) when (ex is JSException or JSDisconnectedException
            or ObjectDisposedException)
        {
            // 회로가 끊겼다. 알릴 곳이 없다.
        }
        finally
        {
            _busy = false;

            if (ReferenceEquals(_work, cts))
            {
                _work = null;
            }

            cts.Dispose();

            try
            {
                StateHasChanged();
            }
            catch (ObjectDisposedException)
            {
            }
        }
    }

    /// <summary>
    /// 다음에 보낼 덩이. <b>줄 수와 글자 수를 함께 본다</b> — 줄 수만 세면
    /// 긴 줄이 섞였을 때 서버의 글자 상한에 걸려 그 덩이만 조용히 실패한다.
    /// </summary>
    private (int Start, int Count) NextChunk()
    {
        var start = _state.Done;
        var chars = 0;
        var count = 0;

        while (start + count < _state.Source.Count && count < AiLogTranslator.ChunkLines)
        {
            var len = _state.Source[start + count].Length;

            if (count > 0 && chars + len > ChunkChars)
            {
                break;
            }

            chars += len;
            count++;
        }

        return (start, Math.Max(count, 1));
    }

    /// <summary>머리띠에 적을 AI 이름. 공급자가 안 알려 주면 <c>null</c> 이다.</summary>
    private static string? Who(AiTranslatedLinesDto answer)
    {
        var name = answer.ProviderName ?? answer.Provider;

        if (string.IsNullOrWhiteSpace(name))
        {
            return null;
        }

        return string.IsNullOrWhiteSpace(answer.Model) ? name : $"{name} · {answer.Model}";
    }

    /// <summary>
    /// 옮겨 갈 말을 바꾼다. <b>옮겨 둔 것을 버린다</b> — 한 판에 두 말이
    /// 섞여 있으면 어느 줄이 무슨 말인지 알 길이 없다.
    /// </summary>
    private async Task PickLanguageAsync(string code)
    {
        if (_state.Lang == code || _busy)
        {
            return;
        }

        _state.Lang = code;
        _state.Rewind();

        await PushValuesAsync();

        if (AutoStart && Remaining > 0)
        {
            await TranslateAsync();
        }
    }

    private async Task ToggleWrapAsync()
    {
        _wrap = !_wrap;

        if (_diff is not { } diff)
        {
            return;
        }

        try
        {
            await diff.UpdateOptions(new DiffEditorOptions
            {
                WordWrap = _wrap ? "on" : "off",
            });
        }
        catch (Exception ex) when (ex is JSException or JSDisconnectedException
            or ObjectDisposedException)
        {
        }
    }

    /// <summary>하던 번역을 거둔다. <b>옮긴 데까지는 그대로 남는다.</b></summary>
    private void Stop()
    {
        StopWork();
        _busy = false;
    }

    private void StopWork()
    {
        var cts = _work;
        _work = null;

        if (cts is null)
        {
            return;
        }

        try
        {
            cts.Cancel();
        }
        catch (ObjectDisposedException)
        {
        }
    }

    /// <summary>
    /// 편집기 옵션.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>고치는 자리가 아니라 견주는 자리다.</b> 그래서 양쪽 다 읽기 전용이고
    /// (<c>ReadOnly</c> · <c>OriginalEditable</c>), 바꿈 표시와 되돌리기 단추를
    /// 끈다 — 이 둘이 가리키는 「바뀜」은 번역이지 누가 고친 것이 아니다.
    /// </para>
    /// <para>
    /// <b>빈칸을 무시하지 않는다</b>(<c>IgnoreTrimWhitespace = false</c>).
    /// 로그는 들여쓰기가 뜻을 들고 있어서, 무시하게 두면 들여쓰기만 다른 줄이
    /// 같은 줄로 묶여 짝이 어긋난다.
    /// </para>
    /// <para>
    /// 줄바꿈은 <b>꺼서 시작한다.</b> 켜면 한 줄이 여러 줄 높이가 되고, 좌우의
    /// 접히는 횟수가 달라 짝이 눈으로 흐트러진다 — 이 판이 하려는 일이 그
    /// 짝이라 기본값은 끄는 쪽이다. 긴 줄을 통째로 읽어야 하면 단추로 켠다.
    /// </para>
    /// </remarks>
    private StandaloneDiffEditorConstructionOptions BuildOptions(StandaloneDiffEditor editor) => new()
    {
        AutomaticLayout = true,
        ReadOnly = true,
        OriginalEditable = false,
        RenderSideBySide = true,
        EnableSplitViewResizing = true,
        RenderIndicators = false,
        RenderMarginRevertIcon = false,
        RenderOverviewRuler = false,
        IgnoreTrimWhitespace = false,
        FontSize = (int)Math.Round(_fontPx),
        LineNumbers = "on",
        Minimap = new EditorMinimapOptions { Enabled = false },
        ScrollBeyondLastLine = false,
        WordWrap = _wrap ? "on" : "off",
        OriginalAriaLabel = "원문 로그",
        ModifiedAriaLabel = "번역한 로그",
    };

    /// <summary>
    /// <b><c>InvokeAsync</c> 로 넘긴다.</b> 이 알림은 테마 서랍 쪽에서 오고,
    /// 한 번 <c>await</c> 하는 순간 회로의 동기화 문맥 밖으로 나간다.
    /// </summary>
    private void OnThemeSizeChanged() => _ = InvokeAsync(ApplyFontSizeAsync);

    /// <summary>
    /// 본문 글자 크기를 다시 재서 편집기에 민다. <b>재는 것을 한 박자 미룬다</b> —
    /// 알림은 화면이 새 크기로 다시 그려지기 <b>전에</b> 온다.
    /// </summary>
    private async Task ApplyFontSizeAsync()
    {
        if (_loader is not { } loader || _diff is not { } diff)
        {
            return;
        }

        try
        {
            await Task.Delay(50);

            var px = await loader.InvokeAsync<double>("baseFontPx");

            if (Math.Abs(px - _fontPx) < 0.5)
            {
                return;
            }

            _fontPx = px;

            await diff.UpdateOptions(new DiffEditorOptions
            {
                FontSize = (int)Math.Round(px),
            });
        }
        catch (Exception ex) when (ex is JSException or JSDisconnectedException
            or ObjectDisposedException or TaskCanceledException)
        {
        }
    }

    /// <summary>
    /// 치울 때의 <b>차례가 중요하다</b> — 편집기를 먼저 놓고 모델을 놓는다.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Monaco 의 모델은 편집기가 아니라 <c>monaco.editor</c> 전역에 매달려
    /// 있어서, 안 놓으면 이 판을 열 때마다 브라우저에 로그 한 벌이 통째로
    /// 쌓인다. 그런데 <b>비교 편집기가 아직 그 모델을 쥔 채로 놓으면</b>
    /// 브라우저가 이렇게 던진다 —
    /// </para>
    /// <code>TextModel got disposed before DiffEditorWidget model got reset</code>
    /// <para>
    /// 탭을 건너갈 때마다 났다(<c>DxTabs</c> 가 안 보이는 탭의 알맹이를
    /// 버린다). 부품 둘이 치워지는 차례는 우리가 정할 수 없지만
    /// <c>DisposeEditor</c> 한 줄은 정할 수 있다 —
    /// <b>편집기를 먼저 치운다.</b>
    /// </para>
    /// </remarks>
    public async ValueTask DisposeAsync()
    {
        Size.Changed -= OnThemeSizeChanged;

        StopWork();

        if (_diff is { } diff)
        {
            try
            {
                await diff.DisposeEditor();
            }
            catch (Exception ex) when (ex is JSException or JSDisconnectedException
                or ObjectDisposedException or TaskCanceledException)
            {
            }

            _diff = null;
        }

        foreach (var model in new[] { _original, _modified })
        {
            if (model is null)
            {
                continue;
            }

            try
            {
                await model.DisposeModel();
            }
            catch (Exception ex) when (ex is JSException or JSDisconnectedException
                or ObjectDisposedException or TaskCanceledException)
            {
                // 회로가 이미 끊겼다(사용자가 탭을 닫았다). 놓을 것이 없다.
            }
        }

        _original = null;
        _modified = null;

        if (_loader is not null)
        {
            try
            {
                await _loader.DisposeAsync();
            }
            catch (Exception ex) when (ex is JSDisconnectedException or JSException)
            {
            }

            _loader = null;
        }
    }
}
