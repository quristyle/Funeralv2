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

    [Inject] private AssistantProjectClient Projects { get; set; } = default!;

    [Inject] private IJSRuntime Js { get; set; } = default!;

    /// <summary>요청글 번호(<c>#1042</c>)를 되짚는다. <b>자릿수가 아니라 문맥으로 가른다</b> —
    /// 자릿수 하한을 두면(2자리 이상) "매출 순위 #1" 은 막아도 실제로 한 자리인 글 번호(지금
    /// 운영 헬프데스크의 13건 중 9건이 한 자리다)가 통째로 안 걸린다. 그래서 자릿수 대신
    /// AIAgentServer 의 <c>PortalToolRunner</c> 와 <c>AssistantAgent</c> 가 실제로 쓰는 낱말을 그대로
    /// 요구한다 — <c>PortalToolRunner.RenderRequest</c>(목록의 매 줄) · <c>GetRequestAsync</c> 의
    /// 머리글과 못 찾음 메시지가 **모두** "요청 글 #{id}" 한 모양을 쓰고, <c>AssistantAgent.SystemPrompt</c>
    /// 역시 "요청글 번호"라고 못박아 모델이 그 낱말로 근거를 대게 유도한다(<c>답의 근거(요청글 번호,
    /// 파일 이름 등)를 반드시 밝히십시오</c>). "요청글"(붙여쓰기)·"요청 글"(띄어쓰기) 둘 다 받는다 —
    /// 코드 안에서도 두 표기가 섞여 있다.
    /// <b>도구가 글 번호를 적는 자리 중 하나라도 이 낱말을 빠뜨리거나 바꾸면 이 링크가 아무 오류
    /// 없이 조용히 사라진다</b> — 바꿀 때는 이 정규식도 함께 고친다. 자릿수 상한(1~6, <c>(?!\d)</c>)
    /// 은 그래도 남겨 둔다 — 문맥이 걸려도 11자리 같은 수가 <c>int.Parse</c> 를 넘기면 안 되므로
    /// 두 번째 방어로 둔다.</summary>
    private static readonly Regex RequestRefPattern = new(
        @"요청\s?글\s*#(\d{1,6})(?!\d)", RegexOptions.Compiled);

    /// <summary>고를 수 있는 프로젝트 — <b>내가 들어가 있는 것</b>. 못 받으면 빈 목록이고,
    /// 그때는 범위를 못 좁혀 사내 자료 전체에서 찾는다.</summary>
    private IReadOnlyList<AssistantProject> _projects = [];

    /// <summary>켜 둔 프로젝트 번호. 처음에는 <see cref="_projects"/> 전부가 켜져 있다.</summary>
    private IEnumerable<int> _picked = [];

    /// <summary>지금 고른 프로젝트에 매인 카테고리 열쇠 전부. 프로젝트 둘이 같은 카테고리를
    /// 매고 있을 수 있어 겹친 것을 턴다.</summary>
    private IReadOnlyList<string> PickedCategories =>
        [.. _projects
            .Where(p => _picked.Contains(p.PrjRid))
            .SelectMany(p => p.RagCateIds)
            .Distinct(StringComparer.Ordinal)];

    /// <summary>고르개 옆에 적는 한 줄. <b>「고르긴 골랐는데 볼 자료가 없다」를 드러내는 것이
    /// 일이다</b> — 프로젝트에 카테고리를 안 매어 두면 범위가 비고, 그러면 서버가 사내 문서
    /// 검색을 거절한다(화면에는 ⚠️ 한 줄로 온다). 그 전에 여기서 보여야 한다.</summary>
    private string? ScopeHint
    {
        get
        {
            if (_projects.Count == 0) return null;
            if (!_picked.Any()) return "고른 프로젝트가 없어 사내 자료 전체에서 찾습니다.";

            return PickedCategories.Count == 0
                ? "고른 프로젝트에 매인 사내 자료가 없습니다 — 프로젝트 목록에서 RAG 카테고리를 매어 주세요."
                : null;
        }
    }

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

    protected override async Task OnInitializedAsync()
    {
        // 프리렌더에서는 건너뛴다 — 조회가 두 번 나가는 것을 막는다(DataPage.CanLoad 와 같은 사정).
        if (!RendererInfo.IsInteractive)
        {
            return;
        }

        try
        {
            _projects = await Projects.MineAsync();

            // 들어와 있는 것은 **전부 켠 채로** 시작한다. 고르러 들어온 사람이 아니라
            // 물어보러 들어온 사람이라, 아무것도 안 고른 빈 칸을 들이밀면 제 자료를
            // 놔두고 사내 자료 전체를 뒤지게 된다.
            _picked = [.. _projects.Select(p => p.PrjRid)];
        }
        catch (Exception ex) when (ex is ApiException or HttpRequestException or TaskCanceledException)
        {
            // 프로젝트를 못 받아도 대화는 된다 — 고르개만 비어 사내 자료 전체에서 찾는다.
        }
    }

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

        // 고른 프로젝트 이름을 함께 보낸다 — 서버가 모델에게 「이 범위로 좁혀져 있다」고
        // 이를 때 카테고리 열쇠 대신 사람이 아는 이름이 나간다.
        var picked = _projects.Where(p => _picked.Contains(p.PrjRid)).ToList();
        var label = picked.Count == 0 ? null : string.Join(" · ", picked.Select(p => p.Label));

        // 고른 것이 없으면 **아예 안 보낸다**(사내 자료 전체). 골랐는데 매인 카테고리가
        // 없으면 **빈 목록을 보낸다**(찾을 문서가 없다). 서버가 그 둘을 가른다.
        var categories = picked.Count == 0 ? null : PickedCategories;

        try
        {
            await foreach (var part in Api.StreamAsync(context, categories, label, _cts.Token))
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
    /// 이 이미 자릿수를 1~6 으로 묶어 넘칠 일은 없지만, 파싱은 절대 실패해서 밖으로 새면 안 된다).
    /// "요청글 #000000" 처럼 문맥·자릿수가 다 맞아도 값이 0 인 것은 실제 글 번호가 아니므로(DB
    /// 일련번호는 1부터다) 링크 없이 글자만 둔다.</summary>
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
