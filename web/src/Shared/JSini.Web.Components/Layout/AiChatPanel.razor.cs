using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.JSInterop;
using JSini.Web.Http;

namespace JSini.Web.Components.Layout;

public partial class AiChatPanel
{
    [Parameter] public bool IsDrawer { get; set; }
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

    /// <summary>대화 목록(히스토리) 패널 열림 여부.</summary>
    private bool _historyOpen;

    /// <summary>목록에서 「…」 메뉴를 펼친 대화. 한 번에 하나만 펼친다.</summary>
    private string? _menuSessionId;

    /// <summary>복사 완료 피드백용 인덱스.</summary>
    private int? _copiedIndex;

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

    /// <summary>
    /// 글상자를 감싼 칸. JS 가 여기에 Enter 막이를 건다(<c>jsiniChat.armEnter</c>).
    /// </summary>
    /// <remarks>
    /// 글상자(<c>textarea</c>)가 아니라 <b>바깥 칸</b>을 넘긴다 — 글상자는
    /// DevExpress 가 그려서 다시 그릴 때 갈릴 수 있고, 그때 거기 건 listener 는
    /// 함께 사라진다. 바깥 칸은 이 부품이 사는 동안 그대로다.
    /// </remarks>
    private ElementReference _inputRef;

    /// <summary>Enter 막이를 이미 걸었나. 렌더마다 다시 걸지 않으려고 둔다.</summary>
    private bool _enterArmed;

    /// <summary>새 글자가 붙을 때마다 맨 아래로 따라 내려갈지. 렌더 뒤에 본다.</summary>
    private bool _scrollPending;

    /// <summary>화면을 떠나면 흐르던 스트림을 끊는다. 안 끊으면 서랍을 닫아도 계속 받는다.</summary>
    private CancellationTokenSource? _cts;

    // ── 말로 묻기 ───────────────────────────────────────────
    //
    // 두 가지가 한 벌로 산다. **받아쓰기**는 말한 것을 입력칸에 채워 주고
    // 사람이 확인한 뒤 보낸다. **음성 모드**는 그 확인을 건너뛰고 바로 묻고,
    // 답을 소리로 읽어 준다.
    //
    // 둘이 같은 녹음 부품(`chat-voice.js`)을 쓰고, 가르는 것은 `_voiceMode`
    // 하나다 — 복사해 두면 「받아쓰기는 되는데 음성 모드에서는 말끝이
    // 잘린다」 같은 어긋남이 생긴다.

    /// <summary>녹음 부품. 쓸 때 한 번만 불러온다.</summary>
    private IJSObjectReference? _voiceJs;

    /// <summary>읽어주기 부품. 프로젝트관리 지시 상세와 **같은 것**이다.</summary>
    private IJSObjectReference? _speechJs;

    /// <summary>JS 가 되부를 자리(자동 종료·읽기 상태).</summary>
    private DotNetObjectReference<AiChatPanel>? _selfRef;

    /// <summary>
    /// 이 브라우저에서 녹음이 되나. <b><c>null</c> 은 아직 안 물어본 것이다</b> —
    /// 물어보는 일 자체가 JS 왕복이라 처음 쓸 때까지 미룬다.
    /// </summary>
    private bool? _voiceSupported;

    /// <summary>음성 모드(전체화면)가 열려 있나.</summary>
    private bool _voiceMode;

    /// <summary>지금 마이크가 열려 있나.</summary>
    private bool _listening;

    /// <summary>녹음을 서버에 보내고 글을 기다리는 중인가.</summary>
    private bool _transcribing;

    /// <summary>읽어주는 중인가. JS 가 알려 준다(<see cref="OnSpeechStateChanged"/>).</summary>
    private bool _speaking;

    /// <summary>음성 모드 자막 — 마지막으로 알아들은 말.</summary>
    private string? _heard;

    /// <summary>음성 모드의 안내 한 줄. 권한 거절·못 알아들음 따위.</summary>
    private string? _voiceNotice;

    /// <summary>
    /// 소리 크기를 적어 넣을 칸. <b>JS 가 여기에 직접 쓴다</b> —
    /// 회로로 올리면 말하는 동안 초당 수십 번 서버가 깨어난다.
    /// </summary>
    private ElementReference _orbRef;

