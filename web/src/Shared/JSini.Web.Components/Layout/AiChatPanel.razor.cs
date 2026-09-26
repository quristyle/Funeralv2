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

    protected override async Task OnInitializedAsync()
    {
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

        try
        {
            // 마지막 빈 칸은 보내지 않는다 — 서버 문맥에 빈 답이 섞인다.
            var context = _messages.Take(_messages.Count - 1).ToList();

            await foreach (var part in Chat.StreamAsync(context, _cts.Token))
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
            StateHasChanged();
        }
    }

    private void Clear()
    {
        _messages.Clear();
        _notices.Clear();
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
