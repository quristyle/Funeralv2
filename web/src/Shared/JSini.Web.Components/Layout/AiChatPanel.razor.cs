using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.JSInterop;
using JSini.Web.Http;

namespace JSini.Web.Components.Layout;

public partial class AiChatPanel
{
    [Inject] private AiChatClient Chat { get; set; } = default!;

    private readonly List<AiChatMessage> _messages = [];

    /// <summary>서버가 답과 갈라 보낸 안내. 대화 문맥에는 넣지 않는다.</summary>
    private readonly List<string> _notices = [];

    /// <summary>내 대화 목록. 최근에 말이 오간 순이다.</summary>
    private IReadOnlyList<AiChatSession> _sessions = [];

    /// <summary>
    /// 지금 보고 있는 대화. <b><c>null</c> 이면 아직 못 만든 것</b>이다 —
    /// 서버에 못 닿았을 때가 그렇고, 그때도 대화는 된다(안 담길 뿐이다).
    /// </summary>
    private string? _sessionId;

    /// <summary>대화를 지우기 전에 묻는 창.</summary>
    private ConfirmDialog? _confirm;

    /// <summary>이름 바꾸기 칸이 열려 있나.</summary>
    private bool _renaming;

    private string? _renameText;

    private string? _input;
    private bool _streaming;

    /// <summary>배지에 쓸 글. 비면 배지를 접는다(정보를 못 받았을 때).</summary>
    private string? _who;

    /// <summary>마우스를 올렸을 때의 설명. 기본값인지 실제 답한 것인지를 가른다.</summary>
    private string? _whoHint;

    /// <summary><b>실제로 답한</b> 공급자인가. 기본값만 아는 상태와 점 색으로 가른다.</summary>
    private bool _whoLive;

    private ElementReference _logRef;

    /// <summary>새 글자가 붙을 때마다 맨 아래로 따라 내려갈지. 렌더 뒤에 본다.</summary>
    private bool _scrollPending;

    /// <summary>화면을 떠나면 흐르던 스트림을 끊는다. 안 끊으면 서랍을 닫아도 계속 받는다.</summary>
    private CancellationTokenSource? _cts;

    [Inject] private IJSRuntime Js { get; set; } = default!;

    /// <summary>
    /// 지금 대화의 제목. 고르개와 이름 바꾸기 칸이 쓴다.
    /// </summary>
    private string CurrentTitle =>
        _sessions.FirstOrDefault(s => s.Id == _sessionId)?.Label ?? "새 대화";

    /// <summary>
    /// 화면을 열면 <b>보던 대화를 그대로 다시 연다.</b>
    /// </summary>
    /// <remarks>
    /// <para>
    /// 이것이 「새로고침하면 사라진다」를 고치는 자리다. 어느 것을 열지는
    /// 둘로 정한다 — <b>브라우저가 적어 둔 마지막 대화</b>가 있으면 그것,
    /// 없으면 <b>가장 최근에 말이 오간 것</b>이다.
    /// </para>
    /// <para>
    /// 적어 두는 쪽이 필요한 까닭은, 지난 대화를 골라 읽다가 새로고침하면
    /// 목록 맨 앞(= 다른 대화)으로 튀기 때문이다. <b>여는 것만으로는
    /// <c>updated_at</c> 이 바뀌지 않으므로</b> 그 값으로는 알 수 없다.
    /// </para>
    /// <para>
    /// 서버에 못 닿아도 대화는 된다 — 목록이 비고 담기지 않을 뿐이다.
    /// 여기서 막으면 AI 가 통째로 멎는다.
    /// </para>
    /// </remarks>
    protected override async Task OnInitializedAsync()
    {
        // **프리렌더에서는 아무것도 하지 않는다.**
        //
        // 포털은 프리렌더를 켜 두어서 이 부품이 두 번 만들어진다 — 정적
        // SSR 로 한 번, 회로가 붙고 또 한 번(`DataPage.CanLoad` 머리말).
        // 그 첫 번째에는 브라우저가 없어 JS 를 부를 수 없고, 부르면
        // **화면이 통째로 500 으로 죽는다**(실제로 밟았다). 조회도 두 벌
        // 나가므로 어차피 건너뛰는 편이 맞다.
        if (!RendererInfo.IsInteractive)
        {
            return;
        }

        // 물어보기 전에 보여 줄 값. 못 받아도 대화는 그대로 된다 — 배지만 안 뜬다.
        if (await Chat.GetDefaultAsync() is { } who)
        {
            _who = who.Configured
                ? $"기본 AI: {who.Label}"
                : $"기본 AI: {who.Label} (설정 미완)";

            _whoHint = who.Configured
                ? "설정된 기본 AI 입니다. 실제로 답한 AI 는 첫 답변 뒤에 표시됩니다."
                : "키가 채워지지 않아 이 AI 로는 부를 수 없습니다. "
                    + "물어보면 자동으로 다른 AI 가 답합니다.";
        }

        await ReloadSessionsAsync();

        var remembered = await RememberedAsync();

        var open = _sessions.FirstOrDefault(s => s.Id == remembered)
                ?? _sessions.FirstOrDefault();

        if (open is not null)
        {
            await OpenAsync(open.Id);
        }
    }

