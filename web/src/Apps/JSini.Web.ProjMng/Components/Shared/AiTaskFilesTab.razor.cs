using Microsoft.AspNetCore.Components;
using JSini.Web.ProjMng.Api;

namespace JSini.Web.ProjMng.Components.Shared;

/// <summary>
/// 「처리 파일」 탭 한 장 — <b>이 작업이 만들고 고친 파일들, 차수별로.</b>
/// 부르는 쪽의 <c>DxTabs</c> 안에 끼워 쓴다(<see cref="AiTaskAskTab"/> 머리말).
/// </summary>
public partial class AiTaskFilesTab
{
    /// <summary>
    /// 보고 있는 건. <b>빈 탭에 무엇이라 적을지가 이것으로 갈린다</b>
    /// (<see cref="EmptyText"/>).
    /// </summary>
    [Parameter] public AiTaskDto? Item { get; set; }

    /// <summary>
    /// 가장 최근 실행. <b>「아직 돈 적이 없나」를 가리는 데만 쓴다</b> —
    /// 목록도 아래 한 줄도 이제 차수마다 제 실행 줄에서 나온다.
    /// </summary>
    [Parameter] public AiTaskRunDto? Run { get; set; }

    /// <summary>
    /// 그릴 묶음들 — <b>차수 하나에 목록 하나.</b> 오래된 차수가 앞이다.
    /// 짓는 법은 <see cref="AiRunFileTurns.Build"/>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>올린 파일과 반대 방향이다.</b> 그쪽은 사람이 지시에 붙여 올린 것이고
    /// (<see cref="AiTaskAskTab.Files"/>) 이쪽은 AI 가 저장소에 남긴 것이다.
    /// 둘이 한 판에 있으므로 이름을 섞지 않는다.
    /// </para>
    /// <para>
    /// <b>탭 안에서는 접지 않는다</b>(2026-10-12). 하루는 요약 아래에서 여덟
    /// 줄만 펴 두고 나머지를 「N개 더 보기」로 접었는데, 그것은 <b>옆에 있는
    /// 결과문을 지키려던 것</b>이었다. 제 탭을 얻은 뒤로는 밀어낼 것이 없고,
    /// 여기까지 건너온 사람이 보려는 것이 그 목록이다.
    /// </para>
    /// <para>
    /// <b>차수를 갈라 쌓는다</b>(2026-10-18). 한동안 여기 깔린 것은 <b>가장
    /// 최근 실행 하나</b>의 목록이었다 — 3차까지 간 건에서 1·2차가 만든 파일은
    /// 화면 어디에도 없었다. 왜 합쳐 펴지 않는지는 <see cref="AiRunFileTurns"/>.
    /// </para>
    /// </remarks>
    [Parameter] public IReadOnlyList<AiRunFileTurn> Turns { get; set; } = [];

    /// <summary>
    /// 탭 이름을 <b>직접 적고 싶을 때.</b> 비우면 개수를 붙여 스스로 짓는다.
    /// </summary>
    [Parameter] public string? Text { get; set; }

    /// <summary>
    /// 탭 이름에 적을 개수 — <b>이 작업이 건드린 파일이 모두 몇이냐.</b>
    /// 같은 경로는 차수가 달라도 한 번만 센다(<see cref="AiRunFileTurns.DistinctCount"/>).
    /// </summary>
    private int ChangedCount => AiRunFileTurns.DistinctCount(Turns);

    /// <summary>그릴 줄이 한 줄이라도 있나. 없으면 <see cref="EmptyText"/> 한 줄만 선다.</summary>
    private bool HasAny => Turns.Any(t => t.Files.Count > 0);

    /// <summary>
    /// 차수 머리를 세울까. <b>차수가 하나뿐이면 안 세운다</b> — 「1차」 한 줄은
    /// 가르는 것이 없이 자리만 먹는다.
    /// </summary>
    private bool ShowTurnHeads => Turns.Count(t => t.Files.Count > 0) > 1;

    /// <summary>대 보니 <b>그런 파일이 없는</b> 줄. 가장 먼저 말해야 하는 수다.</summary>
    private int MissingCount => AiRunFileTurns.CountOf(Turns, AiRunFileCheck.Missing);

    /// <summary>이름만 맞는 것이 여럿이라 못 고른 줄.</summary>
    private int AmbiguousCount => AiRunFileTurns.CountOf(Turns, AiRunFileCheck.Ambiguous);