    /// <summary>답을 소리로 읽어 줄지. 음성 모드에서 끄면 자막만 남는다.</summary>
    private bool _speakReplies = true;

    /// <summary>
    /// 조용해지면 스스로 끊을지(VAD).
    /// </summary>
    /// <remarks>
    /// <b>기본은 켠다</b> — 그래야 손을 안 댄다. 다만 사무실에서는 옆자리
    /// 말소리에 끊기므로 끄는 자리를 남겨 둔다. 끄면 사람이 눌러서 끊는다.
    /// </remarks>
    private bool _autoEnd = true;

    /// <summary>
    /// 읽어주기가 끝나면 다시 들을까. 음성 모드가 한 턴을 마칠 때 켜 둔다.
    /// </summary>
    /// <remarks>
    /// <b>읽기가 끝난 뒤에 열어야 한다.</b> 읽는 중에 마이크를 열면 제가 읽는
    /// 소리를 제가 받아쓴다 — 울림 지우기를 켜 두었어도 기기에 따라 샌다.
    /// </remarks>
    private bool _listenAfterSpeech;

    /// <summary>
    /// 아직 안 읽은 글. <b>문장이 될 때까지 모은다.</b>
    /// </summary>
    /// <remarks>
    /// 답은 글자 단위로 온다. 오는 대로 읽어주기에 넣으면 한 글자짜리 발화가
    /// 줄줄이 생겨서 말이 토막 나 들린다. 그렇다고 다 올 때까지 기다리면
    /// 긴 답에서 몇 초 동안 아무 소리도 안 난다 — 그 둘 사이가 문장이다.
    /// </remarks>
    private readonly System.Text.StringBuilder _speakBuffer = new();

    /// <summary>이번 턴의 답을 읽어 주는 중인가. 스트림이 끝나면 닫는다.</summary>
    private bool _speakStreamOpen;

    [Inject] private IJSRuntime Js { get; set; } = default!;

    [Inject] private PortalBoot Boot { get; set; } = default!;

    /// <summary>
    /// 사내 자료(RAG)를 먼저 뒤질지. <b>기본은 켬</b>이고 기기에 남는다
    /// (<see cref="PortalBoot.AiRagOffKey"/>).
    /// </summary>
    private bool _preferRag = true;

    /// <summary>
    /// 고르개를 눌렀을 때. 저장은 <see cref="PortalBoot"/> 가 한다.
    /// </summary>
    /// <remarks>
    /// <b>지금 쓰고 있는 대화에 곧바로 먹는다</b> — 다음에 보내는 질문부터
    /// 적용되고, 이미 받은 답은 그대로 둔다. 되돌려 다시 묻는 길을 주지 않는
    /// 까닭은 그것이 같은 질문을 한 번 더 치는 것과 다르지 않기 때문이다.
    /// </remarks>
    private async Task OnPreferRagChangedAsync(ChangeEventArgs e)
    {
        _preferRag = e.Value is bool b ? b : !_preferRag;
        await Boot.SetAiPreferRagAsync(_preferRag);
    }

    /// <summary>
    /// 지금 대화의 제목. 고르개와 이름 바꾸기 칸이 쓴다.
    /// </summary>
    private string CurrentTitle =>
        _sessions.FirstOrDefault(s => s.Id == _sessionId)?.Label ?? "새 대화";

    private void ToggleHistory()
    {
        _historyOpen = !_historyOpen;
        _menuSessionId = null;
    }

    private void CloseHistory()
    {
        _historyOpen = false;
        _menuSessionId = null;
    }

    private void ToggleSessionMenu(string sessionId) =>
        _menuSessionId = _menuSessionId == sessionId ? null : sessionId;

    private void CloseSessionMenu() => _menuSessionId = null;

    private async Task SelectSessionAsync(string sessionId)
    {
        _historyOpen = false;
        await OpenAsync(sessionId);
    }

    private async Task StartNewChatAsync()
    {
        _historyOpen = false;
        await NewAsync();
    }

    private async Task AskPromptAsync(string prompt)
    {
        if (_streaming || string.IsNullOrWhiteSpace(prompt)) return;
        _input = prompt;
        await SendAsync();
    }