    private async Task ReloadSessionsAsync()
    {
        _sessions = await Chat.ListSessionsAsync();
    }

    /// <summary>
    /// 그 대화를 연다. 고르개가 부르고, 화면을 처음 열 때도 부른다.
    /// </summary>
    private async Task OpenAsync(string? sessionId)
    {
        if (string.IsNullOrEmpty(sessionId) || sessionId == _sessionId)
        {
            return;
        }

        // 흐르던 답은 끊는다. 안 끊으면 **지난 대화를 열어 둔 화면에 다른
        // 대화의 답이 붙는다.**
        _cts?.Cancel();
        _streaming = false;

        _sessionId = sessionId;
        _renaming = false;
        _notices.Clear();

        _messages.Clear();
        _messages.AddRange(await Chat.GetSessionAsync(sessionId));

        await RememberAsync(sessionId);

        _scrollPending = true;
        StateHasChanged();
    }

    /// <summary>
    /// 새 대화를 연다. <b>제목은 첫 질문이 붙인다</b> — 여기서 묻지 않는다.
    /// </summary>
    private async Task NewAsync()
    {
        if (await Chat.CreateSessionAsync() is not { } created)
        {
            _notices.Add("새 대화를 만들지 못했습니다. 잠시 뒤에 다시 해 보세요.");
            return;
        }

        _cts?.Cancel();
        _streaming = false;

        _messages.Clear();
        _notices.Clear();
        _renaming = false;

        _sessionId = created.Id;
        await RememberAsync(created.Id);
        await ReloadSessionsAsync();
    }

    private void BeginRename()
    {
        if (_sessionId is null) return;

        _renameText = _sessions.FirstOrDefault(s => s.Id == _sessionId)?.Title;
        _renaming = true;
    }

    private async Task SaveRenameAsync()
    {
        if (_sessionId is null)
        {
            _renaming = false;
            return;
        }

        await Chat.RenameSessionAsync(_sessionId, _renameText);
        _renaming = false;
        await ReloadSessionsAsync();
    }

    /// <summary>
    /// 묻고 지운다. <b>되살릴 수 없다</b> — 물어본 말과 받은 답이 함께 사라진다.
    /// </summary>
    private async Task ConfirmDeleteAsync()
    {
        if (_sessionId is null || _confirm is null) return;

        // 무엇이 사라지는지 적는다. 「정말 지웁니까」만으로는 어느 대화를
        // 고른 채였는지 확인할 수 없다(ConfirmDialog 머리말).
        if (!await _confirm.AskAsync(
                $"「{CurrentTitle}」 대화를 지웁니다. 주고받은 말이 함께 사라집니다.",
                title: "대화 지우기"))
        {
            return;
        }

        await DeleteAsync();
    }

    private async Task DeleteAsync()
    {
        if (_sessionId is not { } id) return;

        await Chat.DeleteSessionAsync(id);

        _sessionId = null;
        _messages.Clear();
        _notices.Clear();
        _renaming = false;

        await ReloadSessionsAsync();

        // 지우고 나면 **그다음 대화를 연다.** 빈 화면으로 두면 방금 지운 것이
        // 아니라 전부 사라진 것처럼 보인다.
        if (_sessions.FirstOrDefault() is { } next)
        {
            await OpenAsync(next.Id);
        }
        else
        {
            await RememberAsync(null);
        }
    }

    private async Task OnKeyDownAsync(KeyboardEventArgs e)
    {
        // Shift+Enter 는 줄바꿈 자리라 비워 둔다(지금은 한 줄 입력이라 아무 일도
        // 없지만, 여러 줄로 바꿀 때 여기만 고치면 된다).
        if (e.Key is "Enter" or "NumpadEnter" && !e.ShiftKey)
        {
            await SendAsync();
        }
    }

