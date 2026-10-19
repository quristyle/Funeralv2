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

    /// <summary>볼 로그 줄. 이 판이 로그 탭의 전부라 받은 줄을 그대로 다 그린다.</summary>
    [Parameter] public IReadOnlyList<string> Lines { get; set; } = [];

    /// <summary>옮긴 것을 든 이름. 건마다 달라야 한다 — 같으면 다른 지시를 열었을 때 앞 건의 번역이 보인다.</summary>
    [Parameter, EditorRequired] public string StateKey { get; set; } = string.Empty;

    /// <summary>보이고 있나. 거짓이 되면 돌고 있는 줄을 거둔다 — 탭을 떠난 사람 때문에 로컬 LLM 이 계속 돌 까닭이 없다.</summary>
    [Parameter] public bool Active { get; set; } = true;

    /// <summary>여기 선 줄이 무엇인가. 낭독기와 「불러오는 중」 줄에 들어간다. 칸 이름표는 어느 글에도 맞아 그대로 둔다.</summary>
    [Parameter] public string Subject { get; set; } = "로그";

    /// <summary>새 줄이 붙을 때 바닥까지 따라 내릴까. 로그는 참이고, 결과문은 첫 줄부터 읽는 글이라 거짓이다.</summary>
    [Parameter] public bool Tail { get; set; } = true;

    /// <summary>내용에 맞춰 틀 높이를 잡을까(기본 참). 높이는 Monaco 에게 물어 잡는다 — 줄 수로 어림하면 줄바꿈 때문에 틀린다. 휴대폰은 이 값과 상관없다.</summary>
    [Parameter] public bool Fit { get; set; } = true;

    /// <summary>스크롤이 필요할 때 틀의 최대 높이(px). 비우면 제한 없이 내용만큼 자란다.</summary>
    [Parameter] public int? MaxHeight { get; set; }

    /// <summary>줄바꿈을 켜고 시작할까. 사람이 단추를 한 번이라도 누르면 그 뜻이 이긴다. 로그는 꺼서 시작하고(줄 번호와 눈에 보이는 줄을 맞추려고), 결과문은 켠다.</summary>
    [Parameter] public bool Wrap { get; set; }

    /// <summary>줄 번호를 보일지. 기본은 숨김.</summary>
    [Parameter] public bool ShowLineNumbers { get; set; }

    /// <summary>줄 번호 상태. 첫 값만 <see cref="ShowLineNumbers"/> 에서 받는다.</summary>
    private bool _lineNumbers;

    /// <summary>연속으로 반복되는 로그 구간을 하나로 합쳐 반복 횟수와 함께 한 번만 보인다. 로그 탭에서 켠다.</summary>
    [Parameter] public bool CollapseRepeats { get; set; }

    /// <summary>옮겨 갈 말. 한국어로 고정이다 — 들어오는 것이 영어 로그라 영어로 돌리면 볼 것이 없다.</summary>
    private const string TargetLang = "ko";

    /// <summary>여러 줄 반복 축약 때 묶음으로 허용할 최대 줄 수.</summary>
    public const int MaxRepeatPatternLength = 10;

    /// <summary>이보다 긴 줄은 한 번에 보낼 양을 넘으므로 옮기지 않고 알린다 — 잘라 보내면 번역이 문장 가운데서 끊긴다. 서버 상한(LLMService.MaxTranslateInput)보다 낮게 둔다.</summary>
    private const int MaxLineChars = 6000;

    /// <summary>편집기 DOM 요소의 id. 한 화면에 둘이 설 수 있어 겹치지 않게 짓는다.</summary>
    private readonly string _id = $"pm-xlat-{Guid.NewGuid():N}";

    /// <summary>휴대폰에서 줄을 그리는 목록 상자. 바닥으로 보낼 때 이것을 넘긴다.</summary>
    private ElementReference _listEl;

    /// <summary>Monaco 스크립트를 다 받았나. <b>받기 전에는 편집기를 그리지 않는다.</b></summary>
    private bool _loaded;

    private IJSObjectReference? _loader;

    /// <summary>번역 손잡이. 손가락 누름 하나만 올라온다(OnLineTappedAsync).</summary>
    private DotNetObjectReference<LogTranslateView>? _tap;

    /// <summary>번역 칸을 쥔 편집기. 넓은 화면에만 있다 — 휴대폰은 Monaco 를 세우지 않아 null 이다.</summary>
    private StandaloneCodeEditor? _editor;

    /// <summary>휴대폰인가. 참이면 Monaco 를 세우지 않고 줄을 div 로 그린다.</summary>
    private bool _phone;

    /// <summary>로그 끝을 보이게 하는 JS(js/log-tail.js). Monaco 와 상관없는 길이다.</summary>
    private IJSObjectReference? _tail;

    /// <summary>목록을 한 번이라도 다룬 적이 있나. 폭이 바뀌면 다시 거짓이 된다.</summary>
    private bool _listTailed;

    /// <summary>바닥으로 보낼 때 세어 둔 줄 수. 자랐을 때만 다시 민다.</summary>
    private int _listCount;

    /// <summary>편집기에 거는 모델. 하나다 — 아직 안 옮긴 줄에는 원문이 들어 있어 칸 하나로 족하다.</summary>
    private TextModel? _model;

    /// <summary>모델을 만들었나. 편집기와 따로 센다 — 폭이 바뀌어 편집기가 갈려도 옮긴 줄은 살아 있어야 한다.</summary>
    private bool _modelBuilt;

    /// <summary>지금 모델을 걸어 둔 편집기. 참조로 가르며, 편집기가 갈리면 새 쪽에 다시 건다.</summary>
    private Editor? _attachedTo;

    /// <summary>옮긴 것. 화면 밖(AiLogXlatStore)에 있어 다른 탭으로 건너가도 번역이 살아 있다.</summary>
    private AiLogXlatState _state = new();

    private bool _wrap;

    /// <summary>줄바꿈 단추를 사람이 직접 눌렀나. 누른 뒤에는 폭이 바뀌어도 그 뜻을 지킨다.</summary>
    private bool _wrapTouched;

    private double _fontPx = 14;

    /// <summary>사람이 고른 본문 글꼴(풀린 값). 비면 Monaco 기본(고정폭)이다.</summary>
    private string? _fontFamily;

    /// <summary>내용에 맞춘 틀 높이(px). 0 이면 아직 못 쟀다. Fit 일 때만 쓰고, 휴대폰에는 걸지 않는다.</summary>
    private int _fitPx;

    /// <summary>칸이 <b>몇 px 짜리 글</b>을 들고 있나. Monaco 가 알려 준다.</summary>
    private double _contentPx;

    /// <summary>내용에 맞추더라도 이보다 낮게는 안 간다(px). 한 줄짜리가 띠만 해지는 것을 막는다.</summary>
    private const int FitMin = 96;

    /// <summary>커서가 선 줄(1부터). 0 이면 아직 아무 데도 안 눌렀다. 줄 표시는 Monaco 것이 아니라 우리 색으로 그린다.</summary>
    private int _focusLine;

    /// <summary>지금 옮기고 있는 줄(1부터, 0 이면 없다). 로컬 LLM 왕복이 몇 초라 이 불이 없으면 눌러도 아무 일이 없는 것으로 보인다.</summary>
    private int _redoLine;

    /// <summary>줄 하나를 옮기는 일. 다음 줄을 누르면 앞엣것을 거둔다.</summary>
    private CancellationTokenSource? _lineWork;

    /// <summary>켜 둔 불의 표. 다음에 지울 것을 Monaco 가 이 이름으로 찾는다.</summary>
    private List<string> _marksOn = [];

    /// <summary>글을 미는 길목의 빗장. 로그가 자라 뒤에 붙이는 일과 누른 줄을 꽂는 일이 엇갈리면 바뀐 줄을 못 찾는다.</summary>
    private readonly SemaphoreSlim _push = new(1, 1);

    /// <summary>불을 켜고 끄는 길목의 빗장. 엇갈리면 이미 없는 이름이 남아 꺼야 할 불이 켜진 채로 굳는다.</summary>
    private readonly SemaphoreSlim _marks = new(1, 1);

    /// <summary>편집기에 실제로 들어 있는 줄들. 바뀐 토막을 이것과 견주어 찾는다 — 글 전체를 회로로 나르지 않으려고 줄 단위로 든다.</summary>
    private readonly List<string> _pushedOut = [];

    /// <summary>복사 아이콘이 집어 갈 글. 들고 있는 줄에서 만든다(회로 왕복 없음).</summary>
    private string OutText => string.Join('\n', _state.Out);

    /// <summary>틀에 직접 적을 글. Fit 일 때만 생기며, 높이는 CSS 가 아니라 잰 값이다.</summary>
    private string? EditorStyle
        => Fit && !_phone && _fitPx > 0 ? $"height: {_fitPx}px" : null;

    /// <summary>Monaco 를 못 받았을 때 그 자리에 적을 한 줄.</summary>
    private string LoadingText => $"{Subject}{Particle(Subject)} 불러오는 중…";

    /// <summary>낭독기에게 읽어 줄 칸(과 휴대폰 목록) 이름.</summary>
    private string OutAria => $"한국어로 옮긴 {Subject}";

    /// <summary>받침을 보고 을·를 중 고른다. 들어올 이름을 다 알 수 없어 흔한 쪽으로 둔다.</summary>
    private static string Particle(string word)
    {
        if (word.Length == 0)
        {
            return "를";
        }

        var last = word[^1];

        if (last is < '\uAC00' or > '\uD7A3')
        {
            // 한글 음절이 아니다. 받침을 셀 수 없으므로 흔한 쪽으로 둔다.
            return "를";
        }

        return (last - '\uAC00') % 28 == 0 ? "를" : "을";
    }

    /// <summary>머리띠 오른쪽 한 줄 — 몇 줄 가운데 몇 줄을 옮겼나 · 누가 답했나 · 무엇이 걸렸나.</summary>
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
                // 줄을 누른 사람이 보려는 것은 전체 셈이 아니라 「내가 누른 줄이 지금 도는가」다.
                return $"{_redoLine}번째 줄을 옮기는 중…";
            }

            if (_state.DoneLines.Count == 0)
            {
                // 아직 한 줄도 안 옮겼으면 줄 수만 적는다 — 「0/300줄」은 도는 중으로 읽힌다.
                return $"{_state.Source.Count}줄";
            }

            var head = $"{_state.Source.Count}줄 가운데 {_state.DoneLines.Count}줄 옮김";

            return _state.Who is { Length: > 0 } who ? $"{head} · {who}" : head;
        }
    }

    protected override void OnInitialized()
    {
        // 테마 크기를 바꾸면 다시 잰다. 편집기는 옵션을 만들 때 한 번만 평가하므로 여기서 듣지 않으면 떠 있는 편집기만 옛 크기로 남는다.
        Size.Changed += OnThemeSizeChanged;

        // 줄바꿈의 첫 자리. 여기서만 받고, 그 뒤로는 단추의 뜻과 폭이 정한다.
        _wrap = Wrap;
        _lineNumbers = ShowLineNumbers;
    }

    protected override void OnParametersSet()
    {
        _state = Store.Use(StateKey);

        SyncSource();
    }

    /// <summary>부모가 준 줄을 들여온다. 자라는 꼬리는 뒤에 붙이고, 앞부분이 달라졌으면 옮긴 것을 통째로 버린다(이어 붙이면 번역이 엉뚱한 줄에 붙는다).</summary>
    private void SyncSource()
    {
        var incoming = PrepareIncomingLines();

        if (incoming.Count == _state.Source.Count && SharesHead(incoming, _state.Source.Count))
        {
            return;
        }

        // 1) 줄 수는 같은데 반복 횟수만 늘어난 경우 (돌고 있는 건에서 단일 또는 여러줄 패턴이 계속 찍힐 때)
        if (incoming.Count == _state.Source.Count && incoming.Count > 0)
        {
            var sameRaw = true;
            for (var i = 0; i < incoming.Count; i++)
            {
                var inc = incoming[i] ?? string.Empty;
                var src = _state.Source[i] ?? string.Empty;
                if (string.Equals(inc, src, StringComparison.Ordinal))
                {
                    continue;
                }

                var hasIncRepeat = TryGetRepeat(inc, out _, out var incRaw);
                var hasSrcRepeat = TryGetRepeat(src, out _, out var srcRaw);
                var rawInc = hasIncRepeat ? incRaw : inc;
                var rawSrc = hasSrcRepeat ? srcRaw : src;

                if (!string.Equals(rawInc, rawSrc, StringComparison.Ordinal))
                {
                    sameRaw = false;
                    break;
                }
            }

            if (sameRaw)
            {
                for (var i = 0; i < incoming.Count; i++)
                {
                    if (string.Equals(incoming[i], _state.Source[i], StringComparison.Ordinal))
                    {
                        continue;
                    }

                    _state.Source[i] = incoming[i];
                    if (_state.DoneLines.Contains(i))
                    {
                        if (TryGetRepeat(incoming[i], out var newCount, out _))
                        {
                            var outRaw = TryGetRepeat(_state.Out[i], out _, out var cleanOut) ? cleanOut : _state.Out[i];
                            _state.Out[i] = FormatRepeatLine(outRaw, newCount);
                        }
                    }
                    else
                    {
                        _state.Out[i] = incoming[i];
                    }
                }

                return;
            }
        }

        // 2) 앞부분이 그대로이고 뒤에 새 줄만 늘어난 경우
        if (incoming.Count > _state.Source.Count && SharesHead(incoming, _state.Source.Count))
        {
            for (var i = _state.Source.Count; i < incoming.Count; i++)
            {
                var line = incoming[i] ?? string.Empty;

                _state.Source.Add(line);
                _state.Out.Add(line);
            }

            return;
        }

        // 3) 새 줄이 늘어났는데 직전 꼬리 부근 줄들의 반복 횟수도 함께 늘어난 경우
        if (incoming.Count > _state.Source.Count && _state.Source.Count > 0)
        {
            var mismatch = 0;
            while (mismatch < _state.Source.Count && string.Equals(incoming[mismatch] ?? string.Empty, _state.Source[mismatch], StringComparison.Ordinal))
            {
                mismatch++;
            }

            var tailRepeatOnly = mismatch < _state.Source.Count && mismatch >= Math.Max(0, _state.Source.Count - MaxRepeatPatternLength);
            if (tailRepeatOnly)
            {
                for (var i = mismatch; i < _state.Source.Count; i++)
                {
                    var inc = incoming[i] ?? string.Empty;
                    var src = _state.Source[i] ?? string.Empty;
                    var hasIncRepeat = TryGetRepeat(inc, out _, out var incRaw);
                    var hasSrcRepeat = TryGetRepeat(src, out _, out var srcRaw);
                    var rawInc = hasIncRepeat ? incRaw : inc;
                    var rawSrc = hasSrcRepeat ? srcRaw : src;

                    if (!string.Equals(rawInc, rawSrc, StringComparison.Ordinal))
                    {
                        tailRepeatOnly = false;
                        break;
                    }
                }
            }

            if (tailRepeatOnly)
            {
                for (var i = mismatch; i < _state.Source.Count; i++)
                {
                    _state.Source[i] = incoming[i];
                    if (_state.DoneLines.Contains(i))
                    {
                        if (TryGetRepeat(incoming[i], out var newCount, out _))
                        {
                            var outRaw = TryGetRepeat(_state.Out[i], out _, out var cleanOut) ? cleanOut : _state.Out[i];
                            _state.Out[i] = FormatRepeatLine(outRaw, newCount);
                        }
                    }
                    else
                    {
                        _state.Out[i] = incoming[i];
                    }
                }

                for (var i = _state.Source.Count; i < incoming.Count; i++)
                {
                    var line = incoming[i] ?? string.Empty;

                    _state.Source.Add(line);
                    _state.Out.Add(line);
                }

                return;
            }
        }

        // 앞부분이 달라졌다 — 옮긴 것을 버린다. 이어 붙이면 번역이 엉뚱한 줄에 붙는다.
        _state.Reset(incoming.Select(line => line ?? string.Empty));
    }

    /// <summary>들어온 원본 줄을 화면에 올릴 줄로 다듬는다. CollapseRepeats 가 켜져 있으면 반복을 묶는다.</summary>
    private List<string> PrepareIncomingLines()
    {
        var raw = Lines as IList<string> ?? [.. Lines];
        if (!CollapseRepeats || raw.Count == 0)
        {
            return raw as List<string> ?? [.. raw];
        }

        return CollapseRepeatsList(raw);
    }

    /// <summary>연속으로 반복되는 줄을 하나로 축약하고 반복 횟수를 보인다. 여러 줄 패턴(최대 maxPatternLength 줄)도 묶는다.</summary>
    public static List<string> CollapseRepeatsList(IList<string> lines, int maxPatternLength = MaxRepeatPatternLength)
    {
        if (lines is null || lines.Count == 0)
        {
            return [];
        }

        if (maxPatternLength < 1)
        {
            maxPatternLength = 1;
        }

        var n = lines.Count;
        var dpSaved = new int[n + 1];
        var bestLen = new int[n];
        var bestCount = new int[n];

        for (var i = n - 1; i >= 0; i--)
        {
            // 기본 옵션: i번째 줄을 축약하지 않고 그대로 출력
            dpSaved[i] = dpSaved[i + 1];
            bestLen[i] = 1;
            bestCount[i] = 1;

            var maxLen = Math.Min(maxPatternLength, (n - i) / 2);

            // 긴 패턴부터 찾아야 3줄 반복이 1줄 부분 반복에 선점되어 빠지지 않는다.
            for (var len = maxLen; len >= 1; len--)
            {
                var maxK = 1;
                while (i + (maxK + 1) * len <= n)
                {
                    var match = true;
                    for (var m = 0; m < len; m++)
                    {
                        var expected = lines[i + m] ?? string.Empty;
                        var actual = lines[i + maxK * len + m] ?? string.Empty;
                        if (!string.Equals(expected, actual, StringComparison.Ordinal))
                        {
                            match = false;
                            break;
                        }
                    }

                    if (!match)
                    {
                        break;
                    }

                    maxK++;
                }

                if (maxK < 2)
                {
                    continue;
                }

                for (var c = maxK; c >= 2; c--)
                {
                    var immediateSaved = (c - 1) * len;
                    var saved = immediateSaved + dpSaved[i + c * len];
                    var currentImmediateSaved = (bestCount[i] - 1) * bestLen[i];

                    var isBetter = false;
                    if (saved > dpSaved[i])
                    {
                        isBetter = true;
                    }
                    else if (saved == dpSaved[i] && saved > 0)
                    {
                        // 절약 줄 수가 같으면 더 긴 패턴을 우선한다.
                        if (immediateSaved > currentImmediateSaved)
                        {
                            isBetter = true;
                        }
                        else if (immediateSaved == currentImmediateSaved)
                        {
                            if (len > bestLen[i])
                            {
                                isBetter = true;
                            }
                        }
                    }

                    if (isBetter)
                    {
                        dpSaved[i] = saved;
                        bestLen[i] = len;
                        bestCount[i] = c;
                    }
                }
            }
        }

        var result = new List<string>(n);
        var idx = 0;
        while (idx < n)
        {
            var count = bestCount[idx];
            var len = bestLen[idx];

            if (count >= 2)
            {
                var firstLine = lines[idx] ?? string.Empty;
                result.Add(FormatRepeatLine(firstLine, count));

                for (var k = 1; k < len; k++)
                {
                    result.Add(lines[idx + k] ?? string.Empty);
                }

                idx += count * len;
            }
            else
            {
                result.Add(lines[idx] ?? string.Empty);
                idx++;
            }
        }

        return result;
    }

    /// <summary>반복 횟수가 붙은 한 줄 텍스트를 만든다.</summary>
    public static string FormatRepeatLine(string line, int count)
    {
        if (count <= 1)
        {
            return line;
        }

        return string.IsNullOrEmpty(line)
            ? $"({count})"
            : $"({count}) {line}";
    }

    /// <summary>(숫자) 내용 형태의 반복 요약 줄인지 보고 횟수와 본문을 나눈다.</summary>
    public static bool TryGetRepeat(string? line, out int count, out string raw)
    {
        count = 1;
        raw = line ?? string.Empty;
        if (string.IsNullOrEmpty(line) || line[0] != '(')
        {
            return false;
        }

        var close = line.IndexOf(')');
        if (close > 1 && close < 10)
        {
            var span = line.AsSpan(1, close - 1);
            if (int.TryParse(span, out var parsed) && parsed > 1)
            {
                count = parsed;
                raw = close + 1 < line.Length && line[close + 1] == ' '
                    ? line[(close + 2)..]
                    : line[(close + 1)..];
                return true;
            }
        }

        return false;
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

    /// <summary>휴대폰으로 가면 편집기를 우리 손으로 치운다 — BlazorMonaco 가 부품을 치워도 브라우저의 Monaco 는 남고, @ref 도 비워지지 않는다. 넓은 화면으로 돌아오면 그때 Monaco 를 받는다.</summary>
    private async Task OnPhoneChangedAsync(bool phone)
    {
        if (_phone == phone)
        {
            return;
        }

        _phone = phone;

        // 휴대폰에서는 줄바꿈을 켜고 시작한다 — 좁은 폭에서 끄면 로그 한 줄이 화면 폭의 몇 배라 옆으로 밀어 읽어야 한다.
        // 사람이 단추를 한 번이라도 눌렀으면 그 뜻을 지킨다.
        if (!_wrapTouched)
        {
            _wrap = phone || Wrap;
        }

        if (phone)
        {
            // 목록이 새로 서면 바닥부터 보여 준다.
            _listTailed = false;

            if (_editor is { } leaving)
            {
                _editor = null;
                _attachedTo = null;

                try
                {
                    // 모델은 편집기가 아니라 monaco.editor 전역에 매달려 있어 따로 치운다.
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

    /// <summary>편집기가 브라우저에 다 섰다. 여기서 모델을 건다 — Blazor 는 자식의 뒷정리를 기다려 주지 않아, 부모의 SetModel 이 빈 편집기를 남긴다.</summary>
    private async Task OnEditorReadyAsync()
    {
        if (!_modelBuilt)
        {
            // 모델이 아직 없다. 만든 뒤에 그리기 뒷정리가 건다.
            return;
        }

        if (await AttachModelAsync())
        {
            await PushValuesAsync();
        }
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (_phone)
        {
            // 휴대폰에는 Monaco 가 없으니 남은 일은 바닥을 보여 주는 것뿐이다.
            await FollowListAsync();

            // 탭을 떠나면 돌고 있는 줄을 거둔다 — 넓은 화면과 같은 판단이다.
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

        if (!_modelBuilt)
        {
            await BuildModelAsync();
            return;
        }

        // 서 있는 편집기에 모델을 건다. 폭이 바뀌어 편집기가 갈렸으면 새 쪽에 다시 건다.
        if (!await AttachModelAsync())
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

    /// <summary>Monaco 를 받아 온다. 받기 전에 편집기를 그리면 회로가 끊긴다. 첫 그리기에서는 폭을 몰라 한 박자 기다렸다가 보고, 휴대폰이면 받지 않는다.</summary>
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

            // 옵션은 만들 때 한 번만 평가되므로 편집기를 그리기 전에 잰다.
            _fontPx = await loader.InvokeAsync<double>("baseFontPx");
            _fontFamily = await loader.InvokeAsync<string>("baseFontFamily");
            await loader.InvokeVoidAsync("followBodyFont");
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

    /// <summary>휴대폰 목록의 끝을 보이는 자리에 둔다. 미는 것은 목록을 담은 구르는 조상(js/log-tail.js 의 followPage)이며, 사람이 위로 굴렸으면 따라가지 않는다. 줄 수가 달라졌을 때만 부른다.</summary>
    private async Task FollowListAsync()
    {
        if (!Tail)
        {
            // 자라지 않는 결과문은 맨 위에서 선다.
            return;
        }

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

    /// <summary>모델을 만든다. 편집기와 따로 만든다 — 글은 모델에 넣어야 바뀐 토막만 꽂을 수 있고, 켜 둔 불도 모델에 매달린다.</summary>
    private async Task BuildModelAsync()
    {
        try
        {
            // 주소가 모델의 열쇠다. 한 화면에 둘이 설 수 있어 겹치지 않게 짓는다.
            _model = await Global.CreateModel(
                Js, string.Join('\n', _state.Out), "plaintext",
                $"inmemory://jsini/{_id}/out");
        }
        catch (Exception ex) when (ex is JSException or JSDisconnectedException
            or ObjectDisposedException or TaskCanceledException)
        {
            return;
        }

        _pushedOut.Clear();
        _pushedOut.AddRange(_state.Out);

        _modelBuilt = true;

        // 이번 그리기에는 아직 거는 일이 남았다. 다음 뒷정리에서 이어 간다.
        StateHasChanged();
    }

    /// <summary>지금 선 편집기에 모델을 건다. 돌려주는 것은 「걸려 있나」. 건 뒤 레이아웃을 직접 부른다 — AutomaticLayout 은 Blazor Server 에서 제때 안 걸려 줄이 안 그려진다.</summary>
    private async Task<bool> AttachModelAsync()
    {
        if (_model is not { } model)
        {
            return false;
        }

        Editor? target = _editor;

        if (target is null)
        {
            return false;
        }

        if (ReferenceEquals(_attachedTo, target))
        {
            return true;
        }

        // 걸기 전에 적어 둔다 — 거는 자리가 둘이라 걸고 나서 적으면 그 사이 다른 쪽이 같은 일을 두 번 해 불이 두 벌로 남는다.
        _attachedTo = target;

        try
        {
            if (target is StandaloneCodeEditor view)
            {
                await view.SetModel(model);
                await ApplyThemeAsync();

                await view.Layout();

                // 바닥을 보여 준다 — 폭이 바뀌어 갈린 새 편집기도 맨 위에서 서지 않게.
                if (Tail && _pushedOut.Count > 1)
                {
                    await view.RevealLine(_pushedOut.Count);
                }
            }

            // 편집기가 갈리면 켜 둔 불도 함께 사라진다. 다시 켠다.
            await DrawMarksAsync();

            // 손가락 누름은 Monaco 가 알려 주지 않으므로 새 편집기마다 직접 지켜본다.
            await WatchTapsAsync(target);

            // 내용에 맞추는 틀이면 **여기서 한 번 직접 묻는다**(머리말).
            await MeasureFitAsync();
        }
        catch (Exception ex) when (ex is JSException or JSDisconnectedException
            or ObjectDisposedException or TaskCanceledException)
        {
            // 아직 안 섰거나 회로가 끊겼다. 적어 둔 것을 지워 OnDidInit 이 올 때 다시 걸게 한다.
            if (ReferenceEquals(_attachedTo, target))
            {
                _attachedTo = null;
            }

            return false;
        }

        return true;
    }

    /// <summary>선 편집기의 손가락 누름을 지켜본다. Monaco 는 손가락 누름에 onMouseDown 을 울리지 않아 브라우저에서 직접 되묻는다(monaco-loader.js 의 watchTaps). 주는 것은 편집기가 아니라 그 상자의 id 다.</summary>
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

    /// <summary>편집기 테마를 문서의 data-theme 에 맞춘다. 흰 바탕에 밝은 편집기가 얹히면 그 부분만 튄다.</summary>
    private async Task ApplyThemeAsync()
    {
        var dark = await Js.InvokeAsync<bool>("jsiniTheme.isDark");

        await Global.SetTheme(Js, dark ? "vs-dark" : "vs");
    }

    /// <summary>바뀐 줄만 모델에 민다 — 글 전체를 회로로 나르면 줄 하나를 누를 때마다 수백 줄이 왕복한다.</summary>
    private async Task PushValuesAsync()
    {
        if (!_modelBuilt)
        {
            return;
        }

        // 빗장을 건다 — 미는 쪽이 둘이라(뒤에 붙이기 · 줄 꽂기) 엇갈리면 민 것과 적어 둔 것이 달라진다.
        await _push.WaitAsync();

        try
        {
            // 줄 수가 달라졌을 때만 잰다 — 이 길은 그리기 뒷정리마다 지나가서 재면 왕복이 늘 선다.
            var grew = _pushedOut.Count != _state.Out.Count;

            // 밀기 전에 바닥에 붙어 있나를 잰다. 민 뒤에 재면 늘어난 줄 때문에 늘 거짓이다.
            var tail = Tail && grew && await AtBottomAsync();

            var reset = await PushSideAsync(_model, _pushedOut, _state.Out);

            if (reset || CollapseRepeats)
            {
                // `setValue` 는 켜 둔 불을 함께 지운다. 다시 켠다.
                await DrawMarksAsync();
            }

            // 줄이 늘었고 사람이 바닥을 보고 있었으면 따라 내린다. 위를 읽는 중이면 놓아 둔다.
            if (tail && reset && _pushedOut.Count > 1 && _editor is { } view)
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

    /// <summary>지금 바닥에 붙어 있나. 한 줄 높이만큼은 봐 준다 — 딱 맞아떨어지는 일이 드물다.</summary>
    private async Task<bool> AtBottomAsync()
    {
        if (_editor is not { } view)
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

    /// <summary>모델을 맞춘다. 돌려주는 것은 통째로 갈아 끼웠나 — 그랬다면 켜 둔 불을 부르는 쪽이 다시 켠다. 줄 수가 같으면 달라진 토막만 applyEdits 로 꽂고, 달라지면 setValue 로 읽던 자리를 지킨다.</summary>
    private async Task<bool> PushSideAsync(TextModel? model, List<string> pushed, List<string> want)
    {
        if (model is null)
        {
            return false;
        }

        if (pushed.Count != want.Count)
        {
            // 읽던 자리를 지킨다. 휴대폰에는 편집기가 없어 넘어간다.
            var view = _editor;
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
                // 끝 칸은 지금 모델에 들어 있는 줄의 길이다 — 새 줄 길이로 적으면 짧아진 줄에서 뒷글자가 남는다.
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

    /// <summary>불을 켠다 — 짚은 줄과 지금 도는 줄. Monaco 의 지금 줄 표시 대신 우리 색으로 그린다(두 색이 한 자에서 갈려야 구분된다).</summary>
    private async Task DrawMarksAsync()
    {
        if (_model is not { } model)
        {
            return;
        }

        await _marks.WaitAsync();

        try
        {
            _marksOn = await model.DeltaDecorations(_marksOn, Marks(), null);
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

    /// <summary>켤 불들. 줄 수는 모델에 들어 있는 것으로 센다 — 아직 안 민 줄에 불을 켜면 Monaco 가 던진다.</summary>
    private List<ModelDeltaDecoration> Marks()
    {
        var marks = new List<ModelDeltaDecoration>();
        var count = _pushedOut.Count;

        if (_focusLine >= 1 && _focusLine <= count)
        {
            marks.Add(WholeLine(_focusLine, "pm-xlat__hit", "pm-xlat__hit-num"));
        }

        if (_redoLine >= 1 && _redoLine <= count)
        {
            marks.Add(WholeLine(_redoLine, "pm-xlat__redo", "pm-xlat__redo-num"));
        }

        if (CollapseRepeats)
        {
            var list = _pushedOut;
            for (var i = 0; i < count; i++)
            {
                var lineNum = i + 1;
                if (TryGetRepeat(list[i], out var repeatCount, out _))
                {
                    var prefixLen = $"({repeatCount})".Length;
                    marks.Add(new ModelDeltaDecoration
                    {
                        Range = new BlazorMonaco.Range(lineNum, 1, lineNum, prefixLen + 1),
                        Options = new ModelDecorationOptions
                        {
                            InlineClassName = "pm-xlat__repeat-badge",
                        },
                    });
                }
            }
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

    /// <summary>글이 차지하는 높이가 달라졌다(Monaco 가 알려 준다). 줄이 늘거나, 줄바꿈을 켜고 끄거나, 칸 폭이 바뀌면 온다.</summary>
    private Task OnSizedAsync(ContentSizeChangedEvent e)
    {
        _contentPx = e.ContentHeight;

        return ApplyFitAsync();
    }

    /// <summary>편집기가 막 섰을 때 한 번 직접 묻는다 — 높이가 이미 정해져 있으면 크기 사건이 한 번도 안 온다.</summary>
    private async Task MeasureFitAsync()
    {
        if (!Fit || _phone || _editor is not { } view)
        {
            return;
        }

        try
        {
            _contentPx = await view.GetContentHeight();
        }
        catch (Exception ex) when (ex is JSException or JSDisconnectedException
            or ObjectDisposedException or TaskCanceledException)
        {
            return;
        }

        await ApplyFitAsync();
    }

    /// <summary>잰 값으로 틀 높이를 잡고 편집기에게 다시 재라고 한다. 되돌이는 생기지 않는다 — 글이 차지하는 높이는 칸 폭과 줄 수가 정한다. 가로 막대가 흔들리는 서너 px 은 문턱으로 누른다.</summary>
    private async Task ApplyFitAsync()
    {
        if (!Fit || _phone)
        {
            return;
        }

        var want = (int)Math.Max(_contentPx, FitMin);
        if (MaxHeight is { } max && max >= FitMin)
        {
            want = Math.Min(want, max);
        }

        if (Math.Abs(want - _fitPx) <= 4)
        {
            return;
        }

        _fitPx = want;

        StateHasChanged();

        if (_editor is not { } view)
        {
            return;
        }

        try
        {
            await view.Layout();
        }
        catch (Exception ex) when (ex is JSException or JSDisconnectedException
            or ObjectDisposedException or TaskCanceledException)
        {
        }
    }

    /// <summary>
    /// 커서가 움직였다. 그 줄에 불을 켠다.
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

    /// <summary>줄을 마우스로 눌렀다. 손가락 누름은 OnLineTappedAsync 로 온다. 이미 옮긴 줄도 받는다(고쳐 받는 자리). 커서 사건으로 하지 않는다 — 화살표 키에 로컬 LLM 이 줄줄이 돌기 때문이다.</summary>
    private Task OnLineClickedAsync(EditorMouseEvent e)
        => PickLineAsync(e.Target?.Position?.LineNumber ?? 0, e.Target?.Type);

    /// <summary>휴대폰 목록에서 그 줄에 붙일 이름들. 켜 둔 불이 여기서 나온다.</summary>
    private string RowClass(int line)
    {
        if (line == _redoLine)
        {
            return "pm-xlat__row pm-xlat__row--redo";
        }

        return line == _focusLine ? "pm-xlat__row pm-xlat__row--hit" : "pm-xlat__row";
    }

    /// <summary>휴대폰 목록에서 줄을 눌렀다. div 라 따로 받아 올릴 것이 없고, 옮기기와 불 켜기는 넓은 화면과 같은 길로 간다.</summary>
    private async Task OnRowClickedAsync(int line)
    {
        await FocusLineAsync(line);

        await TranslateLineAsync(line);
    }

    /// <summary>줄을 손가락으로 눌렀다(monaco-loader.js 가 적어 보낸다). Monaco 는 손가락 누름에 onMouseDown 을 울리지 않아 이 길이 따로 있다. 들어오는 자리는 마우스와 하나로 둔다(PickLineAsync).</summary>
    [JSInvokable]
    public Task OnLineTappedAsync(int line, int targetType)
        => InvokeAsync(() => PickLineAsync(line, (MouseTargetType)targetType));

    /// <summary>사람이 짚은 줄. 불을 옮기고 그 한 줄만 옮긴다.</summary>
    private async Task PickLineAsync(int line, MouseTargetType? type)
    {
        if (line <= 0)
        {
            return;
        }

        await FocusLineAsync(line);

        // 글자 자리를 누른 것만 받는다. 줄 번호를 누르면 커서가 다음 줄로 가 엉뚱한 줄에 불이 켜진다.
        if (type is not (MouseTargetType.CONTENT_TEXT or MouseTargetType.CONTENT_EMPTY))
        {
            return;
        }

        await TranslateLineAsync(line);
    }

    /// <summary>그 줄 하나만 옮긴다. 옮긴 글은 누른 자리에 꽂힌다. 돌고 있는 그 줄을 또 누른 것만 지나간다.</summary>
    private async Task TranslateLineAsync(int line)
    {
        var at = line - 1;

        if (at < 0 || at >= _state.Source.Count)
        {
            return;
        }

        if (_redoLine == line)
        {
            // 같은 줄을 또 눌렀다 — 돌고 있는 것을 끊고 다시 걸지 않는다.
            return;
        }

        var text = _state.Source[at];

        if (string.IsNullOrWhiteSpace(text))
        {
            // 빈 줄은 조용히 지나간다 — 옮길 수 없다는 말을 띄우면 빈 줄마다 뜬다.
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

            var toTranslate = text;
            var hasRepeat = false;
            var repeatCount = 1;
            if (CollapseRepeats && TryGetRepeat(text, out var rCount, out var cleanText))
            {
                hasRepeat = true;
                repeatCount = rCount;
                if (!string.IsNullOrWhiteSpace(cleanText))
                {
                    toTranslate = cleanText;
                }
            }

            var answer = await Xlat.TranslateAsync([toTranslate], TargetLang, cts.Token);

            if (cts.IsCancellationRequested)
            {
                return;
            }

            if (_state.Generation != gen)
            {
                // 기다리는 사이 로그가 갈렸다 — 이 답은 이제 없는 줄의 것이다.
                return;
            }

            if (answer?.Lines is [{ Length: > 0 } got, ..])
            {
                var finalOut = hasRepeat ? FormatRepeatLine(got, repeatCount) : got;
                _state.Out[at] = finalOut;
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

    /// <summary>옮긴 줄을 모두 되돌린다. 머리띠에 남는 일은 이것뿐이다(옮기기는 줄을 눌러 시킨다).</summary>
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

    /// <summary>자동 줄바꿈을 켜거나 끈다. 받은 값을 그대로 쓴다 — 여기서 뒤집으면 눌러도 제자리인 단추가 된다.</summary>
    private async Task SetWrapAsync(bool on)
    {
        if (_wrap == on)
        {
            return;
        }

        _wrap = on;

        // 한 번이라도 누르면 그 뒤로는 사람의 뜻을 지킨다.
        _wrapTouched = true;

        // 아이콘 색을 먼저 바꾼다 — 아래의 왕복 동안 옛 모습이면 한 번 더 누르게 된다.
        StateHasChanged();

        try
        {
            // 서 있는 편집기에만 민다. 휴대폰 목록은 이름 클래스로 접고 편다.
            if (_editor is { } view)
            {
                await view.UpdateOptions(new EditorUpdateOptions
                {
                    WordWrap = _wrap ? "on" : "off",
                });
            }
        }
        catch (Exception ex) when (ex is JSException or JSDisconnectedException
            or ObjectDisposedException)
        {
        }

        // 접힌 줄 수가 달라지면 높이도 달라지므로, 사건이 안 오는 날에도 한 번 묻는다.
        await MeasureFitAsync();
    }

    /// <summary>줄 번호를 켜거나 끈다. 휴대폰 목록은 다시 그리면 따라오고, 서 있는 편집기에는 옵션을 민다.</summary>
    private async Task SetLineNumbersAsync(bool on)
    {
        if (_lineNumbers == on)
        {
            return;
        }

        _lineNumbers = on;
        StateHasChanged();

        try
        {
            if (_editor is { } view)
            {
                await view.UpdateOptions(new EditorUpdateOptions
                {
                    LineNumbers = _lineNumbers ? "on" : "off",
                });
            }
        }
        catch (Exception ex) when (ex is JSException or JSDisconnectedException
            or ObjectDisposedException)
        {
        }
    }

    /// <summary>편집기 옵션. 읽는 자리라 ReadOnly 이고 줄바꿈은 꺼서 시작한다(줄 번호와 눈에 보이는 줄을 맞추려고).
    /// 휴대폰에서 자판이 올라오지 않도록 DomReadOnly 와 EditContext = false 를 함께 둔다 — ReadOnly 만으로는 숨은 입력칸이 자판을 올린다.</summary>
    private StandaloneEditorConstructionOptions BuildOptions(StandaloneCodeEditor editor) => new()
    {
        AutomaticLayout = true,
        ReadOnly = true,
        DomReadOnly = true,
        EditContext = false,
        RenderLineHighlight = "none",
        FontSize = (int)Math.Round(_fontPx),
        FontFamily = string.IsNullOrEmpty(_fontFamily) ? null : _fontFamily,
        LineNumbers = _lineNumbers ? "on" : "off",
        Minimap = new EditorMinimapOptions { Enabled = false },
        ScrollBeyondLastLine = false,
        WordWrap = _wrap ? "on" : "off",
        AriaLabel = OutAria,

        // 로그에는 접을 블록·들여쓰기 뜻·괄호 짝·색값이 없어 굴림마다 도는 기능을 끈다.
        Folding = false,
        GlyphMargin = false,
        StickyScroll = new EditorStickyScrollOptions { Enabled = false },
        BracketPairColorization = new BracketPairColorizationOptions { Enabled = false },
        // 이 칸은 boolean | 'active' 라 글자로 받는다 — "false" 를 주면 Monaco 가 말없이 기본값으로 되돌린다.
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

        // 바닥 단추 줄이 편집기 아래를 덮으므로 안쪽에 그만큼 여백을 둔다 — 편집기 위에서 굴리면 Monaco 가 굴림을 먹는다.
        Padding = new EditorPaddingOptions { Bottom = Fit ? 8 : 88 },

        // 막대는 Monaco 가 div 로 그린다(기본 14px). app.css 의 3px 규칙이 닿지 않아 여기서 맞춘다.
        Scrollbar = new EditorScrollbarOptions
        {
            VerticalScrollbarSize = 3,
            HorizontalScrollbarSize = 3,

            // 위아래 그늘은 굴릴 때마다 다시 그려져 끈다.
            UseShadows = false,
            AlwaysConsumeMouseWheel = false,
        },
    };

    /// <summary>InvokeAsync 로 넘긴다 — 이 알림은 테마 서랍에서 오고, await 하는 순간 회로의 동기화 문맥 밖으로 나간다.</summary>
    private void OnThemeSizeChanged() => _ = InvokeAsync(ApplyFontSizeAsync);

    /// <summary>본문 글자 크기를 다시 재서 편집기에 민다. 알림은 새 크기로 그리기 전에 오므로 재는 것을 한 박자 미룬다.</summary>
    private async Task ApplyFontSizeAsync()
    {
        if (_loader is not { } loader || _editor is null)
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

            if (_editor is { } view)
            {
                await view.UpdateOptions(new EditorUpdateOptions
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

    /// <summary>치울 때는 편집기를 먼저 놓고 모델을 놓는다 — 편집기가 쥔 채 모델을 놓으면 브라우저가 던진다. 모델을 안 놓으면 판을 열 때마다 로그 한 벌이 쌓인다.</summary>
    public async ValueTask DisposeAsync()
    {
        Size.Changed -= OnThemeSizeChanged;

        CancelLineWork();

        if (_editor is { } view)
        {
            try
            {
                await view.DisposeEditor();
            }
            catch (Exception ex) when (ex is JSException or JSDisconnectedException
                or ObjectDisposedException or TaskCanceledException)
            {
            }

            _editor = null;
        }

        _attachedTo = null;

        if (_model is { } model)
        {
            try
            {
                await model.DisposeModel();
            }
            catch (Exception ex) when (ex is JSException or JSDisconnectedException
                or ObjectDisposedException or TaskCanceledException)
            {
                // 회로가 이미 끊겼다(사용자가 탭을 닫았다). 놓을 것이 없다.
            }

            _model = null;
        }

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
