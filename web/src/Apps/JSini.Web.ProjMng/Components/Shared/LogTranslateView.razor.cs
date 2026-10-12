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
    /// 볼 로그 줄. <b>이 판이 로그 탭의 전부다</b> — 원문을 보여 주는 자리가
    /// 여기 왼쪽 칸 하나뿐이라, 받은 줄을 그대로 다 그린다.
    /// </summary>
    [Parameter] public IReadOnlyList<string> Lines { get; set; } = [];

    /// <summary>
    /// 옮긴 것을 어느 이름으로 들고 있을지. <b>건마다 달라야 한다</b> —
    /// 같으면 다른 지시를 열었을 때 앞 건의 번역이 그대로 보인다.
    /// </summary>
    [Parameter, EditorRequired] public string StateKey { get; set; } = string.Empty;

    /// <summary>
    /// 지금 보이고 있나. <b>거짓이 되면 돌고 있는 줄을 거둔다</b> — 탭을 떠난
    /// 사람 때문에 로컬 LLM 이 계속 돌 까닭이 없다.
    /// </summary>
    [Parameter] public bool Active { get; set; } = true;

    /// <summary>
    /// 옮겨 갈 말. <b>한국어로 고정이다</b> — 왼쪽 칸이 이미 원문이라, 가는
    /// 말을 영어로 돌리면 영어 로그를 영어로 옮긴 판이 되어 볼 것이 없다.
    /// 고를 것이 하나뿐인 고르개는 자리만 먹으므로 두지 않는다.
    /// </summary>
    private const string TargetLang = "ko";

    /// <summary>
    /// 이보다 긴 줄은 <b>한 번에 보낼 수 있는 양을 넘긴다.</b> 옮기지 않고
    /// 사정을 알린다 — 잘라 보내면 번역문이 문장 가운데서 끊겨 원문보다
    /// 읽기 나쁘다. 서버의 상한(<c>LLMService.MaxTranslateInput</c> = 12000)
    /// 보다 넉넉히 낮게 둔다.
    /// </summary>
    private const int MaxLineChars = 6000;

    /// <summary>편집기 DOM 요소의 id. 한 화면에 둘이 설 수 있어 겹치지 않게 짓는다.</summary>
    private readonly string _id = $"pm-xlat-{Guid.NewGuid():N}";

    /// <summary>Monaco 스크립트를 다 받았나. <b>받기 전에는 편집기를 그리지 않는다.</b></summary>
    private bool _loaded;

    private IJSObjectReference? _loader;

    private StandaloneDiffEditor? _diff;

    /// <summary>
    /// 휴대폰에서 혼자 서는 편집기. <b>번역 칸만</b> 보여 준다.
    /// </summary>
    /// <remarks>
    /// 좁은 화면에서 좌우로 가르면 한 칸이 스무 글자라 <b>양쪽 다 못 읽는다.</b>
    /// 비교 편집기의 칸 하나만 숨기는 길은 없다 — Monaco 가 칸 폭을 제 손으로
    /// 재고 그리므로 CSS 로 왼쪽을 가려도 오른쪽은 반쪽 폭 그대로 남는다.
    /// 그래서 <b>같은 모델을 보통 편집기에 건다</b>(<see cref="_modified"/>).
    /// </remarks>
    private StandaloneCodeEditor? _solo;

    /// <summary>
    /// 휴대폰인가. 참이면 비교 편집기 대신 <see cref="_solo"/> 가 선다.
    /// </summary>
    private bool _phone;

    /// <summary>왼쪽(원문) · 오른쪽(번역) 모델. 둘 다 만들어야 비교가 선다.</summary>
    private TextModel? _original;
    private TextModel? _modified;

    /// <summary>
    /// 모델을 만들었나. <b>편집기와 따로 센다</b> — 화면 폭이 바뀌면 편집기는
    /// 통째로 갈리는데 옮겨 둔 줄은 그대로 살아 있어야 한다.
    /// </summary>
    private bool _modelsBuilt;

    /// <summary>
    /// 지금 모델을 걸어 둔 편집기. <b>참조로 가른다</b> — 폭이 바뀌어 편집기가
    /// 갈리면 이 값이 달라지므로, 새로 선 쪽에 다시 건다.
    /// </summary>
    private Editor? _attachedTo;

    /// <summary>
    /// 번역 칸을 쥐고 있는 편집기. 휴대폰에서는 혼자 선 편집기가 그것이다.
    /// <b>구르는 자리를 재고 옮기는 일이 전부 이쪽을 본다.</b>
    /// </summary>
    private StandaloneCodeEditor? ModifiedView => _phone ? _solo : _diff?.ModifiedEditor;

    /// <summary>
    /// 옮긴 것. <b>화면 밖에 있다</b> — 로그 탭의 알맹이는 다른 탭으로
    /// 건너가는 순간 버려지는데(<c>DxTabs</c>) 번역은 살아 있어야 한다
    /// (<see cref="AiLogXlatStore"/>).
    /// </summary>
    private AiLogXlatState _state = new();

    private bool _wrap;

    /// <summary>
    /// 줄바꿈 단추를 사람이 직접 눌렀나. <b>누른 뒤에는 폭이 바뀌어도 그
    /// 뜻을 지킨다</b> — 휴대폰으로 넘어갈 때 저절로 켜지는 것은 아직 아무
    /// 말도 하지 않은 사람에게만 주는 기본값이다.
    /// </summary>
    private bool _wrapTouched;

    private double _fontPx = 14;

    /// <summary>
    /// 커서가 선 줄(1부터). <b>0 이면 아직 아무 데도 안 눌렀다.</b>
    /// </summary>
    /// <remarks>
    /// 이 하나로 <b>좌우 두 칸에 같은 불을 켠다</b>. Monaco 가 들고 있는
    /// 「지금 줄」 표시(<c>renderLineHighlight</c>)를 쓰지 않는 까닭이 그것이다 —
    /// 그것은 <b>커서가 있는 칸에만</b> 켜지는데, 여기서 보려는 것은 왼쪽
    /// 108번째 줄과 <b>짝인 오른쪽 108번째 줄</b>이다.
    /// </remarks>
    private int _focusLine;

    /// <summary>
    /// 지금 옮기고 있는 줄(1부터). 0 이면 없다. 그 줄에만 따로 불을 켜 둔다 —
    /// 로컬 LLM 왕복이 몇 초라, 표시가 없으면 <b>눌러도 아무 일이 없는 것</b>
    /// 으로 보인다.
    /// </summary>
    private int _redoLine;

    /// <summary>줄 하나를 옮기는 일. 다음 줄을 누르면 앞엣것을 거둔다.</summary>
    private CancellationTokenSource? _lineWork;

    /// <summary>켜 둔 불의 표. 다음에 지울 것을 Monaco 가 이 이름으로 찾는다.</summary>
    private List<string> _leftMarks = [];
    private List<string> _rightMarks = [];

    /// <summary>
    /// 글을 미는 길목의 빗장. <b>미는 쪽이 둘이다</b> — 로그가 자라 뒤에
    /// 붙이는 일과, 누른 줄 하나를 꽂는 일이 같은 때에 돈다. 빗장이 없으면
    /// 둘이 <see cref="_pushedOut"/> 를 엇갈려 고쳐, <b>민 것과 밀었다고
    /// 적어 둔 것이 달라진다</b> — 그 뒤로는 바뀐 줄을 못 찾는다.
    /// </summary>
    private readonly SemaphoreSlim _push = new(1, 1);

    /// <summary>
    /// 불을 켜고 끄는 길목의 빗장. <b>부르는 자리가 넷이다</b> — 커서가
    /// 움직였을 때 · 줄을 눌렀을 때 · 한 줄이 돌아왔을 때 · 글을 통째로
    /// 갈아 끼운 뒤. 엇갈리면 <see cref="_leftMarks"/> 에 <b>이미 없는
    /// 이름</b>이 남아, 꺼야 할 불이 켜진 채로 굳는다.
    /// </summary>
    private readonly SemaphoreSlim _marks = new(1, 1);

    /// <summary>
    /// 편집기에 <b>실제로 들어 있는</b> 줄들. 바꿀 자리를 이것과 견주어 찾는다.
    /// </summary>
    /// <remarks>
    /// 글 전체를 이어 붙인 문자열로 들고 있었다. 그때는 미는 길이 하나뿐이라
    /// 통째로 갈아 끼워도 됐는데, <b>줄 하나를 누를 때마다 수백 줄을 회로로
    /// 실어 나를 수는 없다.</b> 줄 단위로 들고 있으면 바뀐 자리가 어디부터
    /// 어디까지인지 바로 나오고, 그 토막만 <c>ApplyEdits</c> 로 꽂는다 —
    /// 켜 둔 불도 그대로 남는다(<c>setValue</c> 는 지운다).
    /// </remarks>
    private readonly List<string> _pushedSrc = [];
    private readonly List<string> _pushedOut = [];

    /// <summary>
    /// 복사 아이콘이 집어 갈 글. <b>편집기가 아니라 들고 있는 줄에서 만든다</b> —
    /// 보이는 것과 같은 것이고(민 것이 그것이다), 회로 왕복이 없다.
    /// </summary>
    private string SourceText => string.Join('\n', _state.Source);

    private string OutText => string.Join('\n', _state.Out);

    /// <summary>
    /// 머리띠 오른쪽 한 줄 — 몇 줄 가운데 몇 줄을 옮겼나 · 누가 답했나 ·
    /// 무엇이 걸렸나.
    /// </summary>
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

            if (_redoLine > 0)
            {
                // **셈보다 이것이 먼저다.** 줄을 누른 사람이 보려는 것은
                // 전체 셈이 아니라 「내가 누른 그 줄이 지금 도는가」다.
                return $"{_redoLine}번째 줄을 옮기는 중…";
            }

            if (_state.DoneLines.Count == 0)
            {
                // **아직 한 줄도 안 옮겼다.** 여기에 「0/300줄」을 적으면
                // 그것이 「도는 중인데 아직 안 됐다」로 읽힌다 — 이 판은
                // 누르기 전에는 아무것도 하지 않으므로 그렇게 적는다.
                return $"{_state.Source.Count}줄 · 줄을 누르면 그 줄을 옮깁니다";
            }

            var head = $"{_state.Source.Count}줄 가운데 {_state.DoneLines.Count}줄 옮김";

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
    /// 옮긴 것은 그대로 둔다 — 자리가 안 밀리므로 옮겨 둔 줄이 제자리에 남는다.
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
            // 자란 만큼만 뒤에 붙인다. **자리는 그대로**라 옮겨 둔 줄이
            // 제자리에 남는다.
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

    /// <summary>
    /// 화면 폭이 휴대폰 쪽으로 넘어갔거나 되돌아왔다. <b>편집기가 통째로 갈린다.</b>
    /// </summary>
    /// <remarks>
    /// <para>
    /// 옮겨 둔 줄은 그대로 남는다 — 모델(<see cref="_modified"/>)은 편집기
    /// 바깥에 있고, 새로 선 편집기에 그대로 다시 건다.
    /// </para>
    /// <para>
    /// <b>떠나는 편집기를 우리 손으로 치운다.</b> BlazorMonaco 의 부품이
    /// 화면에서 사라져도 <b>브라우저의 Monaco 는 그대로 남는다</b> — 재어
    /// 확인했다(폭을 한 번 바꿀 때마다 편집기가 하나씩 쌓였다). 손잡이도
    /// 함께 놓는다. Blazor 는 사라진 부품의 참조(<c>@ref</c>)를 비워 주지
    /// 않아, 그대로 두면 이미 치운 편집기에 글을 밀게 된다.
    /// </para>
    /// </remarks>
    private async Task OnPhoneChangedAsync(bool phone)
    {
        if (_phone == phone)
        {
            return;
        }

        _phone = phone;
        _attachedTo = null;

        // **휴대폰에서는 줄바꿈을 켜 둔다.** 끄고 시작하는 까닭은 좌우 짝이
        // 눈으로 흐트러지지 않게 하려는 것인데(`BuildOptions` 머리말), 칸이
        // 하나뿐인 자리에는 맞출 짝이 없다. 거기서 끈 채로 두면 로그 한 줄이
        // 화면 폭의 몇 배라 **옆으로 밀어 가며 읽어야 한다.**
        //
        // 사람이 단추를 한 번이라도 눌렀으면 그 뜻을 지킨다.
        if (!_wrapTouched)
        {
            _wrap = phone;
        }

        Editor? leaving;

        if (phone)
        {
            leaving = _diff;
            _diff = null;
        }
        else
        {
            leaving = _solo;
            _solo = null;
        }

        if (leaving is not null)
        {
            try
            {
                // 모델은 살아남는다 — 편집기가 들고 있는 것이 아니라
                // `monaco.editor` 전역에 매달려 있다(`DisposeAsync` 머리말).
                await leaving.DisposeEditor();
            }
            catch (Exception ex) when (ex is JSException or JSDisconnectedException
                or ObjectDisposedException or TaskCanceledException)
            {
            }
        }

        StateHasChanged();
    }

    /// <summary>
    /// 편집기가 브라우저에 다 섰다. <b>여기서 모델을 건다.</b>
    /// </summary>
    /// <remarks>
    /// 그리기 뒷정리(<see cref="OnAfterRenderAsync"/>)만으로는 모자랐다 —
    /// Blazor 는 자식의 뒷정리를 <b>기다려 주지 않아서</b>, 폭이 바뀌어 새
    /// 편집기가 설 때 부모가 먼저 <c>SetModel</c> 을 부른다. 그 부름은 아직
    /// 없는 편집기를 찾다가 조용히 실패하고, <b>다시 부를 일이 없어 그대로
    /// 빈 편집기가 남는다</b>(재어 확인했다 — 휴대폰으로 바꾸면 줄이 하나도
    /// 안 보였다).
    /// </remarks>
    private async Task OnEditorReadyAsync()
    {
        if (!_modelsBuilt)
        {
            // 모델이 아직 없다. 만든 뒤에 그리기 뒷정리가 건다.
            return;
        }

        if (await AttachModelsAsync())
        {
            await PushValuesAsync();
        }
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (firstRender && !_loaded)
        {
            await LoadMonacoAsync();
            return;
        }

        if (!_loaded)
        {
            return;
        }

        if (!_modelsBuilt)
        {
            await BuildModelsAsync();
            return;
        }

        // 지금 서 있는 편집기에 모델을 건다. 폭이 바뀌어 편집기가 갈렸으면
        // 여기서 새 쪽에 다시 걸린다.
        if (!await AttachModelsAsync())
        {
            return;
        }

        await PushValuesAsync();

        // 탭을 떠나면 돌고 있는 줄을 거둔다. 돌아와서 다시 누르면 그때 간다.
        if (!Active && _redoLine > 0)
        {
            CancelLineWork();
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
    /// 좌우 모델을 만든다. <b>편집기와 따로 만든다</b> — 화면 폭이 바뀌면
    /// 편집기는 통째로 갈리는데 옮겨 둔 줄은 살아 있어야 한다.
    /// </summary>
    /// <remarks>
    /// <b>글자를 편집기가 아니라 모델에 넣는다.</b> 비교 편집기는 모델 둘을
    /// 짝지어 받고, 짝을 맞추는 일(같은 줄을 같은 높이에 세우기)도 모델을
    /// 보고 한다 — 편집기에 글을 직접 밀면 그 짝이 다시 계산되지 않는다.
    /// </remarks>
    private async Task BuildModelsAsync()
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
        }
        catch (Exception ex) when (ex is JSException or JSDisconnectedException
            or ObjectDisposedException or TaskCanceledException)
        {
            return;
        }

        _pushedSrc.Clear();
        _pushedSrc.AddRange(_state.Source);

        _pushedOut.Clear();
        _pushedOut.AddRange(_state.Out);

        _modelsBuilt = true;

        // 이번 그리기에는 아직 거는 일이 남았다. 다음 뒷정리에서 이어 간다.
        StateHasChanged();
    }

    /// <summary>
    /// 지금 서 있는 편집기에 모델을 건다. <b>돌려주는 것은 「걸려 있나」</b>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// 거는 자리가 둘이다 — 넓은 화면의 비교 편집기와 휴대폰의 외짝 편집기.
    /// 이미 그 편집기에 걸려 있으면 아무것도 하지 않는다(그리기 뒷정리는
    /// 글을 밀 때마다 지나간다).
    /// </para>
    /// <para>
    /// <b>건 뒤에 레이아웃을 직접 부른다.</b> <c>AutomaticLayout</c> 을 켜
    /// 두어도 Blazor Server 에서는 제때 걸리지 않아, 편집기는 만들어지고 값도
    /// 들어갔는데 <b>줄이 하나도 안 그려지는</b> 일이 난다.
    /// </para>
    /// </remarks>
    private async Task<bool> AttachModelsAsync()
    {
        if (_original is not { } left || _modified is not { } right)
        {
            return false;
        }

        Editor? target = _phone ? _solo : _diff;

        if (target is null)
        {
            return false;
        }

        if (ReferenceEquals(_attachedTo, target))
        {
            return true;
        }

        // **걸기 전에 적어 둔다.** 거는 자리가 둘이라(그리기 뒷정리와
        // `OnDidInit`) 걸고 나서 적으면 그 사이에 다른 쪽이 또 들어와 같은
        // 일을 두 번 한다 — 그러면 켜 둔 불이 두 벌로 남는다.
        _attachedTo = target;

        try
        {
            if (target is StandaloneCodeEditor solo)
            {
                // **외짝 편집기는 제 모델을 하나 들고 태어난다**(빈 글).
                // 이것으로 갈아 끼우면 Monaco 가 그 빈 모델을 스스로 놓는다 —
                // 그러면서 「내 것」 표식도 풀리므로, 나중에 이 편집기가
                // 치워져도 **우리 모델은 살아남는다.**
                await solo.SetModel(right);
                await ApplyThemeAsync();
                await solo.Layout();
            }
            else if (target is StandaloneDiffEditor diff)
            {
                await diff.SetModel(new DiffEditorModel { Original = left, Modified = right });
                await ApplyThemeAsync();

                await diff.OriginalEditor.Layout();
                await diff.ModifiedEditor.Layout();
            }

            // **바닥을 보여 준다.** 로그를 펴는 일 대부분이 방금 찍힌 줄을
            // 보려는 것인데, 맨 위에서 서면 거기까지 삼백 줄을 굴려야 한다.
            // 폭이 바뀌어 편집기가 갈렸을 때도 같다 — 새 편집기는 맨 위에서
            // 선다(`js/log-tail.js` 가 글상자에 하던 일이다).
            if (_pushedOut.Count > 1 && ModifiedView is { } view)
            {
                await view.RevealLine(_pushedOut.Count);
            }

            // 편집기가 갈리면 켜 둔 불도 함께 사라진다. 다시 켠다.
            await DrawMarksAsync();
        }
        catch (Exception ex) when (ex is JSException or JSDisconnectedException
            or ObjectDisposedException or TaskCanceledException)
        {
            // 아직 안 섰거나 회로가 끊겼다. **적어 둔 것을 지운다** —
            // 그래야 `OnDidInit` 이 왔을 때 다시 건다.
            if (ReferenceEquals(_attachedTo, target))
            {
                _attachedTo = null;
            }

            return false;
        }

        return true;
    }

    /// <summary>
    /// 편집기 테마를 문서의 <c>data-theme</c> 에 맞춘다(theme.js 가 정한다).
    /// 흰 바탕에 밝은 편집기가 얹히면 그 부분만 튀어 보인다.
    /// </summary>
    private async Task ApplyThemeAsync()
    {
        var dark = await Js.InvokeAsync<bool>("jsiniTheme.isDark");

        await Global.SetTheme(Js, dark ? "vs-dark" : "vs");
    }

    /// <summary>
    /// 바뀐 줄만 모델에 민다. <b>바뀐 토막만 간다</b> — 미는 일마다 글 전체를
    /// 회로로 실어 나르면, 줄 하나를 누를 때마다 수백 줄이 왕복한다.
    /// </summary>
    private async Task PushValuesAsync()
    {
        if (!_modelsBuilt)
        {
            return;
        }

        // **빗장을 건다.** 미는 쪽이 둘이다 — 로그가 자라 뒤에 붙이는 일과,
        // 누른 줄 하나를 꽂는 일. 엇갈리면 민 것과 밀었다고 적어 둔 것이 달라진다.
        await _push.WaitAsync();

        try
        {
            // **줄 수가 달라졌을 때만 잰다.** 재는 데 회로 왕복이 셋 드는데,
            // 이 길은 그리기 뒷정리마다 지나간다 — 바뀐 것이 없는 날에도
            // 재면 단추 한 번 누를 때마다 쓸데없는 왕복이 선다. 따라 내릴
            // 일이 생기는 것도 줄이 늘었을 때뿐이다.
            var grew = _pushedSrc.Count != _state.Source.Count
                || _pushedOut.Count != _state.Out.Count;

            // **밀기 전에 「바닥에 붙어 있나」를 잰다.** 민 뒤에 재면 늘어난
            // 줄 때문에 늘 거짓이라, 돌고 있는 건을 지켜보는 사람이 새 줄을
            // 한 번도 못 본다.
            var tail = grew && await AtBottomAsync();

            var leftReset = await PushSideAsync(_original, _pushedSrc, _state.Source);
            var rightReset = await PushSideAsync(_modified, _pushedOut, _state.Out);

            if (leftReset || rightReset)
            {
                // `setValue` 는 켜 둔 불을 함께 지운다. 다시 켠다.
                await DrawMarksAsync();
            }

            // 줄이 늘었고 사람이 바닥을 보고 있었으면 새 줄까지 따라 내린다.
            // **위를 읽는 중이면 놓아 둔다** — 읽던 자리가 튀는 것이 새 줄을
            // 놓치는 것보다 나쁘다.
            if (tail && (leftReset || rightReset) && _pushedOut.Count > 1
                && ModifiedView is { } view)
            {
                await view.RevealLine(_pushedOut.Count);
            }
        }
        catch (Exception ex) when (ex is JSException or JSDisconnectedException
            or ObjectDisposedException or TaskCanceledException)
        {
            // 떠났거나 회로가 끊겼다. 밀어 넣을 곳이 없다.
        }
        finally
        {
            _push.Release();
        }
    }

    /// <summary>
    /// 지금 바닥에 붙어 있나. <b>한 줄 높이만큼은 봐 준다</b> — 딱 맞아떨어지는
    /// 일이 드물어서, 엄밀히 재면 거의 늘 거짓이 된다.
    /// </summary>
    private async Task<bool> AtBottomAsync()
    {
        if (ModifiedView is not { } view)
        {
            return false;
        }

        try
        {
            var top = await view.GetScrollTop();
            var height = await view.GetScrollHeight();
            var info = await view.GetLayoutInfo();

            // 아직 구를 것이 없으면(줄이 화면보다 적다) 바닥에 붙어 있는 것이다.
            return top + info.Height >= height - (_fontPx * 2);
        }
        catch (Exception ex) when (ex is JSException or JSDisconnectedException
            or ObjectDisposedException or TaskCanceledException)
        {
            return false;
        }
    }

    /// <summary>
    /// 한 칸을 맞춘다. <b>돌려주는 것은 「통째로 갈아 끼웠나」</b> — 그랬다면
    /// 켜 둔 불이 함께 지워졌으므로 부르는 쪽이 다시 켠다.
    /// </summary>
    /// <remarks>
    /// <para>
    /// 줄 수가 같으면 <b>처음과 끝에서 안쪽으로 좁혀</b> 달라진 토막을 찾고 그
    /// 토막만 <c>applyEdits</c> 로 꽂는다. 한 줄만 달라졌으면 가는 것도 한 줄이다.
    /// </para>
    /// <para>
    /// 줄 수가 달라졌으면(로그가 자랐거나 통째로 갈렸다) <c>setValue</c> 다.
    /// 그때는 <b>읽던 자리를 지킨다</b> — 그냥 밀면 화면이 맨 위로 튀어,
    /// 로그를 내려 읽던 사람에게는 그것이 고장으로 보인다.
    /// </para>
    /// </remarks>
    private async Task<bool> PushSideAsync(TextModel? model, List<string> pushed, List<string> want)
    {
        if (model is null)
        {
            return false;
        }

        if (pushed.Count != want.Count)
        {
            // 읽던 자리는 **번역 칸**에서 잰다. 휴대폰에서는 그것이 혼자 선
            // 편집기고, 넓은 화면에서는 비교 편집기의 오른쪽 칸이다.
            var view = ModifiedView;
            var top = view is not null ? await view.GetScrollTop() : 0;

            await model.SetValue(string.Join('\n', want));

            pushed.Clear();
            pushed.AddRange(want);

            if (top > 0 && view is not null)
            {
                await view.SetScrollTop((int)top);
            }

            return true;
        }

        var first = 0;

        while (first < want.Count && string.Equals(pushed[first], want[first], StringComparison.Ordinal))
        {
            first++;
        }

        if (first == want.Count)
        {
            return false;
        }

        var last = want.Count - 1;

        while (last > first && string.Equals(pushed[last], want[last], StringComparison.Ordinal))
        {
            last--;
        }

        await model.ApplyEdits(
        [
            new IdentifiedSingleEditOperation
            {
                // 끝 칸은 **지금 그 모델에 들어 있는** 줄의 길이다(`pushed`).
                // 새 줄의 길이로 적으면 짧아진 줄에서 뒷글자가 남는다.
                Range = new BlazorMonaco.Range(first + 1, 1, last + 1, pushed[last].Length + 1),
                Text = string.Join('\n', want.GetRange(first, last - first + 1)),
            },
        ], false);

        for (var i = first; i <= last; i++)
        {
            pushed[i] = want[i];
        }

        return false;
    }

    /// <summary>
    /// 불을 켠다 — <b>좌우 같은 줄에 같은 색</b>.
    /// </summary>
    /// <remarks>
    /// Monaco 가 들고 있는 「지금 줄」 표시(<c>renderLineHighlight</c>)를 끄고
    /// 이것으로 대신하는 까닭은 <b>그것이 커서가 있는 칸에만 켜지기</b>
    /// 때문이다. 여기서 보려는 것은 왼쪽 108번째 줄과 <b>짝인 오른쪽
    /// 108번째 줄</b>이라, 불이 한 칸에만 켜지면 반쪽이다.
    /// </remarks>
    private async Task DrawMarksAsync()
    {
        if (_original is not { } left || _modified is not { } right)
        {
            return;
        }

        await _marks.WaitAsync();

        try
        {
            _leftMarks = await left.DeltaDecorations(_leftMarks, MarksFor(onLeft: true), null);
            _rightMarks = await right.DeltaDecorations(_rightMarks, MarksFor(onLeft: false), null);
        }
        catch (Exception ex) when (ex is JSException or JSDisconnectedException
            or ObjectDisposedException or TaskCanceledException)
        {
        }
        finally
        {
            _marks.Release();
        }
    }

    /// <summary>
    /// 그 칸에 켤 불들. <b>줄 수는 모델에 들어 있는 것으로 센다</b>
    /// (<see cref="_pushedSrc"/>) — 아직 안 민 줄에 불을 켜면 Monaco 가 던진다.
    /// </summary>
    private List<ModelDeltaDecoration> MarksFor(bool onLeft)
    {
        var marks = new List<ModelDeltaDecoration>();
        var count = onLeft ? _pushedSrc.Count : _pushedOut.Count;

        if (_focusLine >= 1 && _focusLine <= count)
        {
            marks.Add(WholeLine(_focusLine, "pm-xlat__hit", "pm-xlat__hit-num"));
        }

        // 옮기는 중인 줄은 **오른쪽에만** 표시한다. 바뀌는 것이 그쪽이다.
        if (!onLeft && _redoLine >= 1 && _redoLine <= count)
        {
            marks.Add(WholeLine(_redoLine, "pm-xlat__redo", "pm-xlat__redo-num"));
        }

        return marks;
    }

    private static ModelDeltaDecoration WholeLine(int line, string css, string numCss) => new()
    {
        Range = new BlazorMonaco.Range(line, 1, line, 1),
        Options = new ModelDecorationOptions
        {
            IsWholeLine = true,
            ClassName = css,
            LineNumberClassName = numCss,
        },
    };

    /// <summary>
    /// 커서가 움직였다. <b>좌우 어느 칸에서 움직였든 같은 곳으로 온다</b> —
    /// 짝인 줄은 번호가 같아서, 어느 쪽에서 왔는지 가릴 까닭이 없다.
    /// </summary>
    private Task OnCursorMovedAsync(CursorPositionChangedEvent e)
        => FocusLineAsync(e.Position?.LineNumber ?? 0);

    private async Task FocusLineAsync(int line)
    {
        if (line <= 0 || line == _focusLine)
        {
            return;
        }

        _focusLine = line;

        await DrawMarksAsync();
    }

    /// <summary>
    /// 줄을 눌렀다. 불을 옮기고 <b>그 한 줄만 옮긴다.</b>
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>좌우 어느 칸을 눌러도 같다.</b> 한때는 왼쪽(원문)에만 걸어 두었다 —
    /// 「번역문을 눌러 번역을 시키는 것은 뜻이 맞지 않는다」가 까닭이었는데,
    /// 아직 안 옮긴 줄은 오른쪽에도 <b>원문이 서 있다.</b> 그 글을 누른
    /// 사람에게 왼쪽 것은 되고 오른쪽 것은 안 되는 까닭이 보일 리 없다.
    /// 짝인 줄은 번호가 같으므로 어느 쪽에서 왔는지 가리지 않는다.
    /// </para>
    /// <para>
    /// <b>커서 사건으로 하지 않는 까닭</b>이 있다. 커서는 화살표로도 움직이는데
    /// 거기에 번역을 걸면 키를 누르고 있는 동안 로컬 LLM 이 줄줄이 돈다.
    /// 누름은 사람이 그 줄을 <b>지목한</b> 것이다.
    /// </para>
    /// </remarks>
    private async Task OnLineClickedAsync(EditorMouseEvent e)
    {
        var line = e.Target?.Position?.LineNumber ?? 0;

        if (line <= 0)
        {
            return;
        }

        await FocusLineAsync(line);

        // **글자 자리를 누른 것만 받는다.** 줄 번호 칸을 누르면 그 줄이 통째로
        // 골라지고 **커서는 다음 줄 머리로 간다**(재어 확인했다 — 1번 줄 번호를
        // 누르면 커서가 2번 줄이다). 그대로 받으면 불은 2번 줄에 켜지는데
        // 옮기는 것은 1번 줄이라, 사람 눈에 **엉뚱한 줄이 도는 것**으로
        // 보인다. 줄 번호 누름은 고르기로 남겨 둔다.
        if (e.Target?.Type is not (MouseTargetType.CONTENT_TEXT or MouseTargetType.CONTENT_EMPTY))
        {
            return;
        }

        await TranslateLineAsync(line);
    }

    /// <summary>
    /// 그 줄 하나만 옮긴다. <b>옮긴 글은 짝인 오른쪽 줄 자리에 꽂힌다.</b>
    /// </summary>
    /// <remarks>
    /// 이미 옮긴 줄을 또 눌러도 다시 간다 — 번역이 어설픈 줄을 집어서 고쳐
    /// 받는 자리다. 돌고 있는 그 줄을 또 누른 것만 그냥 지나간다.
    /// </remarks>
    private async Task TranslateLineAsync(int line)
    {
        var at = line - 1;

        if (at < 0 || at >= _state.Source.Count)
        {
            return;
        }

        if (_redoLine == line)
        {
            // 같은 줄을 또 눌렀다. **돌고 있는 것을 끊고 다시 시작하지 않는다** —
            // 끊고 다시 걸면 사람이 기다리는 시간만 길어지고, 브라우저가
            // 누름을 두 번 올리는 날에는 그 한 번이 통째로 버려진다.
            return;
        }

        var text = _state.Source[at];

        if (string.IsNullOrWhiteSpace(text))
        {
            // 빈 줄은 옮길 것이 없다. 눌러도 조용히 지나간다 —
            // 「옮길 수 없습니다」를 띄우면 로그의 빈 줄마다 그 말이 뜬다.
            return;
        }

        if (text.Length > MaxLineChars)
        {
            Toasts.Show($"{line}번째 줄이 너무 길어 옮기지 않습니다.", NoticeTone.Warning);
            return;
        }

        // 앞서 누른 줄이 아직 돌고 있으면 거둔다. 사람이 줄을 연달아 누르는
        // 것은 **앞엣것을 그만두고 이것을 보겠다**는 뜻이다.
        CancelLineWork();

        var cts = _lineWork = new CancellationTokenSource();

        _redoLine = line;
        _state.Error = null;
        StateHasChanged();

        await DrawMarksAsync();

        try
        {
            var gen = _state.Generation;

            var answer = await Xlat.TranslateAsync([text], TargetLang, cts.Token);

            if (cts.IsCancellationRequested)
            {
                return;
            }

            if (_state.Generation != gen)
            {
                // 기다리는 사이에 로그가 통째로 갈렸다(앞이 잘려 나갔다).
                // 이 답은 **이제 없는 줄의 것**이다.
                return;
            }

            if (answer?.Lines is [{ Length: > 0 } got, ..])
            {
                _state.Out[at] = got;
                _state.DoneLines.Add(at);
                _state.Who = Who(answer) ?? _state.Who;

                await PushValuesAsync();
            }
        }
        catch (OperationCanceledException)
        {
            // 다음 줄을 눌렀거나 화면을 떠났다. 앞서 옮긴 것은 그대로 남는다.
        }
        catch (ApiException ex)
        {
            _state.Error = ex.Message is { Length: > 0 } message
                ? $"{line}번째 줄을 옮기지 못했습니다 — {message}"
                : $"{line}번째 줄을 옮기지 못했습니다.";

            Toasts.Show(_state.Error, NoticeTone.Warning);
        }
        catch (Exception ex) when (ex is JSException or JSDisconnectedException
            or ObjectDisposedException)
        {
            // 회로가 끊겼다. 알릴 곳이 없다.
        }
        finally
        {
            if (ReferenceEquals(_lineWork, cts))
            {
                _lineWork = null;
            }

            cts.Dispose();

            if (_redoLine == line)
            {
                _redoLine = 0;
                await DrawMarksAsync();
            }

            try
            {
                StateHasChanged();
            }
            catch (ObjectDisposedException)
            {
            }
        }
    }

    private void CancelLineWork()
    {
        var cts = _lineWork;
        _lineWork = null;

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
    /// 옮긴 줄을 모두 되돌린다. 오른쪽 칸이 다시 원문이 된다.
    /// </summary>
    /// <remarks>
    /// <b>옮기는 단추가 아니다.</b> 옮기는 일은 줄을 눌러 시키므로, 머리띠에
    /// 남는 일은 그 반대 — 섞여 버린 판을 처음 상태로 돌리는 것뿐이다.
    /// </remarks>
    private async Task ClearTranslationsAsync()
    {
        CancelLineWork();
        _redoLine = 0;

        _state.Rewind();

        await PushValuesAsync();
        await DrawMarksAsync();
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

    private async Task ToggleWrapAsync()
    {
        _wrap = !_wrap;

        // 한 번이라도 누르면 그 뒤로는 사람의 뜻을 지킨다 — 휴대폰으로
        // 넘어갈 때 저절로 켜는 일을 더 하지 않는다.
        _wrapTouched = true;

        try
        {
            // 서 있는 편집기에만 민다. 휴대폰에서는 외짝 편집기가 그것이다.
            if (_diff is { } diff)
            {
                await diff.UpdateOptions(new DiffEditorOptions
                {
                    WordWrap = _wrap ? "on" : "off",
                });
            }

            if (_solo is { } solo)
            {
                await solo.UpdateOptions(new EditorUpdateOptions
                {
                    WordWrap = _wrap ? "on" : "off",
                });
            }
        }
        catch (Exception ex) when (ex is JSException or JSDisconnectedException
            or ObjectDisposedException)
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
    /// <para>
    /// <b>「지금 줄」 표시를 끈다</b>(<c>RenderLineHighlight = "none"</c>).
    /// 그것은 <b>커서가 있는 칸에만</b> 켜지는데, 여기서 보려는 것은 왼쪽
    /// 108번째 줄과 <b>짝인 오른쪽 108번째 줄</b>이다. 좌우에 같이 켜는 일은
    /// 우리가 한다(<see cref="DrawMarksAsync"/>) — 둘 다 켜 두면 한쪽 줄에만
    /// 색이 두 겹으로 얹혀 좌우가 다른 색으로 보인다.
    /// </para>
    /// <para>
    /// <b>가운데 가름쇠를 못 끌게 한다</b>(<c>EnableSplitViewResizing = false</c>).
    /// 칸 이름표(「원문」·「번역」)가 반반을 믿고 서 있어서, 끌어 7:3 으로
    /// 만들면 <b>이름이 남의 칸을 가리킨다.</b>
    /// </para>
    /// <para>
    /// <b>휴대폰에서 줄을 짚어도 자판이 올라오지 않게 한다</b>
    /// (<c>DomReadOnly</c> · <c>EditContext</c>). <c>ReadOnly</c> 하나로는
    /// 모자란다 — 그것은 <b>글자가 들어오는 것만</b> 막고, 글자를 받는 숨은
    /// 칸은 그대로 선다. 휴대폰은 그 칸에 초점이 가는 것만 보고 자판을
    /// 올리므로, 줄 하나 짚을 때마다 화면 절반이 자판에 덮인다. 여기는
    /// <b>읽고 견주는 자리</b>라 자판이 할 일이 아예 없다.
    /// </para>
    /// <para>
    /// 두 가지를 같이 적어야 한다. Monaco 가 글자를 받는 길이 둘로 갈려
    /// 있어서다 — 숨은 <c>textarea</c> 와, 크롬 계열에서 기본으로 쓰는
    /// <c>EditContext</c>. <c>DomReadOnly</c> 는 앞의 것에만 <c>readonly</c> 를
    /// 달아 주고(자판이 안 올라온다), 뒤의 것은 읽기 전용이라는 말을 듣지
    /// 않는다. 그래서 <c>EditContext = false</c> 로 <b>옛 길로 돌려놓고</b>
    /// 거기에 <c>readonly</c> 를 단다. 돌려놓아도 잃는 것이 없다 — 그 길이
    /// 주는 것은 입력기(IME) 편의인데 여기서는 아무것도 입력하지 않는다.
    /// </para>
    /// </remarks>
    private StandaloneDiffEditorConstructionOptions BuildOptions(StandaloneDiffEditor editor) => new()
    {
        AutomaticLayout = true,
        ReadOnly = true,
        OriginalEditable = false,
        DomReadOnly = true,
        EditContext = false,
        RenderSideBySide = true,
        EnableSplitViewResizing = false,
        RenderLineHighlight = "none",
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
        ModifiedAriaLabel = "한국어로 옮긴 로그",

        // 막대는 Monaco 가 제 손으로 div 로 그린다(기본 14px) — app.css 의
        // 3px 규칙이 닿지 않으므로 여기서 같은 굵기로 맞춘다.
        Scrollbar = new EditorScrollbarOptions
        {
            VerticalScrollbarSize = 3,
            HorizontalScrollbarSize = 3,
        },
    };

    /// <summary>
    /// 휴대폰에서 혼자 서는 편집기의 옵션.
    /// </summary>
    /// <remarks>
    /// <para>
    /// 비교 편집기와 <b>같은 값을 고른다</b> — 읽기 전용 · 자판 안 부르기 ·
    /// 미니맵 끄기 · 줄 번호. 하나라도 어긋나면 기기를 돌릴 때마다 같은 글이
    /// 다른 모양으로 서서, 보는 사람에게는 <b>화면이 통째로 갈린 것</b>으로
    /// 읽힌다.
    /// </para>
    /// <para>
    /// <b>「지금 줄」 표시만 켜 둔다</b>(<c>RenderLineHighlight = "line"</c>).
    /// 비교판에서 그것을 끈 까닭은 <b>커서가 있는 칸에만</b> 켜져 좌우가 짝으로
    /// 안 보이기 때문인데, 여기는 칸이 하나뿐이라 그 문제가 없다. 우리가 켜는
    /// 불(<see cref="DrawMarksAsync"/>)이 그 위에 그대로 얹힌다.
    /// </para>
    /// <para>
    /// 글은 여기 넣지 않는다 — <see cref="_modified"/> 모델을 걸어 준다
    /// (<see cref="AttachModelsAsync"/>). 그래야 옮긴 줄이 꽂히는 자리와
    /// 켜 둔 불이 넓은 화면과 한 벌로 남는다.
    /// </para>
    /// </remarks>
    private StandaloneEditorConstructionOptions BuildSoloOptions(StandaloneCodeEditor editor) => new()
    {
        AutomaticLayout = true,
        ReadOnly = true,
        DomReadOnly = true,
        EditContext = false,
        RenderLineHighlight = "line",
        FontSize = (int)Math.Round(_fontPx),
        LineNumbers = "on",
        Minimap = new EditorMinimapOptions { Enabled = false },
        ScrollBeyondLastLine = false,
        WordWrap = _wrap ? "on" : "off",
        AriaLabel = "한국어로 옮긴 로그",
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
        if (_loader is not { } loader || (_diff is null && _solo is null))
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

            if (_diff is { } diff)
            {
                await diff.UpdateOptions(new DiffEditorOptions
                {
                    FontSize = (int)Math.Round(px),
                });
            }

            if (_solo is { } solo)
            {
                await solo.UpdateOptions(new EditorUpdateOptions
                {
                    FontSize = (int)Math.Round(px),
                });
            }
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

        CancelLineWork();

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

        if (_solo is { } solo)
        {
            try
            {
                await solo.DisposeEditor();
            }
            catch (Exception ex) when (ex is JSException or JSDisconnectedException
                or ObjectDisposedException or TaskCanceledException)
            {
            }

            _solo = null;
        }

        _attachedTo = null;

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

        _push.Dispose();
        _marks.Dispose();
    }
}
