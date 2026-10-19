using JSini.Web.ProjMng.Api;

namespace JSini.Web.ProjMng.Components.Shared;

/// <summary>지시 한 건의 한 차수 — 그때 사람이 적은 글과 그 답 한 쌍. 차수는 실행 줄(seq)이고, 실행 줄이 없는 차수도 자리는 만든다.</summary>
public sealed class AiTaskTurn
{
    /// <summary>몇 차인가. <b>1 부터다</b>(실행 줄의 <c>seq</c>).</summary>
    public required int No { get; init; }

    /// <summary>그 차수의 실행 줄. <b>널이면 아직 안 돈 차수</b>다.</summary>
    public AiTaskRunDto? Run { get; init; }

    /// <summary>그 차수에 사람이 적은 글. 서버가 덧댄 지난 진행은 Lead 로 갈라 둔다(AiTaskAsk.Split).</summary>
    public string? Ask { get; init; }

    /// <summary>서버가 글 앞에 덧댄 지난 진행. 접어 두고, 펴면 그때 실제로 간 글이 보인다.</summary>
    public string? Lead { get; init; }

    /// <summary>그 차수의 답 원문(<c>result_text</c>).</summary>
    public string? Result { get; init; }

    /// <summary>답이 없을 때 그 자리에 적을 말. 사정이 넷이라 갈라 적는다.</summary>
    public required string ResultWaitText { get; init; }

    /// <summary>머리줄에 적을 상태. 실행 줄이 있으면 그 실행의 것이다.</summary>
    public required string StatusText { get; init; }

    /// <summary>머리줄 배지 수식어(<c>jsini-badge--*</c>).</summary>
    public required string StatusTone { get; init; }

    /// <summary>그 차수의 제목. 서버 AI 가 지은 것을 쓴다 — 지시문 첫 줄을 떠다 쓰면 차수마다 비슷해 가르지 못한다.</summary>
    public string? Title { get; init; }

    /// <summary>머리줄에 적을 시각 한 토막. 「시작 시각 · 걸린 시간」이다.</summary>
    public required string WhenText { get; init; }

    /// <summary>첫 차수인가. <b>함께 올린 파일이 여기 붙는다</b>(첨부는 보낼 때 묶인다).</summary>
    public bool IsFirst { get; set; }

    /// <summary>마지막 차수인가. <b>처음에 펴 두는 것이 이것 하나다.</b></summary>
    public bool IsLast { get; set; }
}

/// <summary>
/// 차수 목록을 짓는다 — 실행 이력과 지금 본문으로.
/// </summary>
public static class AiTaskTurns
{
    /// <summary>오래된 차수가 앞이다. 사람이 읽는 순서이고 이어가기 프롬프트의 순서와 같다.</summary>
    /// <param name="task">볼 작업. 아직 안 돈 차수의 지시문이 여기 있다.</param>
    /// <param name="runs">실행 이력. <b>서버는 최근 것을 앞에 준다.</b></param>
    public static IReadOnlyList<AiTaskTurn> Build(
        AiTaskDto task, IReadOnlyList<AiTaskRunDto> runs)
    {
        var list = new List<AiTaskTurn>();

        foreach (var run in runs.OrderBy(r => r.Seq))
        {
            var (ask, lead) = AiTaskAsk.Split(run.Instruction);

            // 지시문이 안 남은 옛 건. 한 번만 돈 건만 지금 본문을 쓴다 — 이어서 지시한 건은 본문이 마지막 차수 것이라 1차에 3차 글이 앉는다.
            if (string.IsNullOrWhiteSpace(ask) && runs.Count == 1)
            {
                (ask, lead) = AiTaskAsk.Split(task.Contents);
            }

            list.Add(new AiTaskTurn
            {
                No = run.Seq,
                Run = run,
                Ask = ask,
                Lead = lead,
                Result = run.ResultText,
                ResultWaitText = run.IsRunning
                    ? "아직 돌고 있습니다."
                    : "AI 가 남긴 말이 없습니다.",
                StatusText = run.StatusText,
                StatusTone = run.StatusTone,
                WhenText = AiTaskWhen.RunWhen(run),
                Title = Trim(run.TurnTitle),
            });
        }

        // 보냈는데 아직 실행 줄이 없는 차수. 돌고 있는 차수가 있으면 만들지 않는다(그 실행 줄이 곧 이 차수다).
        if (runs.Count == 0 || (task.IsBusy && runs.All(r => r.FinishedAt is not null)))
        {
            var (ask, lead) = AiTaskAsk.Split(task.Contents);

            list.Add(new AiTaskTurn
            {
                No = list.Count == 0 ? 1 : list[^1].No + 1,
                Run = null,
                Ask = ask,
                Lead = lead,
                Result = null,
                ResultWaitText = task.IsBusy
                    ? "아직 시작되지 않았습니다. 실행기가 집어 가면 여기에 답이 섭니다."
                    : "아직 보내지 않았습니다.",
                StatusText = task.StatusText,
                StatusTone = task.StatusTone,
                WhenText = task.IsBusy ? "대기 중" : "-",

                // 실행 줄이 없으니 시킬 때 작업에 얹어 둔 제목을 쓴다. 집어 가는 순간 실행 줄로 옮겨져 둘이 동시에 차지 않는다.
                Title = Trim(task.NextTurnTitle),
            });
        }

        if (list.Count > 0)
        {
            list[0].IsFirst = true;
            list[^1].IsLast = true;
        }

        return list;
    }