    /// <summary>대 보니 실제로 있는 파일이던 줄.</summary>
    private int FoundCount => AiRunFileTurns.CountOf(Turns, AiRunFileCheck.Found);

    /// <summary>아직 안 대 본 줄. 실행기가 곧 대 본다.</summary>
    private int PendingCount => AiRunFileTurns.CountOf(Turns, AiRunFileCheck.Pending);

    /// <summary>
    /// 줄 한 장에 걸 꾸밈 이름.
    /// </summary>
    /// <remarks>
    /// <b>없는 경로만 따로 센다.</b> 나머지 추정 줄은 「덜 미더운 것」이라
    /// 점선으로 물러서 있으면 되지만, 없는 경로는 <b>틀린 것</b>이라 같은
    /// 모습으로 두면 목록을 훑는 눈이 그냥 지나간다.
    /// </remarks>
    private static string? RowClass(AiRunFile f) => f.Check switch
    {
        AiRunFileCheck.Missing => "pm-peek__chg-row--gone",
        AiRunFileCheck.None => null,
        _ => "pm-peek__chg-row--guess",
    };

    /// <summary>
    /// 탭에 적을 이름. <b>개수를 여기 적는다</b> — 건너오기 전에도
    /// 「몇 개인가」는 알아야 건너올지가 정해진다.
    /// </summary>
    private string TabText => Text
        ?? (ChangedCount == 0 ? "처리 파일" : $"처리 파일 ({ChangedCount})");

    /// <summary>
    /// 차수 묶음 머리에 적을 말 — 「2차 · 3개」.
    /// </summary>
    private static string TurnHead(AiRunFileTurn turn) =>
        turn.Count == 0 ? $"{turn.No}차" : $"{turn.No}차 · {turn.Count}개";

    /// <summary>
    /// 차수 묶음 아래 한 줄 — <b>가지 · 몇 줄이 바뀌었나.</b>
    /// </summary>
    /// <remarks>
    /// <b>이름 목록이 말하지 않는 것이 여기 있다.</b> 차수별 답 아래의 그 줄과
    /// 같은 값이고(<c>AiTaskAskTab.TurnHint</c>), 둘 다 <b>그 차수</b>의 것이다.
    /// <para>
    /// <b>올린 자리는 여기 안 적는다.</b> 그 값은 작업에 하나뿐이라
    /// (<c>ai_task.pushed_commit</c>) 차수마다 깔면 <b>안 올린 차수까지</b>
    /// 올라간 것처럼 보인다 — 목록 맨 끝에 한 번만 적는다(<see cref="PushHint"/>).
    /// </para>
    /// </remarks>
    private static string? TurnHint(AiRunFileTurn turn)
    {
        if (turn.Run is not { } run)
        {
            return null;
        }

        var parts = new List<string>();

        if (run.GitBranch is { Length: > 0 } branch)
        {
            parts.Add(branch);
        }

        if (run.DiffStat is { Length: > 0 } diff)
        {
            parts.Add(diff.Split('\n')[^1].Trim());
        }

        return parts.Count == 0 ? null : string.Join(" · ", parts);
    }

    /// <summary>올린 자리 한 줄. 작업에 하나뿐이라 <b>맨 끝에 한 번</b> 적는다.</summary>
    private string? PushHint => Item?.PushedCommit is { Length: > 0 } commit
        ? $"올림 {commit[..Math.Min(7, commit.Length)]}"
        : null;

    /// <summary>
    /// 처리 파일이 없을 때 그 자리에 적을 말.
    /// </summary>
    /// <remarks>
    /// <b>셋을 가른다.</b> 아직 도는 중인가 · 돈 적이 없는가 · 돌았는데 건드린
    /// 것이 없는가. 한 가지 말로 뭉개면 보는 사람이 「기다리면 차는 것」과
    /// 「원래 빈 것」을 구분하지 못해 같은 자리를 몇 번씩 다시 연다.
    /// </remarks>
    private string EmptyText => Item switch
    {
        { IsBusy: true } => "아직 돌고 있습니다. 끝나면 이번에 만들고 고친 파일이 여기 섭니다.",
        _ when Run is null => "아직 실행된 적이 없습니다.",
        _ => "이번 작업이 만들거나 고친 파일이 없습니다.",
    };
}
