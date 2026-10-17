using JSini.Web.ProjMng.Api;

namespace JSini.Web.ProjMng.Components.Shared;

/// <summary>
/// <b>한 차수가 건드린 파일 목록.</b> 「처리 파일」 탭의 묶음 하나다.
/// </summary>
/// <param name="No">몇 차인가. <b>1 부터다</b>(<c>ai_task_run.seq</c>).</param>
/// <param name="Run">그 차수의 실행 줄. 아래 한 줄(가지 · 몇 줄)이 여기서 나온다.</param>
/// <param name="Files">그 차수의 목록. <b>빌 수 있다</b> — 아무것도 안 고친 차수다.</param>
public sealed record AiRunFileTurn(
    int No, AiTaskRunDto? Run, IReadOnlyList<AiRunFile> Files)
{
    /// <summary>
    /// 묶음 머리에 적을 개수. <b>「외 N개」 줄은 빼고 센다</b> — 그 줄은 파일이
    /// 아니라 상한에 걸려 접힌 나머지가 몇이더라는 말이다.
    /// </summary>
    public int Count => Files.Count(f => f.Kind != AiRunFileKind.More);
}

/// <summary>
/// 차수별 「처리 파일」 묶음을 짓는다.
/// </summary>
/// <remarks>
/// <para>
/// [왜 차수별인가 (2026-10-18)]
/// </para>
/// <para>
/// 목록은 <b>처음부터 차수별로 쌓여 있었다</b> — <c>ai_task_run.changed_files</c>
/// 는 실행 줄마다 제 몫을 들고 있다. 그런데 화면은 <b>가장 최근 실행 하나만</b>
/// 읽어 그렸다. 그래서 3차까지 간 건에서 「처리 파일」 탭이 말하는 것은 3차가
/// 건드린 것뿐이고, <b>1·2차가 만든 파일은 어디에도 없었다.</b> 「이 작업이
/// 무엇을 고쳤나」를 물으러 온 사람에게 그 탭은 <b>마지막 조각만</b> 보여
/// 주면서 전부인 척했다.
/// </para>
/// <para>
/// 합쳐서 한 벌로 펴지도 않는다. 같은 파일을 1차가 만들고 3차가 고치는 일이
/// 흔한데, 한 줄로 접으면 <b>어느 차수의 일이었는지가 사라진다</b> — 차수를
/// 갈라 보려고 지시·결과를 차수별로 편 것과 같은 까닭이다(<see cref="AiTaskTurns"/>).
/// </para>
/// </remarks>
public static class AiRunFileTurns
{
    /// <summary>
    /// 차수 목록에서 <b>실제로 돈 차수</b>의 파일 묶음만 뽑는다. 오래된 차수가 앞이다.
    /// </summary>
    /// <remarks>
    /// <b>아직 안 돈 차수는 묶음을 안 만든다.</b> 그 차수에는 실행 줄이 없어
    /// 목록도 없고, 빈 묶음을 세워 두면 「0개」가 「아직인 것」이 아니라
    /// 「아무것도 안 고친 것」으로 읽힌다.
    /// </remarks>
    public static IReadOnlyList<AiRunFileTurn> Build(IReadOnlyList<AiTaskTurn> turns)
    {
        var list = new List<AiRunFileTurn>();

        foreach (var turn in turns)
        {
            if (turn.Run is not { } run)
            {
                continue;
            }

            list.Add(new AiRunFileTurn(
                turn.No, run, AiRunFiles.Parse(run.ChangedFiles, run.DiffStat)));
        }

        return list;
    }

    /// <summary>
    /// 탭 이름에 적을 수 — <b>이 작업이 건드린 파일이 모두 몇이냐.</b>
    /// </summary>
    /// <remarks>
    /// <b>차수별 개수를 더하지 않는다.</b> 1차가 만든 파일을 3차가 또 고치면
    /// 더하기로는 둘이 되는데, 물어본 것은 「파일이 몇 개냐」다 — 같은 경로는
    /// 한 번만 센다. 묶음 머리에 적는 수는 그 차수의 것이라 따로다
    /// (<see cref="AiRunFileTurn.Count"/>).
    /// </remarks>
    public static int DistinctCount(IReadOnlyList<AiRunFileTurn> turns)
    {
        var seen = new HashSet<string>(StringComparer.Ordinal);

        foreach (var turn in turns)
        {
            foreach (var file in turn.Files)
            {
                if (file.Kind != AiRunFileKind.More)
                {
                    seen.Add(file.Path);
                }
            }
        }

        return seen.Count;
    }

    /// <summary>
    /// 대 본 결과가 <paramref name="check"/> 인 줄이 <b>모든 차수를 통틀어</b> 몇인가.
    /// 목록 아래의 설명 줄들이 이 수로 설지 말지가 갈린다.
    /// </summary>
    /// <remarks>
    /// <b>여기는 경로를 접지 않는다.</b> 설명이 말하는 것은 「그 표가 붙은 줄이
    /// 몇이냐」라, 차수 둘에 같은 경로가 서 있으면 보는 눈에도 둘이다.
    /// </remarks>
    public static int CountOf(IReadOnlyList<AiRunFileTurn> turns, AiRunFileCheck check) =>
        turns.Sum(t => t.Files.Count(f => f.Check == check && f.Kind != AiRunFileKind.More));
}
