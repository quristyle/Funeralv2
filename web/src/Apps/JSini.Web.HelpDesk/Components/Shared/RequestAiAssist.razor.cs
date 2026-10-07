using JSini.Web.Components.Layout;
using JSini.Web.HelpDesk.Api;
using Microsoft.AspNetCore.Components;

namespace JSini.Web.HelpDesk.Components.Shared;

public partial class RequestAiAssist : IDisposable
{
    [Inject] private RequestAi Ai { get; set; } = default!;
    [Inject] private Toasts Toasts { get; set; } = default!;

    /// <summary>
    /// 지금 화면에 있는 본문을 읽어 오는 길.
    /// </summary>
    /// <remarks>
    /// <b>글자가 아니라 부르는 길을 받는다.</b> 편집기는 친 글을 곧바로
    /// 알려 주지 않는다(<c>InputDelay</c>, 기본 500ms). 파라미터로 글자를
    /// 받으면 방금 친 마지막 줄이 빠진 채로 AI 에게 간다 — 「등록」이
    /// <c>FlushContentAsync</c> 를 먼저 부르는 것과 같은 까닭이다.
    /// </remarks>
    [Parameter, EditorRequired] public Func<Task<string?>> Content { get; set; } = default!;

    /// <summary>사람이 고른 제목.</summary>
    [Parameter] public EventCallback<string> TitleChosen { get; set; }

    /// <summary>본문 맨 위에 얹을 요약 조각(HTML).</summary>
    [Parameter] public EventCallback<string> SummaryReady { get; set; }

    private bool _open;
    private bool _busy;
    private string _busyWhat = string.Empty;
    private List<string> _titles = [];

    /// <summary>
    /// 이번 답을 만든 AI 의 이름. 모르면 <c>null</c> — 공급자가 그 표시를
    /// 안 보내 줄 수도 있어서 <b>없을 수 있는 값</b>이다.
    /// </summary>
    private string? _model;

    /// <summary>
    /// 화면을 떠나면 하던 일을 그만둔다. 로컬 LLM 은 느릴 때 십 초가 넘는데,
    /// 그 사이에 사람이 나가면 돌아온 답이 <b>없는 화면</b>을 고치려 든다.
    /// </summary>
    private CancellationTokenSource? _work;

    private void Toggle()
    {
        _open = !_open;

        if (!_open)
        {
            Stop();
        }
    }

    private void Close()
    {
        _open = false;
        Stop();
    }

    /// <summary>
    /// 하던 일을 거두고 지어 둔 후보도 버린다. <b>판을 닫으면 후보도
    /// 사라진다</b> — 다음에 열었을 때 전에 쓰던 본문으로 지은 제목이
    /// 그대로 떠 있으면, 그 사이에 본문을 고친 사람이 그것을 지금 글의
    /// 제목으로 믿는다.
    /// </summary>
    private void Stop()
    {
        _work?.Cancel();
        _work = null;
        _busy = false;
        _titles = [];
        _model = null;
    }

    private async Task MakeTitlesAsync()
    {
        var content = await BeginAsync("제목을 짓고 있습니다");

        if (content is null)
        {
            return;
        }

        try
        {
            var answer = await Ai.SuggestTitlesAsync(content, _work!.Token);
            _model = answer.Model;

            if (answer.Titles.Count < RequestAi.MinTitles)
            {
                // **몇 개 안 나온 것을 그냥 보여 주지 않는다.** 하나나 둘만
                // 뜨면 사람은 그것이 최선이라고 읽는다. 모델이 형식을 어긴
                // 것이지 후보가 그것뿐인 것이 아니다.
                Toasts.Show("제목 후보를 충분히 짓지 못했습니다. 다시 눌러 보십시오.", NoticeTone.Warning);
                return;
            }

            _titles = [.. answer.Titles];
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception)
        {
            // 공급자가 전부 막혔거나(429·502) 네트워크가 끊겼다. 까닭을
            // 그대로 옮겨 봐야 사람이 할 수 있는 일이 없다.
            Toasts.Show("AI 가 답하지 못했습니다. 잠시 뒤에 다시 눌러 보십시오.", NoticeTone.Error);
        }
        finally
        {
            End();
        }
    }

    private async Task SummarizeAsync()
    {
        var content = await BeginAsync("내용을 간추리고 있습니다");

        if (content is null)
        {
            return;
        }

        try
        {
            var summary = await Ai.SummarizeAsync(content, _work!.Token);
            _model = summary.Model;

            if (summary.Html is null)
            {
                Toasts.Show("내용을 간추리지 못했습니다. 다시 눌러 보십시오.", NoticeTone.Warning);
                return;
            }

            await SummaryReady.InvokeAsync(summary.Html);

            // 꽂고 나면 판을 닫는다. 요약은 고를 것이 없는 일이라, 판이
            // 열린 채로 남으면 본문 맨 위에 방금 들어간 것을 가린다.
            _open = false;
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception)
        {
            Toasts.Show("AI 가 답하지 못했습니다. 잠시 뒤에 다시 눌러 보십시오.", NoticeTone.Error);
        }
        finally
        {
            End();
        }
    }

    /// <summary>
    /// 본문을 읽고 기다리는 모양으로 바꾼다. 본문이 비어 있으면
    /// <c>null</c> — <b>AI 를 부르지 않는다.</b>
    /// </summary>
    private async Task<string?> BeginAsync(string what)
    {
        var content = await Content();

        if (RequestContentHtml.IsBlank(content))
        {
            // 빈 글로 물으면 모델이 **아무 말이나 지어낸다.** 그것이 제목
            // 후보로 뜨면 사람은 그럴듯해서 고른다.
            Toasts.Show("내용을 먼저 채워 주십시오. 그 글을 보고 짓습니다.", NoticeTone.Warning);
            return null;
        }

        _work?.Cancel();
        _work = new CancellationTokenSource();
        _busy = true;
        _busyWhat = what;
        _titles = [];
        _model = null;

        return content;
    }

    private void End()
    {
        _busy = false;
        _work?.Dispose();
        _work = null;
    }

    private async Task PickAsync(string title)
    {
        await TitleChosen.InvokeAsync(title);

        _open = false;
        _titles = [];
    }

    public void Dispose()
    {
        _work?.Cancel();
        _work?.Dispose();
        _work = null;
    }
}