    /// <summary>
    /// 답 만들기를 멈춘다. <b>읽어 주던 것도 함께 멈춘다</b> — 글은 멎었는데
    /// 소리만 계속 나면 사람은 멈추기가 안 들은 것으로 읽는다.
    /// </summary>
    private async Task StopGeneratingAsync()
    {
        _cts?.Cancel();
        _streaming = false;
        _listenAfterSpeech = false;

        await StopSpeakingAsync();
        StateHasChanged();
    }

    private async Task CopyMessageAsync(string content, int index)
    {
        if (string.IsNullOrWhiteSpace(content)) return;
        var ok = await Js.InvokeAsync<bool>("jsiniChat.copyText", content);
        if (ok)
        {
            _copiedIndex = index;
            StateHasChanged();
            _ = Task.Delay(2000).ContinueWith(_ =>
            {
                _copiedIndex = null;
                _ = InvokeAsync(StateHasChanged);
            });
        }
    }

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
    /// 목록의 「…」 메뉴에서 고른 대화를 묻고 지운다. <b>되살릴 수 없다</b> —
    /// 물어본 말과 받은 답이 함께 사라진다.
    /// </summary>
    private async Task ConfirmDeleteAsync(AiChatSession session)
    {
        _menuSessionId = null;
        if (_confirm is null) return;
        if (_streaming && session.Id == _sessionId) return;

        // 무엇이 사라지는지 적는다. 「정말 지웁니까」만으로는 어느 대화를
        // 고른 채였는지 확인할 수 없다(ConfirmDialog 머리말).
        if (!await _confirm.AskAsync(
                $"「{session.Label}」 대화를 지웁니다. 주고받은 말이 함께 사라집니다.",
                title: "대화 지우기"))
        {
            return;
        }

        await DeleteAsync(session.Id);
    }