    /// <summary>제목을 다듬는다. 흰 줄만 든 값은 null 로 친다 — 그리는 쪽이 Length 하나로 가리게 한다.</summary>
    private static string? Trim(string? title) =>
        string.IsNullOrWhiteSpace(title) ? null : title.Trim();
}

/// <summary>
/// 지시문에서 사람이 적은 부분을 가른다. 마지막 머리표 뒤만 본문이고, 앞의 덧댄 진행은 Lead 로 접는다.
/// 문구 걷기(AiTaskAlways.Strip)는 본문에만 한다 — Lead 는 실제로 간 글이라 문구째 보여야 한다.
/// </summary>
public static class AiTaskAsk
{
    /// <summary>이어서 지시가 세우는 머리표(<c>BuildContinuation</c>).</summary>
    private const string TurnMark = "## 이번에 할 일";

    /// <summary>재시도가 세우는 머리표(<c>AiTaskService.RetryAsync</c>).</summary>
    private const string RetryMark = "## 재시도 추가 지시";

    /// <summary>(사람이 적은 글, 앞에 덧댄 글)로 가른다. 덧댄 것이 없으면 뒤엣것이 널이다.</summary>
    public static (string? Ask, string? Lead) Split(string? instruction)
    {
        if (string.IsNullOrWhiteSpace(instruction))
        {
            return (null, null);
        }

        var lines = instruction.Replace("\r\n", "\n").Split('\n');
        var at = -1;

        // 재시도로 머리표가 둘 함께 있으면 마지막 것 뒤가 사람의 글이다.
        for (var i = 0; i < lines.Length; i++)
        {
            var line = lines[i].Trim();

            if (line == TurnMark || line == RetryMark)
            {
                at = i;
            }
        }

        if (at < 0)
        {
            return (AiTaskAlways.Strip(instruction.Trim()), null);
        }

        var ask = string.Join('\n', lines.Skip(at + 1)).Trim();

        // 머리표만 있고 뒤가 비었다. 가르면 본문이 통째로 사라지므로 그대로 둔다.
        if (ask.Length == 0)
        {
            return (AiTaskAlways.Strip(instruction.Trim()), null);
        }

        var lead = string.Join('\n', lines.Take(at)).Trim();

        // 덧댄 글은 걷지 않는다 — 실제로 간 글이라 문구째 보여야 「왜 그렇게 했나」를 되짚을 수 있다.
        return (AiTaskAlways.Strip(ask), lead.Length == 0 ? null : lead);
    }
}
