using JSini.Web.Components.Layout;
using JSini.Web.ProjMng.Api;
using Microsoft.AspNetCore.Components;

namespace JSini.Web.ProjMng.Components.Shared;

public partial class AiTaskWriteAssist : IDisposable
{
    [Inject] private AiTaskAssist Ai { get; set; } = default!;
    [Inject] private Toasts Toasts { get; set; } = default!;

    /// <summary>
    /// 지금 화면에 있는 지시문을 읽어 오는 길.
    /// </summary>
    /// <remarks>
    /// <b>글자가 아니라 부르는 길을 받는다.</b> 파라미터로 글자를 받으면
    /// 그 값은 <b>부모가 마지막으로 그린 때</b>의 것이다 — 편집기가 친 글을
    /// 곧바로 올려 주지 않으므로(<c>CodeEditor.DebounceMs</c>, 기본 300ms)
    /// 누르는 순간에 읽어야 방금 적은 줄까지 간다.
    /// </remarks>
    [Parameter, EditorRequired] public Func<string?> Content { get; set; } = default!;

    /// <summary>도는 중인 작업은 못 고친다. 그때는 단추도 회색이다.</summary>
    [Parameter] public bool Enabled { get; set; } = true;

    /// <summary>사람이 고른 제목.</summary>
    [Parameter] public EventCallback<string> TitleChosen { get; set; }

    /// <summary>본문 맨 위에 얹을 요약 조각(마크다운).</summary>
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

    /// <summary>
    /// 창을 연다. <b>지난번에 지은 후보를 들고 열지 않는다</b> —
    /// 그 사이에 지시문을 고친 사람이 그것을 지금 글의 제목으로 믿는다.
    /// </summary>
    private void Open()
    {
        Stop();
        _open = true;
    }

    private void Close()
    {
        _open = false;
        Stop();
    }

    /// <summary>하던 일을 거두고 지어 둔 후보도 버린다.</summary>
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
        var content = Begin("제목을 짓고 있습니다");

        if (content is null)
        {
            return;
        }

        try
        {
            var answer = await Ai.SuggestTitlesAsync(content, _work!.Token);
            _model = answer.Model;

            if (answer.Titles.Count < AiTaskAssist.MinTitles)
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
        var content = Begin("내용을 간추리고 있습니다");

        if (content is null)
        {
            return;
        }

        try
        {
            var summary = await Ai.SummarizeAsync(content, _work!.Token);
            _model = summary.Model;

            if (summary.Markdown is null)
            {
                Toasts.Show("내용을 간추리지 못했습니다. 다시 눌러 보십시오.", NoticeTone.Warning);
                return;
            }

            await SummaryReady.InvokeAsync(summary.Markdown);

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
    /// 지시문을 읽고 기다리는 모양으로 바꾼다. 비어 있으면 <c>null</c> —
    /// <b>AI 를 부르지 않는다.</b>
    /// </summary>
    private string? Begin(string what)
    {
        var content = Content();

        if (string.IsNullOrWhiteSpace(content))
        {
            // 빈 글로 물으면 모델이 **아무 말이나 지어낸다.** 그것이 제목
            // 후보로 뜨면 사람은 그럴듯해서 고른다.
            Toasts.Show("지시 내용을 먼저 채워 주십시오. 그 글을 보고 짓습니다.", NoticeTone.Warning);
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