    private async Task DeleteAsync(string id)
    {
        await Chat.DeleteSessionAsync(id);

        // 보고 있지 않은 대화를 지웠으면 목록만 고친다.
        if (id != _sessionId)
        {
            await ReloadSessionsAsync();
            return;
        }

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

    /// <summary>
    /// 글상자에서 Enter 를 받는다. <b>Enter 는 보내기, Shift+Enter 는 줄바꿈</b>이다.
    /// </summary>
    /// <remarks>
    /// <para>
    /// 줄바꿈 쪽은 여기서 아무것도 하지 않는 것이 곧 하는 일이다 —
    /// <c>textarea</c> 의 기본 동작이 줄바꿈이라 지나보내면 줄이 바뀐다.
    /// </para>
    /// <para>
    /// 보내는 쪽은 반대로 그 기본 동작을 꺼야 하는데, 끄는 것은 누르는 그
    /// 순간에 정해야 해서 여기서는 못 한다(Blazor 의 <c>preventDefault</c> 는
    /// 그릴 때 박히는 값이다). 그 한 가지만 JS 가 한다 —
    /// <c>jsiniChat.armEnter</c> 의 조건을 <b>이 조건과 같게</b> 두어야 한다.
    /// </para>
    /// <para>
    /// 한글을 조합하는 중의 Enter 는 글자를 앉히는 Enter 라서 걸러진다 —
    /// 그때 브라우저가 주는 <c>Key</c> 는 <c>"Enter"</c> 가 아니라
    /// <c>"Process"</c> 다.
    /// </para>
    /// </remarks>
    private async Task OnKeyDownAsync(KeyboardEventArgs e)
    {
        if (e.Key is "Enter" or "NumpadEnter" && !e.ShiftKey)
        {
            await SendAsync();
        }
    }

    /// <param name="speakAloud">
    /// 답을 소리로 읽어 줄지. <b>음성 모드에서만 참</b>이다 — 글로 물었는데
    /// 소리가 나면 놀란다.
    /// </param>
    private async Task SendAsync(bool speakAloud = false)
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

        // 읽어주기는 **답이 오기 전에** 열어 둔다. 첫 문장이 왔을 때 그제서야
        // 부품을 불러오면 그 한 번의 왕복만큼 소리가 늦는다.
        if (speakAloud)
        {
            await BeginSpeakAsync();
        }

        try
        {
            // 마지막 빈 칸은 보내지 않는다 — 서버 문맥에 빈 답이 섞인다.
            var context = _messages.Take(_messages.Count - 1).ToList();

            await foreach (var part in Chat.StreamAsync(
                context, turnSession, _preferRag, _cts.Token))
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

                    // 문장이 될 때까지 모았다가 읽어주기에 넘긴다.
                    await FeedSpeakAsync(piece);
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

            // ── 음성 모드의 한 턴을 닫는다 ──────────────────────
            //
            // **다음 차례를 여기서 바로 열지 않는다.** 읽어 주는 중에 마이크를
            // 열면 제가 읽는 소리를 제가 받아쓴다. 읽기가 끝났다는 것은 JS 가
            // 알려 주므로(`OnSpeechStateChanged`) 그때까지 표시만 남긴다.
            if (speakAloud)
            {
                _listenAfterSpeech = _voiceMode;
                await EndSpeakAsync();

                // **읽어주기가 아예 안 돌았을 수 있다** — 그 브라우저에 TTS 가
                // 없거나 목소리를 못 잡은 경우다. 그러면 「읽기가 끝났다」는
                // 소식이 영영 안 오고 음성 모드가 그 자리에 멎는다.
                if (_voiceMode && _listenAfterSpeech && !_speaking)
                {
                    _listenAfterSpeech = false;
                    await StartListeningAsync(auto: _autoEnd);
                }
            }
            else if (_voiceMode)
            {
                // 읽어주기를 꺼 둔 음성 모드. 기다릴 것이 없으니 바로 듣는다.
                await StartListeningAsync(auto: _autoEnd);
            }

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
        // 고른 적이 있으면 그것을 따른다. **회로가 붙은 뒤에만 읽는다** —
        // 프리렌더에는 브라우저가 없다. 왕복은 안 는다(`PortalBoot` 가 레이아웃의
        // 한 번 읽기에 실어 온다).
        if (firstRender && RendererInfo.IsInteractive)
        {
            try
            {
                var boot = await Boot.ReadAsync();
                if (boot.AiPreferRag != _preferRag)
                {
                    _preferRag = boot.AiPreferRag;
                    StateHasChanged();
                }
            }
            catch (Exception ex) when (ex is JSException or InvalidOperationException)
            {
                // 못 읽으면 기본값(켬)으로 둔다. 대화를 막을 일이 아니다.
            }
        }

        // Enter 막이는 **한 번만** 건다. 글상자가 그려진 뒤여야 하므로
        // 여기서 걸고, JS 쪽도 같은 칸에 두 번 걸리지 않게 표시를 남긴다.
        if (!_enterArmed)
        {
            _enterArmed = true;

            try
            {
                await Js.InvokeVoidAsync("jsiniChat.armEnter", _inputRef);
            }
            catch (JSException)
            {
                // 못 걸어도 보내기는 된다 — Enter 가 줄을 함께 바꿀 뿐이다.
                // 여기서 막으면 대화가 통째로 멎는다.
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

    // ════════════════════════════════════════════════════════
    //  말로 묻기
    // ════════════════════════════════════════════════════════

    // ── 음성 모드가 그리는 것 ───────────────────────────────
    //
    // 네 상태를 **하나의 차례로** 가린다. 조건을 markup 에 흩어 두면
    // 오브의 그림·글·애니메이션이 서로 다른 상태를 가리키는 날이 온다.

    /// <summary>오브에 붙는 상태 클래스. CSS 가 이것으로 애니메이션을 가른다.</summary>
    private string VoiceStageClass =>
        _transcribing ? "is-thinking"
        : _streaming ? "is-thinking"
        : _speaking ? "is-speaking"
        : _listening ? "is-listening"
        : "is-idle";

    private string VoiceOrbIcon =>
        _transcribing || _streaming ? "jsini-icon-hourglass"
        : _speaking ? "jsini-icon-volume"
        : "jsini-icon-microphone";

    private string VoiceStateText =>
        _transcribing ? "말씀을 옮기고 있습니다…"
        : _streaming ? "답변을 만들고 있습니다…"
        : _speaking ? "답변을 읽고 있습니다"
        : _listening ? "듣고 있습니다…"
        : "눌러서 말하기";

    /// <summary>
    /// 오브를 눌렀을 때 무슨 일이 날지 미리 적는다 — <b>누르기 전에 알아야
    /// 하는 자리</b>다. 읽어 주는 중에 누르면 말이 끊긴다는 것을 모르면
    /// 사람은 그 단추를 안 누른다.
    /// </summary>
    private string VoiceOrbHint =>
        _transcribing ? "옮기는 중입니다"
        // **답이 흐르는 동안은 잠긴다.** 안 잠그면 여기서 마이크가 열리고,
        // 곧 시작될 읽어주기와 겹쳐 **제가 읽는 소리를 제가 받아쓴다.**
        // 멈추는 길은 아래 「그만」 단추다.
        : _streaming ? "답변을 만드는 중입니다 — 멈추려면 아래 「그만」"
        : _speaking ? "읽기를 멈추고 이어서 말합니다"
        : _listening ? "말하기를 끝내고 질문합니다"
        : "눌러서 말하기";

    /// <summary>
    /// 자막에 쓸 <b>마지막 AI 답</b>. 흐르는 중에는 지금까지 온 만큼 보인다.
    /// </summary>
    private string? LastReply =>
        _messages.Count > 0 && _messages[^1].Role != "user"
            ? _messages[^1].Content
            : null;

    /// <summary>
    /// 녹음 부품을 불러온다. <b>처음 쓸 때까지 미룬다</b> — 대화를 글로만 쓰는
    /// 사람에게 쓰지도 않을 부품을 내려받게 할 까닭이 없다.
    /// </summary>
    private async Task<IJSObjectReference?> VoiceJsAsync()
    {
        if (_voiceJs is not null) return _voiceJs;

        try
        {
            _voiceJs = await Js.InvokeAsync<IJSObjectReference>(
                "import", "./_content/JSini.Web.Components/js/chat-voice.js");

            _voiceSupported = await _voiceJs.InvokeAsync<bool>("isSupported");
            return _voiceJs;
        }
        catch (Exception ex) when (ex is JSException or JSDisconnectedException
            or InvalidOperationException or TaskCanceledException)
        {
            _voiceSupported = false;
            return null;
        }
    }

    private async Task<IJSObjectReference?> SpeechJsAsync()
    {
        if (_speechJs is not null) return _speechJs;

        try
        {
            // 프로젝트관리 지시 상세와 **같은 것**이다(`speech.js` 머리말).
            _speechJs = await Js.InvokeAsync<IJSObjectReference>(
                "import", "./_content/JSini.Web.Components/js/speech.js");

            return _speechJs;
        }
        catch (Exception ex) when (ex is JSException or JSDisconnectedException
            or InvalidOperationException or TaskCanceledException)
        {
            return null;
        }
    }

    private DotNetObjectReference<AiChatPanel> SelfRef()
        => _selfRef ??= DotNetObjectReference.Create(this);

    // ── 1단계 · 받아쓰기 ────────────────────────────────────

    /// <summary>
    /// 입력칸 옆 마이크. 누르면 듣고, 다시 누르면 <b>입력칸에 채운다.</b>
    /// </summary>
    /// <remarks>
    /// <b>바로 보내지 않는다.</b> 받아쓰기는 틀릴 수 있고, 틀린 채로 나가면
    /// 사람은 AI 가 엉뚱한 답을 했다고 읽는다. 눈으로 한 번 보고 보내는
    /// 것까지가 이 단추의 일이다 — 그 확인을 건너뛰는 쪽이 음성 모드다.
    /// </remarks>
    private async Task ToggleDictationAsync()
    {
        if (_transcribing) return;

        if (_listening)
        {
            await FinishListeningAsync(sendStraightAway: false);
            return;
        }

        await StartListeningAsync(auto: false);
    }

    // ── 2단계 · 음성 모드 ───────────────────────────────────

    /// <summary>음성 모드를 연다. 열자마자 듣기 시작한다.</summary>
    private async Task OpenVoiceModeAsync()
    {
        if (_voiceMode) return;

        _voiceMode = true;
        _voiceNotice = null;
        _heard = null;
        StateHasChanged();

        await StartListeningAsync(auto: _autoEnd);
    }

    /// <summary>
    /// 음성 모드를 닫는다. <b>대화는 그대로 남는다</b> — 글 화면으로 돌아갈 뿐이다.
    /// </summary>
    private async Task CloseVoiceModeAsync()
    {
        _voiceMode = false;
        _listenAfterSpeech = false;

        await StopListeningAsync(discard: true);
        await StopSpeakingAsync();

        _heard = null;
        _voiceNotice = null;
        StateHasChanged();
    }

    /// <summary>
    /// 가운데 오브를 눌렀을 때. <b>지금 무엇을 하고 있느냐에 따라 다르다.</b>
    /// </summary>
    /// <remarks>
    /// 읽어 주는 중이면 <b>말을 끊고 듣기로 넘어간다</b>(끼어들기). 이것이
    /// 없으면 긴 답을 끝까지 들어야 해서 음성 대화의 값이 사라진다.
    /// 듣는 중이면 거기서 끊고 묻는다. 둘 다 아니면 듣기 시작한다.
    /// </remarks>
    private async Task TapOrbAsync()
    {
        if (_transcribing) return;

        if (_speaking)
        {
            _listenAfterSpeech = false;
            await StopSpeakingAsync();
            await StartListeningAsync(auto: _autoEnd);
            return;
        }

        if (_listening)
        {
            await FinishListeningAsync(sendStraightAway: true);
            return;
        }

        await StartListeningAsync(auto: _autoEnd);
    }

    // ── 듣기 ────────────────────────────────────────────────

    private async Task StartListeningAsync(bool auto)
    {
        if (_listening || _transcribing) return;

        if (await VoiceJsAsync() is not { } js)
        {
            _voiceNotice = "이 브라우저에서는 녹음을 쓸 수 없습니다.";
            StateHasChanged();
            return;
        }

        _voiceNotice = null;

        bool started;
        try
        {
            // 오브가 없을 때(받아쓰기)는 소리 크기를 적을 자리도 없다.
            started = await js.InvokeAsync<bool>(
                "start", SelfRef(), _voiceMode ? _orbRef : (object?)null, auto);
        }
        catch (Exception ex) when (ex is JSException or JSDisconnectedException
            or InvalidOperationException or TaskCanceledException)
        {
            started = false;
        }

        if (!started)
        {
            // 거의 늘 권한 거절이다. 그다음이 HTTPS 가 아닌 주소로 들어온 경우 —
            // 그때는 브라우저가 마이크를 아예 안 열어 준다.
            _voiceNotice = "마이크를 열지 못했습니다. 브라우저의 마이크 권한을 확인해 주세요.";
            StateHasChanged();
            return;
        }

        _listening = true;
        _heard = null;
        StateHasChanged();
    }

    /// <summary>녹음을 버리고 마이크를 닫는다. 글로 바꾸지 않는다.</summary>
    private async Task StopListeningAsync(bool discard)
    {
        if (!_listening && _voiceJs is null) return;

        _listening = false;

        if (discard && _voiceJs is not null)
        {
            try
            {
                await _voiceJs.InvokeVoidAsync("discard");
            }
            catch (Exception ex) when (ex is JSException or JSDisconnectedException
                or InvalidOperationException or TaskCanceledException)
            {
                // 회로가 닫혔다. 브라우저가 떠나면서 마이크도 함께 닫힌다.
            }
        }
    }

    /// <summary>
    /// 녹음을 끝내고 글로 바꾼다.
    /// </summary>
    /// <param name="sendStraightAway">
    /// 참이면 바로 묻는다(음성 모드). 거짓이면 입력칸에 채운다(받아쓰기).
    /// </param>
    private async Task FinishListeningAsync(bool sendStraightAway)
    {
        if (!_listening || _voiceJs is null) return;

        _listening = false;
        _transcribing = true;
        StateHasChanged();

        try
        {
            var clip = await _voiceJs.InvokeAsync<VoiceClip?>("stop");

            if (clip is null || string.IsNullOrEmpty(clip.Base64))
            {
                _voiceNotice = "녹음된 소리가 없습니다.";
                return;
            }

            var r = await Chat.TranscribeAsync(
                clip.Base64, clip.ContentType, clip.FileName);

            if (!r.Ok)
            {
                // 받아쓰기가 끊긴 것은 고장이 아니라 흔한 일이다. 글로 묻는
                // 길은 그대로라 그쪽으로 안내한다.
                _voiceNotice = $"{r.Message} 글로 물어보실 수 있습니다.";
                return;
            }

            if (r.HeardNothing)
            {
                _voiceNotice = "말소리를 알아듣지 못했습니다. 다시 말씀해 주세요.";
                return;
            }

            _voiceNotice = null;
            _heard = r.Text;

            if (sendStraightAway)
            {
                _input = r.Text;
                _transcribing = false;
                await SendAsync(speakAloud: _speakReplies);
                return;
            }

            // 입력칸에 **이어 붙인다.** 덮어쓰면 적어 둔 것을 잡아먹는다.
            _input = string.IsNullOrWhiteSpace(_input)
                ? r.Text
                : _input.TrimEnd() + " " + r.Text;
        }
        catch (Exception ex) when (ex is JSException or JSDisconnectedException
            or InvalidOperationException or TaskCanceledException)
        {
            _voiceNotice = "녹음을 가져오지 못했습니다.";
        }
        finally
        {
            _transcribing = false;
            StateHasChanged();
        }
    }

    /// <summary>
    /// 조용해졌거나 2분이 지나 JS 가 스스로 끊었다.
    /// </summary>
    /// <param name="why"><c>silence</c> 이거나 <c>max</c>.</param>
    [JSInvokable]
    public async Task OnVoiceAutoStop(string why)
    {
        // JS 쪽 스레드에서 들어온다. 화면을 만지려면 회로로 돌아가야 한다.
        await InvokeAsync(async () =>
        {
            if (!_listening) return;

            if (why == "max")
            {
                _voiceNotice = "녹음이 2분을 넘어 끊었습니다.";
            }

            // 음성 모드에서는 바로 묻고, 받아쓰기에서는 입력칸에 채운다.
            await FinishListeningAsync(sendStraightAway: _voiceMode);
        });
    }

    // ── 읽어주기 ────────────────────────────────────────────

    /// <summary>이번 턴의 답을 읽어 줄 채비. 글은 오는 대로 밀어 넣는다.</summary>
    private async Task BeginSpeakAsync()
    {
        if (await SpeechJsAsync() is not { } js) return;

        try
        {
            _speakStreamOpen = await js.InvokeAsync<bool>("beginStream", SelfRef());
            _speakBuffer.Clear();
        }
        catch (Exception ex) when (ex is JSException or JSDisconnectedException
            or InvalidOperationException or TaskCanceledException)
        {
            _speakStreamOpen = false;
        }
    }

    /// <summary>
    /// 새로 온 글자를 모았다가 <b>문장이 되면</b> 읽어주기에 넘긴다.
    /// </summary>
    private async Task FeedSpeakAsync(string piece)
    {
        if (!_speakStreamOpen || _speechJs is null) return;

        _speakBuffer.Append(piece);

        while (TakeSentence(_speakBuffer) is { Length: > 0 } sentence)
        {
            try
            {
                await _speechJs.InvokeAsync<bool>("pushStream", sentence);
            }
            catch (Exception ex) when (ex is JSException or JSDisconnectedException
                or InvalidOperationException or TaskCanceledException)
            {
                _speakStreamOpen = false;
                return;
            }
        }
    }

    /// <summary>남은 글을 마저 넘기고 읽어주기를 닫는다. 남은 것은 끝까지 읽는다.</summary>
    private async Task EndSpeakAsync()
    {
        if (!_speakStreamOpen || _speechJs is null)
        {
            _speakStreamOpen = false;
            return;
        }

        try
        {
            if (_speakBuffer.Length > 0)
            {
                await _speechJs.InvokeAsync<bool>("pushStream", _speakBuffer.ToString());
            }

            await _speechJs.InvokeVoidAsync("endStream");
        }
        catch (Exception ex) when (ex is JSException or JSDisconnectedException
            or InvalidOperationException or TaskCanceledException)
        {
            // 회로가 닫혔다. 읽던 것은 브라우저가 알아서 멎는다.
        }
        finally
        {
            _speakBuffer.Clear();
            _speakStreamOpen = false;
        }
    }

    private async Task StopSpeakingAsync()
    {
        _speakStreamOpen = false;
        _speakBuffer.Clear();

        if (_speechJs is null)
        {
            _speaking = false;
            return;
        }

        try
        {
            await _speechJs.InvokeVoidAsync("stop");
        }
        catch (Exception ex) when (ex is JSException or JSDisconnectedException
            or InvalidOperationException or TaskCanceledException)
        {
            // 이미 끊겼다.
        }

        _speaking = false;
    }

    /// <summary>읽어주기가 시작·끝났다고 JS 가 알려 준다.</summary>
    [JSInvokable]
    public async Task OnSpeechStateChanged(bool speaking)
    {
        await InvokeAsync(async () =>
        {
            _speaking = speaking;

            // **읽기가 끝난 뒤에야 마이크를 연다.** 읽는 중에 열면 제가 읽는
            // 소리를 제가 받아쓴다.
            if (!speaking && _listenAfterSpeech && _voiceMode)
            {
                _listenAfterSpeech = false;
                await StartListeningAsync(auto: _autoEnd);
            }

            StateHasChanged();
        });
    }

    /// <summary>
    /// 버퍼 앞에서 <b>문장 하나</b>를 떼어 낸다. 아직 없으면 <c>null</c>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// 문장 끝은 <c>. ! ? …</c> 과 줄바꿈이다. <b>마침표 뒤가 숫자면 문장
    /// 끝이 아니다</b> — 「3.14」나 「1.2 판」이 두 동강 나면 읽는 소리가
    /// 이상해진다.
    /// </para>
    /// <para>
    /// 문장 끝이 안 와도 <b>너무 길어지면 끊는다.</b> 목록이나 표처럼 마침표
    /// 없이 길게 가는 글이 있는데, 그것을 다 모을 때까지 기다리면 아무 소리도
    /// 안 난다.
    /// </para>
    /// </remarks>
    private const int SpeakFlushAt = 140;

    private static string? TakeSentence(System.Text.StringBuilder buffer)
    {
        if (buffer.Length == 0) return null;

        for (var i = 0; i < buffer.Length; i++)
        {
            var c = buffer[i];

            var isEnd = c is '\n' or '!' or '?' or '。' or '！' or '？' or '…'
                || (c == '.' && !(i + 1 < buffer.Length && char.IsDigit(buffer[i + 1])));

            if (!isEnd) continue;

            var take = i + 1;
            var sentence = buffer.ToString(0, take);
            buffer.Remove(0, take);
            return sentence.Trim();
        }

        // 문장 끝이 안 온다. 길어지면 빈칸에서 끊는다.
        if (buffer.Length < SpeakFlushAt) return null;

        var cut = -1;
        for (var i = buffer.Length - 1; i > SpeakFlushAt / 2; i--)
        {
            if (buffer[i] is ' ' or ',' or '、' or ',')
            {
                cut = i + 1;
                break;
            }
        }

        if (cut < 0) cut = buffer.Length;

        var piece = buffer.ToString(0, cut);
        buffer.Remove(0, cut);
        return piece.Trim();
    }

    /// <summary>
    /// 브라우저가 녹음해 준 한 마디. <c>chat-voice.js</c> 의 <c>stop()</c> 이 준다.
    /// </summary>
    /// <param name="Base64">녹음된 바이트.</param>
    /// <param name="ContentType">그 바이트의 종류. 브라우저마다 다르다.</param>
    /// <param name="FileName">받아쓰기 서버 로그에 남을 이름.</param>
    /// <param name="Ms">녹음 길이(밀리초).</param>
    private sealed record VoiceClip(string Base64, string ContentType, string FileName, int Ms);

    /// <summary>
    /// 화면을 떠날 때. <b>마이크를 반드시 닫는다</b> — 안 닫으면 탭에 녹음
    /// 표시가 켜진 채로 남고, 사람은 포털이 몰래 듣고 있다고 읽는다.
    /// </summary>
    public async ValueTask DisposeAsync()
    {
        _cts?.Cancel();
        _cts?.Dispose();

        if (_voiceJs is not null)
        {
            try
            {
                await _voiceJs.InvokeVoidAsync("discard");
                await _voiceJs.DisposeAsync();
            }
            catch (Exception ex) when (ex is JSException or JSDisconnectedException
                or InvalidOperationException or TaskCanceledException or ObjectDisposedException)
            {
                // 회로가 이미 닫혔다. 브라우저가 떠나면서 마이크도 함께 닫힌다.
            }
        }

        if (_speechJs is not null)
        {
            try
            {
                await _speechJs.InvokeVoidAsync("stop");
                await _speechJs.DisposeAsync();
            }
            catch (Exception ex) when (ex is JSException or JSDisconnectedException
                or InvalidOperationException or TaskCanceledException or ObjectDisposedException)
            {
            }
        }

        _selfRef?.Dispose();
    }
}
