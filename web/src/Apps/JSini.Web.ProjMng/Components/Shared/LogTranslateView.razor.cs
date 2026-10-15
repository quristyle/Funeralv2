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

    /// <summary>
    /// 칸 이름표 줄의 id. <b>브라우저가 가름쇠 자리를 여기에 적는다</b>
    /// (<c>--pm-xlat-left</c> · <see cref="WatchSplitAsync"/>). 편집기 id 에
    /// 매달아 지으므로 한 화면에 판이 둘이어도 안 겹친다.
    /// </summary>
    private string HeadsId => $"{_id}-heads";

    /// <summary>휴대폰에서 줄을 그리는 목록 상자. 바닥으로 보낼 때 이것을 넘긴다.</summary>
    private ElementReference _listEl;

    /// <summary>Monaco 스크립트를 다 받았나. <b>받기 전에는 편집기를 그리지 않는다.</b></summary>
    private bool _loaded;

    private IJSObjectReference? _loader;

    /// <summary>
    /// 브라우저가 되알려 줄 때 쥘 손잡이. 한 번만 만들고 치울 때 놓는다.
    /// </summary>
    /// <remarks>
    /// 올라오는 것이 둘이다 — <b>손가락 누름</b>(<see cref="OnLineTappedAsync"/>)과
    /// <b>가름쇠를 옮긴 자리</b>(<see cref="OnSplitMoved"/>). <b>손잡이는 한
    /// 벌이다</b> — 둘로 나누면 치울 때 한쪽을 빠뜨리는 날이 온다.
    /// </remarks>
    private DotNetObjectReference<LogTranslateView>? _tap;

    private StandaloneDiffEditor? _diff;

    /// <summary>
    /// 휴대폰인가. 참이면 <b>Monaco 가 아예 서지 않는다</b> — 줄을 <c>div</c>
    /// 하나씩 그대로 그리고 굴림은 브라우저에게 맡긴다(머리말의 표).
    /// </summary>
    private bool _phone;

    /// <summary>
    /// 로그의 끝을 보이는 자리에 두는 JS(<c>js/log-tail.js</c>). <b>Monaco 와
    /// 상관없는 길이다</b> — 그쪽 모듈(3MB 를 받아 오는 자리)과 섞지 않는다.
    /// </summary>
    private IJSObjectReference? _tail;

    /// <summary>목록을 한 번이라도 다룬 적이 있나. 폭이 바뀌면 다시 거짓이 된다.</summary>
    private bool _listTailed;

    /// <summary>바닥으로 보낼 때 세어 둔 줄 수. 자랐을 때만 다시 민다.</summary>
    private int _listCount;

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
    /// 번역 칸을 쥐고 있는 편집기. <b>넓은 화면에만 있다</b> — 휴대폰은
    /// Monaco 를 안 세우므로 <c>null</c> 이고, 구르는 자리는 브라우저가 쥔다.
    /// </summary>
    private StandaloneCodeEditor? ModifiedView => _diff?.ModifiedEditor;

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
    /// 화면 폭이 휴대폰 쪽으로 넘어갔거나 되돌아왔다. <b>보여 주는 것이 통째로 갈린다.</b>
    /// </summary>
    /// <remarks>
    /// <para>
    /// 옮겨 둔 줄은 그대로 남는다 — 옮긴 글은 편집기가 아니라 화면 밖
    /// (<see cref="AiLogXlatStore"/>)에 있다.
    /// </para>
    /// <para>
    /// <b>휴대폰으로 가면 비교 편집기를 우리 손으로 치운다.</b> BlazorMonaco 의
    /// 부품이 화면에서 사라져도 <b>브라우저의 Monaco 는 그대로 남는다</b> —
    /// 재어 확인했다(폭을 한 번 바꿀 때마다 편집기가 하나씩 쌓였다). 손잡이도
    /// 함께 놓는다. Blazor 는 사라진 부품의 참조(<c>@ref</c>)를 비워 주지
    /// 않아, 그대로 두면 이미 치운 편집기에 글을 밀게 된다.
    /// </para>
    /// <para>
    /// <b>넓은 화면으로 돌아오면 그때 Monaco 를 받는다.</b> 휴대폰에서는 아예
    /// 안 받으므로(<see cref="LoadMonacoAsync"/>) 여기가 받는 유일한 자리다.
    /// </para>
    /// </remarks>
    private async Task OnPhoneChangedAsync(bool phone)
    {
        if (_phone == phone)
        {
            return;
        }

        _phone = phone;

        // 줄바꿈은 휴대폰에서 켜고 시작한다. 넓은 화면에서 끄는 까닭은 좌우
        // 짝이 눈으로 흐트러지지 않게 하려는 것인데(`BuildOptions` 머리말),
        // 칸이 하나뿐인 자리에는 맞출 짝이 없다. 거기서 끈 채로 두면 로그 한
        // 줄이 화면 폭의 몇 배라 **옆으로 밀어 가며 읽어야 한다.**
        //
        // 사람이 단추를 한 번이라도 눌렀으면 그 뜻을 지킨다.
        if (!_wrapTouched)
        {
            _wrap = phone;
        }

        if (phone)
        {
            // 목록이 새로 선다. **바닥부터 보여 준다** — 로그를 펴는 일
            // 대부분이 방금 찍힌 줄을 보려는 것이다.
            _listTailed = false;

            if (_diff is { } leaving)
            {
                _diff = null;
                _attachedTo = null;

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
            return;
        }

        StateHasChanged();

        if (!_loaded)
        {
            // 휴대폰으로 열었던 판이다. 이제야 받는다.
            await LoadMonacoAsync();
        }
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
        if (_phone)
        {
            // 휴대폰에는 Monaco 가 없다. 그릴 것은 이미 그려졌고, 남은 일은
            // **바닥을 보여 주는 것**뿐이다.
            await FollowListAsync();

            // 탭을 떠나면 돌고 있는 줄을 거둔다. **넓은 화면 쪽과 같은
            // 판단이다**(아래) — 여기서 빠뜨리면 로그 탭을 떠난 사람 때문에
            // 로컬 LLM 이 휴대폰에서만 계속 돈다.
            if (!Active && _redoLine > 0)
            {
                CancelLineWork();
            }

            return;
        }

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
    /// <remarks>
    /// <para>
    /// <b>첫 그리기에서는 아직 폭을 모른다.</b> <c>DxLayoutBreakpoint</c> 는
    /// 브라우저에 재어 보고 알려 주므로, 이 자리에서 <see cref="_phone"/> 는
    /// 늘 거짓이다. 그대로 받아 버리면 <b>휴대폰이 3MB 짜리
    /// <c>editor.main.js</c> 를 쓰지도 않을 편집기 때문에 받는다.</b>
    /// </para>
    /// <para>
    /// 그래서 <b>한 박자 기다렸다가</b> 다시 본다. 늦어 봐야 그 한 박자이고
    /// (받는 데 드는 시간이 그보다 훨씬 길다), 그 사이에 폭이 안 왔으면
    /// 받는다 — 지금까지와 같은 자리로 떨어질 뿐 나빠지지 않는다. 휴대폰이라
    /// 건너뛴 판은 <see cref="OnPhoneChangedAsync"/> 가 넓은 화면으로 돌아올
    /// 때 다시 부른다.
    /// </para>
    /// </remarks>
    private async Task LoadMonacoAsync()
    {
        try
        {
            await Task.Delay(120);
        }
        catch (TaskCanceledException)
        {
            return;
        }

        if (_phone)
        {
            // 휴대폰이다. 받지 않는다 — 여기서 쓸 일이 없다.
            return;
        }

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
    /// 휴대폰 목록의 <b>끝을 보이는 자리에 둔다.</b>
    /// </summary>
    /// <remarks>
    /// <para>
    /// 로그를 펴는 일 대부분이 <b>방금 찍힌 줄</b>을 보려는 것인데, 맨 위에서
    /// 서면 거기까지 삼백 줄을 굴려야 한다. 비교 편집기 쪽에서
    /// <c>RevealLine</c> 이 하던 몫이다.
    /// </para>
    /// <para>
    /// <b>미는 것이 목록이 아니라 쪽이다</b>(<c>js/log-tail.js</c> 의
    /// <c>followPage</c>). 휴대폰의 목록은 제 안에서 구르지 않으므로
    /// (<c>projmng.css</c> 의 <c>.pm-xlat__list</c> — 받은 줄을 전부 그리고
    /// 높이를 내용에 맡긴다) 글상자가 쓰던 <c>follow</c> 로는 밀 곳이 없다.
    /// 목록을 담은 <b>구르는 조상</b>을 찾아 그것을 민다.
    /// </para>
    /// <para>
    /// 「위를 읽는 중이면 놓아 둔다」는 그대로다 — 처음 펴면 끝을 보여 주고,
    /// 사람이 위로 굴린 뒤에는 따라가지 않는다. 읽던 자리가 튀는 것이 새 줄을
    /// 놓치는 것보다 나쁘다.
    /// </para>
    /// <para>
    /// <b>줄 수가 달라졌을 때만 부른다.</b> 그리기 뒷정리는 단추 한 번에도
    /// 지나가는데, 그때마다 부르면 회로 왕복이 그만큼 는다.
    /// </para>
    /// </remarks>
    private async Task FollowListAsync()
    {
        var count = _state.Out.Count;

        if (count == 0)
        {
            return;
        }

        if (_listTailed && count == _listCount)
        {
            return;
        }

        _listTailed = true;
        _listCount = count;

        try
        {
            _tail ??= await Js.InvokeAsync<IJSObjectReference>(
                "import", "./_content/JSini.Web.ProjMng/js/log-tail.js");

            await _tail.InvokeVoidAsync("followPage", _listEl);
        }
        catch (Exception ex) when (ex is JSException or JSDisconnectedException
            or ObjectDisposedException or TaskCanceledException)
        {
            // 떠났거나 회로가 끊겼다. 굴릴 곳이 없다.
        }
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
    /// 거는 곳은 <b>비교 편집기 하나</b>다(휴대폰에는 편집기가 없다). 이미
    /// 걸려 있으면 아무것도 하지 않는다 — 그리기 뒷정리는 글을 밀 때마다
    /// 지나간다.
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

        Editor? target = _diff;

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
            if (target is StandaloneDiffEditor diff)
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

            // 손가락 누름은 Monaco 가 알려 주지 않는다. **새로 선 편집기마다**
            // 직접 지켜본다(`OnLineTappedAsync` 머리말).
            await WatchTapsAsync(target);

            // 가름쇠가 움직였다는 것도 알려 주지 않는다. 칸의 크기를 직접
            // 지켜보고 이름표를 그 자리에 맞춘다.
            await WatchSplitAsync();
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
    /// 지금 선 편집기의 <b>손가락 누름</b>을 지켜본다.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Monaco 가 손가락 누름에 <c>onMouseDown</c> 을 울리지 않아서, 누른
    /// 자리를 브라우저에서 직접 되묻고 올려 보낸다(<c>js/monaco-loader.js</c> 의
    /// <c>watchTaps</c> · 까닭은 <see cref="OnLineTappedAsync"/>).
    /// </para>
    /// <para>
    /// <b>주는 것은 편집기가 아니라 그것이 선 상자의 id 다.</b> 비교 편집기는
    /// 칸이 둘인데 누른 자리를 되물을 수 있는 것은 칸 안쪽의 편집기라, 상자로
    /// 묶어 찾으면 그 둘이 한꺼번에 걸린다.
    /// </para>
    /// <para>
    /// <b>휴대폰 때문에 생긴 길인데 지금은 휴대폰이 안 쓴다</b> — 거기서는
    /// Monaco 가 아니라 <c>div</c> 목록이라 그냥 눌린다. 남겨 두는 까닭은
    /// <b>손가락이 달린 넓은 화면</b>(태블릿·터치 노트북)이다. 거기서는
    /// 비교 편집기가 서고, Monaco 는 거기서도 <c>onMouseDown</c> 을 안 울린다.
    /// </para>
    /// </remarks>
    private async Task WatchTapsAsync(Editor target)
    {
        if (_loader is not { } loader)
        {
            return;
        }

        string[] ids = [_id];

        try
        {
            _tap ??= DotNetObjectReference.Create(this);

            await loader.InvokeVoidAsync("watchTaps", ids, _tap);
        }
        catch (Exception ex) when (ex is JSException or JSDisconnectedException
            or ObjectDisposedException or TaskCanceledException)
        {
            // 아직 안 섰거나 회로가 끊겼다. 다음에 걸릴 때 다시 건다.
        }
    }

    /// <summary>
    /// 가운데 가름쇠를 지켜보고 <b>칸 이름표를 그 자리에 맞춘다.</b>
    /// </summary>
    /// <remarks>
    /// <para>
    /// 이름표 줄(「원문」·「번역 · 한국어」)은 편집기 <b>밖</b>에 따로 서 있어서,
    /// 칸을 끌어 7:3 으로 만들면 이름표만 5:5 에 남아 <b>이름이 남의 칸을
    /// 가리킨다.</b> 그 어긋남 때문에 한동안 끄는 길을 아예 닫아 두었다.
    /// </para>
    /// <para>
    /// <b>자리를 맞추는 일은 브라우저 안에서 끝난다</b>(<c>js/monaco-loader.js</c>
    /// 의 <c>watchSplit</c>). 끄는 동안 프레임마다 회로를 왕복하면 Blazor
    /// Server 에서는 이름표가 손가락보다 한참 뒤에 따라온다 — 올라오는 것은
    /// 손을 뗀 뒤의 비율 하나다(<see cref="OnSplitMoved"/>).
    /// </para>
    /// <para>
    /// <b>끌어 두었던 자리를 되돌리는 것도 그쪽이 한다.</b> 비율을 함께
    /// 넘기는 까닭은 <c>BuildOptions</c> 머리말에 있다 — 그 옵션의 칸이
    /// <c>int?</c> 라 소수를 못 싣는다.
    /// </para>
    /// <para>
    /// <b>휴대폰에서는 걸 것이 없다</b> — 거기는 Monaco 가 아니고 칸도 하나다.
    /// 부르는 자리(<see cref="AttachModelsAsync"/>)가 이미 넓은 화면뿐이라
    /// 따로 가리지 않는다.
    /// </para>
    /// </remarks>
    private async Task WatchSplitAsync()
    {
        if (_loader is not { } loader)
        {
            return;
        }

        try
        {
            _tap ??= DotNetObjectReference.Create(this);

            await loader.InvokeVoidAsync("watchSplit", _id, HeadsId, _state.SplitRatio, _tap);
        }
        catch (Exception ex) when (ex is JSException or JSDisconnectedException
            or ObjectDisposedException or TaskCanceledException)
        {
            // 아직 안 섰거나 회로가 끊겼다. 다음에 걸릴 때 다시 건다.
        }
    }

    /// <summary>
    /// 사람이 가름쇠를 옮겼다. <b>적어 두기만 한다</b>(<see cref="AiLogXlatState.SplitRatio"/>).
    /// </summary>
    /// <remarks>
    /// <para>
    /// 쓰는 자리는 <b>편집기를 다시 세울 때</b> 하나다
    /// (<c>SplitViewDefaultRatio</c>). 지금 서 있는 편집기는 이미 그 자리에
    /// 있으므로 여기서 할 일이 없다.
    /// </para>
    /// <para>
    /// <b>다시 그리지 않는다.</b> 이 알림은 끄는 동안에도 몇 번 올라오는데
    /// (브라우저가 0.5% 문턱으로 추려 보낸다) 그때마다 회로가 화면을 다시
    /// 그리면 <b>Monaco 가 통째로 다시 서서</b> 끌던 손이 떨어진다.
    /// </para>
    /// </remarks>
    [JSInvokable]
    public void OnSplitMoved(double ratio)
    {
        if (double.IsFinite(ratio) && ratio is > 0 and < 1)
        {
            _state.SplitRatio = ratio;
        }
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
    /// 줄을 <b>마우스로</b> 눌렀다. 손가락 누름은 이리로 오지 않는다
    /// (<see cref="OnLineTappedAsync"/>).
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
    private Task OnLineClickedAsync(EditorMouseEvent e)
        => PickLineAsync(e.Target?.Position?.LineNumber ?? 0, e.Target?.Type);

    /// <summary>
    /// 휴대폰 목록에서 그 줄에 붙일 이름들. <b>켜 둔 불이 여기서 나온다</b> —
    /// 비교 편집기 쪽의 자리(<see cref="DrawMarksAsync"/>)에 해당한다.
    /// </summary>
    private string RowClass(int line)
    {
        if (line == _redoLine)
        {
            return "pm-xlat__row pm-xlat__row--redo";
        }

        return line == _focusLine ? "pm-xlat__row pm-xlat__row--hit" : "pm-xlat__row";
    }

    /// <summary>
    /// 휴대폰 목록에서 <b>줄을 눌렀다.</b>
    /// </summary>
    /// <remarks>
    /// <para>
    /// 거기서는 Monaco 가 아니라 <c>div</c> 라 <b>따로 받아 올릴 것이 없다</b> —
    /// 누른 자리의 종류를 되물을 일도, 줄 번호 칸을 가릴 일도 없다(줄 번호는
    /// 같은 <c>div</c> 안의 글자다).
    /// </para>
    /// <para>
    /// <b>불 켜기와 옮기기는 넓은 화면과 같은 자리로 간다</b> — 가는 길이
    /// 갈라지면 같은 화면이 기기마다 다르게 군다.
    /// </para>
    /// </remarks>
    private async Task OnRowClickedAsync(int line)
    {
        await FocusLineAsync(line);

        await TranslateLineAsync(line);
    }

    /// <summary>
    /// 줄을 <b>손가락으로</b> 눌렀다. 브라우저에서 <c>js/monaco-loader.js</c> 가
    /// 적어 보낸다.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Monaco 는 손가락 누름에 <c>onMouseDown</c> 을 울리지 않는다.</b>
    /// 터치가 있는 브라우저에서는 손짓을 쥐는 다른 길로 갈라지고, 그 길은
    /// 누름을 커서 옮기기로만 쓴 뒤 흉내 마우스 사건까지 막는다. 재어
    /// 확인했다 — 터치를 흉내 낸 크롬에서 줄 하나를 눌렀을 때 올라온 것은
    /// <c>touchstart · touchend · 커서 옮김</c> 뿐이었고 <c>onMouseDown</c> 은
    /// 한 번도 오지 않았다. 같은 자리를 마우스로 누르면 바로 온다.
    /// </para>
    /// <para>
    /// 그래서 「줄을 눌러 그 한 줄만 옮긴다」가 <b>안드로이드 크롬에서만
    /// 통째로 죽어 있었다</b> — PC 창을 좁혀 만든 반응형 화면은 멀쩡했다.
    /// 거기서는 손이 아니라 마우스가 누른다.
    /// </para>
    /// <para>
    /// <b>들어오는 자리를 마우스와 하나로 둔다</b>(<see cref="PickLineAsync"/>) —
    /// 줄 번호 칸을 지나치는 규칙이 길마다 갈라지면 같은 화면이 기기마다
    /// 다르게 군다.
    /// </para>
    /// </remarks>
    [JSInvokable]
    public Task OnLineTappedAsync(int line, int targetType)
        => InvokeAsync(() => PickLineAsync(line, (MouseTargetType)targetType));

    /// <summary>
    /// 사람이 짚은 줄. 불을 옮기고 <b>그 한 줄만 옮긴다.</b>
    /// </summary>
    private async Task PickLineAsync(int line, MouseTargetType? type)
    {
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
        if (type is not (MouseTargetType.CONTENT_TEXT or MouseTargetType.CONTENT_EMPTY))
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
    /// <b>가운데 가름쇠를 끌 수 있다</b>(<c>EnableSplitViewResizing</c>).
    /// 한동안 꺼 두었던 자리다 — 칸 이름표(「원문」·「번역」)가 반반을 믿고
    /// 서 있어서, 끌어 7:3 으로 만들면 <b>이름이 남의 칸을 가리켰다.</b>
    /// 지금은 이름표가 칸을 따라가므로(<see cref="WatchSplitAsync"/>) 막을
    /// 까닭이 없다.
    /// </para>
    /// <para>
    /// <b>첫 자리는 여기서 못 준다.</b> 탭을 건너갔다 오면 편집기가 새로
    /// 서는데 그때 끌어 둔 자리로 되돌리려면 <c>SplitViewDefaultRatio</c> 에
    /// 0~1 사이의 소수를 넣어야 하고, <b>BlazorMonaco 가 그 칸을 <c>int?</c>
    /// 로 선언해 두었다</b> — 넣을 수 있는 것이 0 과 1 뿐이다. 그래서 그
    /// 한 줄만 브라우저 쪽에서 민다(<see cref="WatchSplitAsync"/>).
    /// </para>
    /// <para>
    /// <b>좁아져도 좌우를 걷지 않는다</b>(<c>UseInlineViewWhenSpaceIsLimited
    /// = false</c>). Monaco 는 기본으로 폭이 900px 아래면 좌우 비교를 걷고
    /// 한 칸짜리 인라인 비교로 갈아타는데, 그 자리에서 <b>왼쪽 칸이 42px 짜리
    /// 토막으로 주저앉는다</b>(재어 확인했다 — 899px). 이름표 둘이 그대로
    /// 서 있으므로 「원문」이 가리킬 칸이 없어진다. 휴대폰은 여기까지 오지도
    /// 않으므로(위) 이 구간은 태블릿과 좁힌 창이고, 거기서 할 일은 칸을 걷는
    /// 것이 아니라 <b>끌어서 보고 싶은 쪽을 넓히는 것</b>이다.
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
    /// <para>
    /// <b>굴릴 때마다 도는 것을 눌러 둔다</b>(접기 · 글리프 칸 · 붙박이 머리 ·
    /// 들여쓰기 안내선 · 괄호 색 · 같은 낱말 표시 · 코드렌즈 · 색 상자 ·
    /// 개요 눈금). 여기 들어오는 것은 <b>로그 한 덩어리</b>라 그 가운데 뜻을
    /// 갖는 것이 하나도 없는데, 그려지는 줄마다 비용은 그대로 든다. 재어 보면
    /// 한 프레임에 드는 품이 <b>5.1ms → 4.1ms</b> 다(CPU 6배 느리게 · 300줄 ·
    /// 한 걸음 60px 로 80걸음).
    /// </para>
    /// <para>
    /// <b>오른쪽 클릭 창과 링크는 그대로 둔다.</b> 그 둘은 사람이 쓰는 것이라
    /// 굴림을 위해 걷을 것이 아니다 — 눌러서 되던 일이 안 되는 쪽이 느린
    /// 것보다 나쁘다.
    /// </para>
    /// <para>
    /// <b>휴대폰에는 이 옵션이 걸리지 않는다</b> — 거기는 Monaco 가 아니다
    /// (머리말). 옵션을 아무리 눌러도 손가락 굴림의 버벅임은 20%쯤만
    /// 줄어들어서, 그 자리는 굴림을 브라우저에게 돌려주는 쪽으로 풀었다.
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
        EnableSplitViewResizing = true,
        UseInlineViewWhenSpaceIsLimited = false,
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

        // ── 굴릴 때마다 도는 것을 눌러 둔다 ──────────────────
        // 로그에는 접을 블록도, 들여쓰기 뜻도, 괄호 짝도, 색값도 없다.
        Folding = false,
        GlyphMargin = false,
        StickyScroll = new EditorStickyScrollOptions { Enabled = false },
        BracketPairColorization = new BracketPairColorizationOptions { Enabled = false },
        // 괄호 안내선은 기본으로 이미 꺼져 있다 — 적지 않는다. 그 칸은
        // `boolean | 'active'` 라 BlazorMonaco 가 글자로 받는데, 목록에 없는
        // 글자("false")를 주면 Monaco 가 **말없이 기본값으로 되돌린다.**
        Guides = new GuidesOptions
        {
            Indentation = false,
            HighlightActiveIndentation = false,
        },
        OccurrencesHighlight = "off",
        SelectionHighlight = false,
        MatchBrackets = "never",
        ColorDecorators = false,
        CodeLens = false,
        RenderWhitespace = "none",
        OverviewRulerLanes = 0,

        // **바닥 단추 줄이 먹는 몫을 비워 둔다.** 이 판 아래에는 붙박인 단추
        // 줄이 있고(`pm-taskview__acts` — 넓은 화면에서도 `sticky` 다), 그
        // 줄이 편집기의 아래쪽을 덮는다. 쪽을 굴려 피할 수 있을 것 같지만
        // **편집기 위에서 굴리면 그 굴림을 Monaco 가 먹으므로** 쪽은 안
        // 움직인다. 안쪽에 그만큼 여백을 두면 마지막 줄이 줄 위로 올라온다.
        Padding = new EditorPaddingOptions { Bottom = 88 },

        // 막대는 Monaco 가 제 손으로 div 로 그린다(기본 14px) — app.css 의
        // 3px 규칙이 닿지 않으므로 여기서 같은 굵기로 맞춘다.
        Scrollbar = new EditorScrollbarOptions
        {
            VerticalScrollbarSize = 3,
            HorizontalScrollbarSize = 3,

            // 위아래 끝의 그늘. 굴릴 때마다 다시 그려지는데 3px 짜리 막대
            // 옆에서 알려 주는 것이 없다.
            UseShadows = false,
        },
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
        if (_loader is not { } loader || _diff is null)
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

        if (_tail is not null)
        {
            try
            {
                await _tail.DisposeAsync();
            }
            catch (Exception ex) when (ex is JSDisconnectedException or JSException)
            {
            }

            _tail = null;
        }

        _tap?.Dispose();
        _tap = null;

        _push.Dispose();
        _marks.Dispose();
    }
}
