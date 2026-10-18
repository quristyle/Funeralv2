using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.JSInterop;

namespace JSini.Web.ProjMng.Components.Shared;

public partial class EditorActions : IDisposable
{
    [Inject] private IJSRuntime Js { get; set; } = default!;

    /// <summary>
    /// 편집기 안쪽 오른쪽 위에 <b>얹을지</b>. 거짓이면 글 흐름에 그대로 선다.
    /// </summary>
    /// <remarks>
    /// 얹을 때는 <b>부모에 <c>position: relative</c> 가 있어야 한다.</b>
    /// 없으면 화면 전체를 기준으로 떠서 엉뚱한 자리에 선다.
    /// </remarks>
    [Parameter] public bool Floating { get; set; }

    /// <summary>복사 단추에 붙일 말. 칸이 둘인 자리에서는 서로 달라야 한다.</summary>
    [Parameter] public string CopyTitle { get; set; } = "내용 복사";

    /// <summary>
    /// 복사할 글을 <b>누른 순간에</b> 묻는 길. <see cref="Text"/> 보다 먼저다.
    /// </summary>
    /// <remarks>
    /// 편집기가 들고 있는 값을 그때 읽어야 하는 자리에서 쓴다 —
    /// <c>CodeEditor</c> 는 입력을 모아 올리므로(debounce) 바깥이 들고 있는
    /// 값에는 <b>방금 친 글자가 아직 없다.</b>
    /// </remarks>
    [Parameter] public Func<Task<string?>>? TextProvider { get; set; }

    /// <summary>복사할 글. <see cref="TextProvider"/> 가 없을 때 쓴다.</summary>
    [Parameter] public string? Text { get; set; }

    /// <summary>
    /// 자동 줄바꿈 상태. <see cref="WordWrapChanged"/> 를 걸어 준 자리에만
    /// 줄바꿈 단추가 선다 — 걸 곳이 없으면 켜고 끌 수 없는 단추가 된다.
    /// </summary>
    [Parameter] public bool WordWrap { get; set; }

    [Parameter] public EventCallback<bool> WordWrapChanged { get; set; }

    /// <summary>
    /// 줄 번호를 보이는가. <see cref="LineNumbersChanged"/> 를 걸어 준 자리에만
    /// 스위치가 선다.
    /// </summary>
    [Parameter] public bool LineNumbers { get; set; }

    [Parameter] public EventCallback<bool> LineNumbersChanged { get; set; }

    /// <summary>「…」 판이 펴져 있나.</summary>
    private bool _open;

    private void Toggle() => _open = !_open;

    private void Close() => _open = false;

    private void OnKeyDown(KeyboardEventArgs e)
    {
        if (e.Key == "Escape")
        {
            _open = false;
        }
    }

    /// <summary>복사했다는 표시(체크 아이콘 · 「복사되었습니다」)를 켜 둔 동안인가.</summary>
    private bool _copied;

    /// <summary>표시를 끄는 대기. 연달아 누르면 앞엣것을 거둔다.</summary>
    private CancellationTokenSource? _copiedCts;

    private Task ToggleWrapAsync() => WordWrapChanged.InvokeAsync(!WordWrap);

    private Task ToggleLineNumbersAsync() => LineNumbersChanged.InvokeAsync(!LineNumbers);

    /// <summary>
    /// 글을 클립보드에 넣는다. <b>된 때에만</b> 표시를 켠다 — 안 된 것을
    /// 「복사되었습니다」로 보여 주면 붙여넣기에서야 비어 있는 것을 안다.
    /// </summary>
    /// <remarks>
    /// <c>jsiniClipboard.copy</c> 는 안전한 문맥이 아니거나 권한이 없으면
    /// 숨은 글상자로 한 번 더 해 본다(theme.js). 그래도 안 되면 거짓이다.
    /// </remarks>
    private async Task CopyAsync()
    {
        string? text;

        try
        {
            text = TextProvider is { } ask ? await ask() : Text;
        }
        catch (Exception ex) when (ex is JSException or JSDisconnectedException
            or ObjectDisposedException or TaskCanceledException)
        {
            return;
        }

        if (string.IsNullOrEmpty(text))
        {
            return;
        }

        bool ok;

        try
        {
            ok = await Js.InvokeAsync<bool>("jsiniClipboard.copy", text);
        }
        catch (Exception ex) when (ex is JSException or JSDisconnectedException
            or ObjectDisposedException or TaskCanceledException)
        {
            return;
        }

        if (!ok)
        {
            return;
        }

        _copiedCts?.Cancel();
        _copiedCts?.Dispose();

        var cts = _copiedCts = new CancellationTokenSource();

        _copied = true;
        StateHasChanged();

        // **여기서 기다리지 않는다.** 기다리면 누른 사람의 손짓이 그동안
        // 회로에 매달린 채로 남는다.
        _ = Task.Run(async () =>
        {
            try
            {
                // 판 안에서 「복사되었습니다」를 잠깐 보인 뒤 판째 닫는다.
                await Task.Delay(700, cts.Token);

                _copied = false;
                _open = false;

                await InvokeAsync(StateHasChanged);
            }
            catch (OperationCanceledException)
            {
            }
            catch (ObjectDisposedException)
            {
                // 화면을 떠났다. 끌 표시가 없다.
            }
        });
    }

    public void Dispose()
    {
        _copiedCts?.Cancel();
        _copiedCts?.Dispose();
        _copiedCts = null;
    }
}
