using JSini.Web.ProjMng.Api;

namespace JSini.Web.ProjMng.Components.Shared;

/// <summary>
/// 지시 한 건의 <b>한 차수</b> — 그때 사람이 적은 글과 그 답 한 쌍.
/// </summary>
/// <remarks>
/// <para>
/// 「이어서 지시」는 새 건을 만들지 않는다 — 같은 건에 실행이 하나 더 붙는다
/// (<c>AiTaskService.ContinueAsync</c>). 그래서 <b>차수는 실행 줄이다</b>
/// (<c>ai_task_run.seq</c> · 서버도 이어가기 프롬프트에 「N차」로 적는다).
/// </para>
/// <para>
/// <b>아직 실행 줄이 없는 차수가 하나 있을 수 있다.</b> 보냈는데 아무도 안
/// 집어 갔거나, 적어만 두고 안 보낸 건이다(<see cref="Run"/> 이 <c>null</c>).
/// 그 차수에는 지시만 있고 답이 없다 — 그래도 <b>자리는 만든다.</b> 보낸 글이
/// 어디에도 안 보이면 보는 사람은 그것이 갔는지조차 알 수 없다.
/// </para>
/// </remarks>
public sealed class AiTaskTurn
{
    /// <summary>몇 차인가. <b>1 부터다</b>(실행 줄의 <c>seq</c>).</summary>
    public required int No { get; init; }

    /// <summary>그 차수의 실행 줄. <b>널이면 아직 안 돈 차수</b>다.</summary>
    public AiTaskRunDto? Run { get; init; }

    /// <summary>
    /// 그 차수에 <b>사람이 적은 글.</b> 서버가 앞에 덧댄 「지난 진행」은
    /// <see cref="Lead"/> 로 갈라 둔다 — 읽는 법은 <see cref="AiTaskAsk.Split"/>.
    /// </summary>
    public string? Ask { get; init; }

    /// <summary>
    /// 그 글 앞에 <b>서버가 덧댄 것</b>(지난 진행 · 재시도 전의 본문).
    /// 접어 두고, 펴면 그때 실제로 간 글 전부가 보인다.
    /// </summary>
    public string? Lead { get; init; }

    /// <summary>그 차수의 답 원문(<c>result_text</c>).</summary>
    public string? Result { get; init; }

    /// <summary>답이 없을 때 그 자리에 적을 말. 사정이 넷이라 갈라 적는다.</summary>
    public required string ResultWaitText { get; init; }

    /// <summary>머리줄에 적을 상태. 실행 줄이 있으면 그 실행의 것이다.</summary>
    public required string StatusText { get; init; }

    /// <summary>머리줄 배지 수식어(<c>jsini-badge--*</c>).</summary>
    public required string StatusTone { get; init; }

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
    /// <summary>
    /// 오래된 차수가 앞이다. <b>사람이 읽는 순서</b>이고, 서버가 이어가기
    /// 프롬프트에 지난 진행을 적는 순서와도 같다.
    /// </summary>
    /// <param name="task">볼 작업. 아직 안 돈 차수의 지시문이 여기 있다.</param>
    /// <param name="runs">실행 이력. <b>서버는 최근 것을 앞에 준다.</b></param>
    public static IReadOnlyList<AiTaskTurn> Build(
        AiTaskDto task, IReadOnlyList<AiTaskRunDto> runs)
    {
        var list = new List<AiTaskTurn>();

        foreach (var run in runs.OrderBy(r => r.Seq))
        {
            var (ask, lead) = AiTaskAsk.Split(run.Instruction);

            // 실행 줄에 지시문이 안 남은 옛 건(그 칸이 생기기 전이다).
            // **한 번만 돈 건이면** 지금 본문이 그때 간 글 그대로다 —
            // 이어서 지시한 건은 본문이 마지막 차수의 것으로 덮여 있어
            // 여기에 끌어다 쓰면 1차 자리에 3차 글이 앉는다.
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
            });
        }

        // 보냈는데 아직 실행 줄이 없는 차수. **돌고 있는 차수가 있을 때는
        // 만들지 않는다** — 그때는 그 실행 줄이 곧 이 차수다.
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
            });
        }

        if (list.Count > 0)
        {
            list[0].IsFirst = true;
            list[^1].IsLast = true;
        }

        return list;
    }
}

/// <summary>
/// 그 차수에 간 지시문에서 <b>사람이 적은 부분</b>을 가른다.
/// </summary>
/// <remarks>
/// <para>
/// 2차부터의 지시문은 사람이 적은 글이 아니다. 서버가 <b>지난 진행을 앞에
/// 덧대</b> 한 덩어리로 만들어 보낸다(<c>AiTaskService.BuildContinuation</c>) —
/// 「## 이전까지의 진행 … ## 이번에 할 일 … 」. 재시도도 비슷하게 본문 뒤에
/// 「## 재시도 추가 지시」를 붙인다.
/// </para>
/// <para>
/// 차수별로 펴 놓고 그 덧댄 글까지 그대로 보여 주면, <b>1차의 글이 2차·3차에
/// 요약된 꼴로 또 서고 또 선다.</b> 같은 말이 세 번 보이는 판에서는 이번 차수에
/// 무엇을 더 시켰는지가 묻힌다. 그래서 <b>마지막 머리표 뒤만</b> 본문으로 올리고
/// 앞엣것은 접어 둔다 — 지우지는 않는다. 그때 실제로 간 글이 무엇이었나는
/// 「왜 AI 가 그렇게 했나」를 되짚을 때 유일한 단서다.
/// </para>
/// <para>
/// <b>머리표가 없으면 통째로 사람의 글이다</b> — 1차가 그렇다.
/// </para>
/// </remarks>
public static class AiTaskAsk
{
    /// <summary>이어서 지시가 세우는 머리표(<c>BuildContinuation</c>).</summary>
    private const string TurnMark = "## 이번에 할 일";

    /// <summary>재시도가 세우는 머리표(<c>AiTaskService.RetryAsync</c>).</summary>
    private const string RetryMark = "## 재시도 추가 지시";

    /// <summary>
    /// (사람이 적은 글, 앞에 덧댄 글)로 가른다. 덧댄 것이 없으면 뒤엣것이 널이다.
    /// </summary>
    public static (string? Ask, string? Lead) Split(string? instruction)
    {
        if (string.IsNullOrWhiteSpace(instruction))
        {
            return (null, null);
        }

        var lines = instruction.Replace("\r\n", "\n").Split('\n');
        var at = -1;

        // **마지막 머리표를 집는다.** 이어서 지시한 건을 다시 재시도하면
        // 둘이 한 글에 함께 있고, 그때 사람이 마지막에 적은 것은 뒤엣것이다.
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
            return (instruction.Trim(), null);
        }

        var ask = string.Join('\n', lines.Skip(at + 1)).Trim();

        // 머리표만 있고 뒤가 비었다. 가르면 본문이 통째로 사라지므로 그대로 둔다.
        if (ask.Length == 0)
        {
            return (instruction.Trim(), null);
        }

        var lead = string.Join('\n', lines.Take(at)).Trim();

        return (ask, lead.Length == 0 ? null : lead);
    }
}
