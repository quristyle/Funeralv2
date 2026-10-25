using System.Text.RegularExpressions;

using JSini.Web.Admin.Api;
using JSini.Web.Http;

using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.JSInterop;

namespace JSini.Web.Admin.Components.Pages;

public partial class Assistant
{
    [Inject] private AssistantClient Api { get; set; } = default!;

    [Inject] private IJSRuntime Js { get; set; } = default!;

    /// <summary>요청글 번호(<c>#1042</c>)를 되짚는다 — 도구 결과가 <c>- #{Id} 제목 [상태] (...)</c>
    /// 모양으로 적고(<c>PortalToolRunner.RenderRequest</c>) 시스템 프롬프트가 "숫자·글 번호는 도구
    /// 결과에 적힌 그대로 옮기라"고 못박으므로(<c>AssistantAgent.SystemPrompt</c>) 답에도 그 모양이
    /// 그대로 나온다. 자릿수를 2~6 으로 묶은 것은 두 가지를 동시에 막는다 —
    /// 위로는 <c>int.Parse</c> 가 넘칠 걱정이 아예 없고(6자리라야 999999, int 범위에 한참 못
    /// 미친다 — 모델이 쓸 법한 11자리 같은 수도 그냥 안 걸린다), 아래로는 "매출 순위 #1" 처럼
    /// 흔한 한 자리 숫자를 글 번호로 잘못 읽는 것을 막는다. <c>(?&lt;!\S)</c> 는 <c>#</c> 앞이
    /// 글 시작이거나 빈칸이어야 한다는 뜻이라 <c>##3단계</c>(마크다운 제목 꾸밈) 의 두 번째 <c>#</c>
    /// 는 걸리지 않는다.</summary>
    private static readonly Regex RequestRefPattern = new(@"(?<!\S)#(\d{2,6})(?!\d)", RegexOptions.Compiled);

    /// <summary>자료 범위(RAG 카테고리) 목록. 못 받으면 빈 목록 — 「모든 카테고리」로만 묻는다.</summary>
    private IReadOnlyList<AssistantScope> _scopes = [];

    /// <summary>고른 범위의 카테고리 열쇠. 비면 모든 카테고리.</summary>
    private string? _scope;

    /// <summary>한 번 묻고 받은 한 덩이 — 질문 · 진행 안내 · 답.</summary>
    private sealed class Turn
    {
        public required string Question { get; init; }

        /// <summary>도구가 도는 동안 온 진행 안내(<c>kind: "tool"</c>). 끝난 뒤에도 남겨 둔다 —
        /// 무엇을 뒤져 답했는지가 그대로 근거의 일부다.</summary>
        public List<string> ToolNotices { get; } = [];

        /// <summary>완성된 답. 서버가 한 번에 통째로 보낸다(글자 단위 스트리밍이 아니다) — 그래서
        /// 빈 문자열에서 바로 완성된 글로 바뀌고, 타이핑 커서를 그릴 자리가 없다.</summary>
        public string Answer { get; set; } = string.Empty;

        public bool Streaming { get; set; }

        /// <summary>중단 단추로 끊겨 답을 못 받았나. 답도 없고 더 이상 돌지도 않는데 아무 표시가
        /// 없으면 중단한 것인지 그냥 안 온 것인지 가릴 수 없다(Finding 4).</summary>
        public bool Stopped { get; set; }
    }

    private readonly List<Turn> _turns = [];

    private string? _input;
    private bool _streaming;
    private CancellationTokenSource? _cts;

    private ElementReference _logRef;

    /// <summary>글상자를 감싼 칸. jsiniChat.armEnter 가 여기에 Enter 막이를 건다(AiChatPanel 과 같은
    /// 자리 — 글상자 자신에 걸면 다시 그릴 때 갈려 listener 가 사라진다).</summary>
    private ElementReference _inputRef;

    private bool _enterArmed;

    private bool _scrollPending;

    /// <summary>고른 범위가 지금도 목록에 있나. 카테고리가 지워졌는데 값만 남아 있으면 「모든
    /// 카테고리」로 본다 — 고르개도 그렇게 보인다(AiChatPanel.EffectiveRagScope 와 같은 방어).</summary>
    private string? EffectiveScope =>
        string.IsNullOrEmpty(_scope) || !_scopes.Any(s => s.Id == _scope) ? null : _scope;

    protected override async Task OnInitializedAsync()
    {
        // 프리렌더에서는 건너뛴다 — 조회가 두 번 나가는 것을 막는다(DataPage.CanLoad 와 같은 사정).
        if (!RendererInfo.IsInteractive)
        {
            return;
        }

        try
        {
            _scopes = await Api.ScopesAsync();
        }
        catch (Exception ex) when (ex is ApiException or HttpRequestException or TaskCanceledException)
        {
            // 범위를 못 받아도 대화는 된다 — 고르개만 비어 「모든 카테고리」로 고정된다.
        }
    }

    private void OnScopeChanged(ChangeEventArgs e) => _scope = e.Value as string;

    private async Task OnKeyDownAsync(KeyboardEventArgs e)
    {
        if (e.Key is "Enter" or "NumpadEnter" && !e.ShiftKey)
        {
            await SendAsync();
        }
    }