    private async Task SendAsync()
    {
        if (_streaming || string.IsNullOrWhiteSpace(_input))
        {
            return;
        }

        // 아직 담을 자리가 없으면 먼저 만든다. **만들기에 실패해도 보낸다** —
        // 대화가 안 담길 뿐이고, 여기서 막으면 서버가 흔들릴 때 AI 가 통째로 멎는다.
        if (_sessionId is null)
        {
            await NewAsync();
        }

        var text = _input.Trim();
        _input = string.Empty;

        _messages.Add(new AiChatMessage("user", text));

        // 답이 들어갈 빈 자리를 먼저 만든다. 조각이 올 때마다 이 칸만 갈아 끼운다.
        _messages.Add(new AiChatMessage("assistant", string.Empty));
        var slot = _messages.Count - 1;

        _streaming = true;
        _scrollPending = true;
        StateHasChanged();

        _cts?.Cancel();
        _cts?.Dispose();
        _cts = new CancellationTokenSource();

        // **이 턴이 어느 대화의 것인지 붙잡아 둔다.** 답을 받는 동안 사람이
        // 다른 대화를 고를 수 있는데, 그때 `_sessionId` 를 그대로 읽으면
        // 방금 연 대화에 남의 턴이 담긴다.
        var turnSession = _sessionId;

        try
        {
            // 마지막 빈 칸은 보내지 않는다 — 서버 문맥에 빈 답이 섞인다.
            var context = _messages.Take(_messages.Count - 1).ToList();

            await foreach (var part in Chat.StreamAsync(context, turnSession, _cts.Token))
            {
                if (part.IsUsedMarker && part.Notice is { Length: > 0 } used)
                {
                    // 안내 목록에 쌓지 않는다. 매 턴 오므로 쌓으면 같은 줄이 도배된다.
                    _who = used;
                    _whoHint = "이번 답을 만든 AI 입니다.";
                    _whoLive = true;
                }
                else if (part.Notice is { Length: > 0 } notice)
                {
                    _notices.Add(notice);
                }
                else if (part.Text is { Length: > 0 } piece)
                {
                    _messages[slot] = _messages[slot] with
                    {
                        Content = _messages[slot].Content + piece,
                    };
                }

                _scrollPending = true;
                StateHasChanged();
            }
        }
        catch (OperationCanceledException)
        {
            // 서랍을 닫았거나 화면을 떠났다. 알릴 것이 없다.
        }
        catch (Exception ex)
        {
            // 오류를 **답에 이어 붙이지 않는다.** 붙이면 다음 턴 문맥에 실려
            // 나가서 AI 가 그 문장을 자기가 한 말로 읽는다.
            _notices.Add($"답을 받지 못했습니다 — {ex.Message}");
        }
        finally
        {
            // 한 글자도 못 받았으면 빈 말풍선이 남는다. 지운다.
            if (_messages.Count > slot && _messages[slot].Content.Length == 0)
            {
                _messages.RemoveAt(slot);
            }

            _streaming = false;
            _scrollPending = true;

            // 제목은 **첫 질문이 들어올 때 서버가 붙인다.** 목록을 다시 읽어야
            // 고르개의 「새 대화」가 그 제목으로 바뀐다. 차례도 이때 앞으로 온다.
            if (turnSession is not null && turnSession == _sessionId)
            {
                await ReloadSessionsAsync();
            }

            StateHasChanged();
        }
    }

    /// <summary>
    /// 마지막으로 열어 본 대화를 브라우저에 적어 둔다.
    /// </summary>
    /// <remarks>
    /// <b>사람의 것이 아니라 이 브라우저의 것</b>이라 서버에 두지 않는다 —
    /// 책상에서 보던 대화와 휴대폰에서 보던 대화가 다를 수 있고, 그것이
    /// 자연스럽다. 고정 탭·공지 닫힘 표시와 같은 갈래다.
    ///
    /// <para>못 해도 조용하다. 그때는 목록 맨 앞엣것이 열린다.</para>
    /// </remarks>
    private async Task RememberAsync(string? sessionId)
    {
        try
        {
            await Js.InvokeVoidAsync("jsiniChat.remember", sessionId);
        }
        catch (JSException)
        {
            // 저장소를 막아 둔 브라우저다. 다음에는 맨 앞엣것이 열린다.
        }
    }

    private async Task<string?> RememberedAsync()
    {
        try
        {
            return await Js.InvokeAsync<string?>("jsiniChat.remembered");
        }
        catch (JSException)
        {
            return null;
        }
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
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

    public void Dispose()
    {
        _cts?.Cancel();
        _cts?.Dispose();
    }
}
