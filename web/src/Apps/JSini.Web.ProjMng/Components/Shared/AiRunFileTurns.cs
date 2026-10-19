using JSini.Web.ProjMng.Api;

namespace JSini.Web.ProjMng.Components.Shared;

/// <summary>한 차수가 건드린 파일 목록. 「처리 파일」 탭의 묶음 하나다.</summary>
/// <param name="No">몇 차인가. <b>1 부터다</b>(<c>ai_task_run.seq</c>).</param>
/// <param name="Run">그 차수의 실행 줄. 아래 한 줄(가지 · 몇 줄)이 여기서 나온다.</param>
/// <param name="Files">그 차수의 목록. <b>빌 수 있다</b> — 아무것도 안 고친 차수다.</param>
public sealed record AiRunFileTurn(
    int No, AiTaskRunDto? Run, IReadOnlyList<AiRunFile> Files)
{
    /// <summary>묶음 머리에 적을 개수. 「외 N개」 줄은 파일이 아니라 상한에 걸린 나머지 표시라 뺀다.</summary>
    public int Count => Files.Count(f => f.Kind != AiRunFileKind.More);
}

/// <summary>차수별 「처리 파일」 묶음을 짓는다. 목록은 처음부터 차수별로 쌓여 있으니 전부 펴야 한다 — 최근 실행 하나만 읽으면 앞 차수가 만든 파일이 사라진다.
/// 합쳐서 한 벌로 펴지 않는다: 같은 파일을 차수마다 고치면 어느 차수의 일인지가 사라진다(<see cref="AiTaskTurns"/> 와 같은 까닭).</summary>
public static class AiRunFileTurns
{
    /// <summary>차수 목록에서 실제로 돈 차수의 파일 묶음만 뽑는다. 오래된 차수가 앞이다. 아직 안 돈 차수는 묶음을 안 만든다 — 빈 묶음은 「0개」로 읽힌다.</summary>
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

    /// <summary>탭 이름에 적을 수 — 이 작업이 건드린 파일이 모두 몇이냐. 차수별 개수를 더하지 않는다: 같은 경로를 차수마다 고치면 중복으로 센다.</summary>
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

    /// <summary>대 본 결과가 <paramref name="check"/> 인 줄이 모든 차수를 통틀어 몇인가. 설명 줄이 이 수로 갈린다. 경로는 접지 않는다 — 같은 경로가 두 차수에 서 있으면 보는 눈에도 둘이다.</summary>
    public static int CountOf(IReadOnlyList<AiRunFileTurn> turns, AiRunFileCheck check) =>
        turns.Sum(t => t.Files.Count(f => f.Check == check && f.Kind != AiRunFileKind.More));
}