    private void Stop() => _cts?.Cancel();

    private async Task SendAsync()
    {
        if (_streaming || string.IsNullOrWhiteSpace(_input))
        {
            return;
        }

        var question = _input.Trim();
        _input = string.Empty;

        // 지난 turn 들을 대화 문맥으로 함께 보낸다 — 서버는 세션을 안 두므로(AssistantChatRequest
        // 머리말) 매번 전부 다시 실어야 "아까 그거 더 보여줘" 같은 이어지는 질문이 통한다.
        var context = new List<AiChatMessage>(_turns.Count * 2 + 1);
        foreach (var t in _turns)
        {
            context.Add(new AiChatMessage("user", t.Question));
            context.Add(new AiChatMessage("assistant", t.Answer));
        }
        context.Add(new AiChatMessage("user", question));

        var turn = new Turn { Question = question, Streaming = true };
        _turns.Add(turn);

        _streaming = true;
        _scrollPending = true;
        StateHasChanged();

        _cts?.Cancel();
        _cts?.Dispose();
        _cts = new CancellationTokenSource();

        try
        {
            await foreach (var part in Api.StreamAsync(context, EffectiveScope, _cts.Token))
            {
                if (part.Kind == "tool" && part.Notice is { Length: > 0 } notice)
                {
                    turn.ToolNotices.Add(notice);
                }
                else if (part.Text is { Length: > 0 } answer)
                {
                    turn.Answer = answer;
                }

                _scrollPending = true;
                StateHasChanged();
            }
        }
        catch (OperationCanceledException)
        {
            // 중단 단추를 눌렀거나 화면을 떠났다. 이미 답이 좀 왔으면 그걸로 충분하고,
            // 하나도 못 받았으면 「중단했다」고 적는다 — 안 그러면 무응답과 구별이 안 된다.
            if (turn.Answer.Length == 0)
            {
                turn.Stopped = true;
            }
        }
        catch (Exception ex)
        {
            turn.Answer = $"답을 받지 못했습니다 — {ex.Message}";
        }
        finally
        {
            turn.Streaming = false;
            _streaming = false;
            _scrollPending = true;
            StateHasChanged();
        }
    }

    /// <summary>답 글을 「보통 글」과 「요청글 번호 링크」 조각으로 가른다. 서버가 HTML 을 안 주므로
    /// (답은 평문이다) 화면이 직접 <c>#1042</c> 을 찾아 <c>/helpdesk/request/detail/1042</c> 으로
    /// 잇는다 — 브리프의 경로(<c>/helpdesk/requests/{id}</c>)는 실제 화면 라우트와 다르다(RequestDetail.razor
    /// 의 <c>@page</c> 가 정본이다). 안 맞는 경로로 이으면 눌러도 404 라 여기서는 실제 라우트를 쓴다.
    /// <paramref name="text"/> 의 인스턴스 상태를 안 쓰는 순수 함수라 정적으로 두어 시험이 직접 부른다
    /// (JSini.Web.Architecture.Tests 에 InternalsVisibleTo 가 걸려 있다).
    /// <b>렌더링 도중 도는 이터레이터라 여기서 벗어나는 예외는 바로 회로를 끊는다</b> — 그래서
    /// <see cref="int.TryParse(string?, out int)"/> 로만 수를 읽고(<see cref="RequestRefPattern"/>
    /// 이 이미 자릿수를 2~6 으로 묶어 넘칠 일은 없지만, 파싱은 절대 실패해서 밖으로 새면 안 된다).
    /// "#000000" 처럼 자릿수만 맞고 값이 0 인 것은 실제 글 번호가 아니므로 링크 없이 글자만 둔다.</summary>
    internal static IEnumerable<(string Text, int? RequestId)> AnswerSegments(string text)
    {
        var lastIndex = 0;

        foreach (Match m in RequestRefPattern.Matches(text))
        {
            if (m.Index > lastIndex)
            {
                yield return (text[lastIndex..m.Index], null);
            }

            yield return int.TryParse(m.Groups[1].Value, out var id) && id > 0
                ? (m.Value, id)
                : (m.Value, null);

            lastIndex = m.Index + m.Length;
        }

        if (lastIndex < text.Length)
        {
            yield return (text[lastIndex..], null);
        }
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        // Enter 막이는 한 번만 건다 — 글상자가 그려진 뒤여야 한다(AiChatPanel 과 같은 자리).
        if (!_enterArmed)
        {
            _enterArmed = true;

            try
            {
                await Js.InvokeVoidAsync("jsiniChat.armEnter", _inputRef);
            }
            catch (JSException)
            {
                // 못 걸어도 보내기 단추는 그대로 된다 — Enter 가 줄을 함께 바꿀 뿐이다.
            }
        }

        if (!_scrollPending)
        {
            return;
        }
        _scrollPending = false;

        try
        {
            await Js.InvokeVoidAsync("jsiniChat.toBottom", _logRef);
        }
        catch (JSException)
        {
            // 스크롤은 곁일이다. 못 해도 대화는 된다.
        }
    }

    public ValueTask DisposeAsync()
    {
        _cts?.Cancel();
        _cts?.Dispose();
        return ValueTask.CompletedTask;
    }
}
