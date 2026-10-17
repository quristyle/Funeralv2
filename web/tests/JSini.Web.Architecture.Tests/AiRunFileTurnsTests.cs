using JSini.Web.ProjMng.Api;
using JSini.Web.ProjMng.Components.Shared;
using Xunit;

namespace JSini.Web.Architecture.Tests;

/// <summary>
/// 「처리 파일」을 <b>차수마다 한 묶음</b>으로 가르는 규칙(2026-10-18).
/// </summary>
/// <remarks>
/// <para>
/// <b>이 가르기가 틀리면 조용히 틀린다.</b> 한동안 이 탭이 그리던 것은
/// <b>가장 최근 실행 하나</b>의 목록이었는데, 화면은 그 목록을 「이 작업이
/// 고친 파일」이라는 이름으로 멀쩡히 그렸다 — 3차까지 간 건에서 1·2차가
/// 만든 파일이 어디에도 없다는 것을 보는 사람은 알 길이 없었다.
/// </para>
/// <para>
/// 화면으로는 확인하기 나쁜 자리다 — 2차·3차를 눈으로 보려면 실행기가 운영
/// 장비에서 같은 건을 두 번 세 번 돌아야 한다. 차수를 가르는 규칙
/// (<see cref="AiTaskTurnsTests"/>)과 같은 까닭으로 여기 박아 둔다.
/// </para>
/// </remarks>
public sealed class AiRunFileTurnsTests
{
    /// <summary>
    /// <b>차수마다 제 목록을 든다.</b> 마지막 차수의 것만 남지 않는다.
    /// </summary>
    [Fact]
    public void 차수마다_제_목록을_든다()
    {
        var turns = Build(
            Run(1, "A\tweb/one.razor\nM\tweb/common.razor"),
            Run(2, "M\tweb/two.razor"),
            Run(3, "M\tweb/common.razor\nD\tweb/three.razor"));

        Assert.Equal([1, 2, 3], turns.Select(t => t.No));
        Assert.Equal([2, 1, 2], turns.Select(t => t.Count));

        Assert.Equal("web/one.razor", turns[0].Files[0].Path);
        Assert.Equal(AiRunFileKind.Added, turns[0].Files[0].Kind);
        Assert.Equal("web/two.razor", turns[1].Files[0].Path);
        Assert.Equal(AiRunFileKind.Deleted, turns[2].Files[1].Kind);
    }

    /// <summary>
    /// <b>오래된 차수가 앞이다.</b> 서버는 최근 것을 앞에 주지만
    /// (<c>AiTaskClient.RunsAsync</c>) 사람이 읽는 순서는 반대다 —
    /// 「지시와 결과」가 서는 순서와도 같아야 한다.
    /// </summary>
    [Fact]
    public void 오래된_차수가_앞이다()
    {
        var task = Task();
        var runs = new[] { Run(3, "M\tc.razor"), Run(2, "M\tb.razor"), Run(1, "M\ta.razor") };

        var turns = AiRunFileTurns.Build(AiTaskTurns.Build(task, runs));

        Assert.Equal([1, 2, 3], turns.Select(t => t.No));
    }

    /// <summary>
    /// 탭 이름의 수는 <b>같은 경로를 한 번만</b> 센다. 1차가 만든 파일을 3차가
    /// 또 고쳤다고 해서 「파일이 몇 개냐」의 답이 둘이 되지는 않는다.
    /// </summary>
    [Fact]
    public void 탭의_개수는_경로를_접어_센다()
    {
        var turns = Build(
            Run(1, "A\tweb/one.razor\nM\tweb/common.razor"),
            Run(2, "M\tweb/common.razor"),
            Run(3, "M\tweb/common.razor\nD\tweb/three.razor"));

        // one · common · three — common 이 세 차수에 서 있어도 하나다.
        Assert.Equal(3, AiRunFileTurns.DistinctCount(turns));
    }

    /// <summary>
    /// <b>「외 N개」 줄은 세지 않는다.</b> 그 줄은 파일이 아니라 상한에 걸려
    /// 접힌 나머지가 몇이더라는 말이다.
    /// </summary>
    [Fact]
    public void 외_N개_줄은_개수에서_뺀다()
    {
        var turns = Build(Run(1, "M\tweb/one.razor\n…\t외 12개"));

        Assert.Equal(2, turns[0].Files.Count);
        Assert.Equal(1, turns[0].Count);
        Assert.Equal(1, AiRunFileTurns.DistinctCount(turns));
    }

    /// <summary>
    /// 확인 표의 수는 <b>차수를 통틀어</b> 센다 — 그 설명은 목록 전체에 거는
    /// 말이라, 묶음마다 되풀이하면 같은 글이 차수 수만큼 선다.
    /// </summary>
    /// <remarks>
    /// <b>여기는 경로를 접지 않는다.</b> 물어본 것이 「그 표가 붙은 줄이
    /// 몇이냐」라, 차수 둘에 같은 경로가 서 있으면 보는 눈에도 둘이다.
    /// </remarks>
    [Fact]
    public void 확인_표는_차수를_통틀어_센다()
    {
        var turns = Build(
            Run(1, "M*\tweb/a.razor\nM!\tweb/없는것.razor"),
            Run(2, "M!\tweb/또없는것.razor\nM?\tweb/아직.razor"));

        Assert.Equal(1, AiRunFileTurns.CountOf(turns, AiRunFileCheck.Found));
        Assert.Equal(2, AiRunFileTurns.CountOf(turns, AiRunFileCheck.Missing));
        Assert.Equal(1, AiRunFileTurns.CountOf(turns, AiRunFileCheck.Pending));
        Assert.Equal(0, AiRunFileTurns.CountOf(turns, AiRunFileCheck.Ambiguous));
    }

    /// <summary>
    /// <b>아직 안 돈 차수는 묶음을 안 만든다.</b> 보냈는데 아무도 안 집어 간
    /// 차수가 끝에 하나 더 붙는데(<see cref="AiTaskTurns.Build"/>), 그 차수에는
    /// 실행 줄이 없어 목록도 없다 — 빈 묶음을 세우면 「0개」가 「아직인 것」이
    /// 아니라 「아무것도 안 고친 것」으로 읽힌다.
    /// </summary>
    [Fact]
    public void 아직_안_돈_차수는_묶음이_없다()
    {
        var task = Task();

        task.TaskStatus = "running";

        var turns = AiRunFileTurns.Build(AiTaskTurns.Build(task, [Run(1, "M\ta.razor")]));

        Assert.Single(turns);
        Assert.Equal(1, turns[0].No);
    }

    /// <summary>
    /// 한 번도 안 돈 건은 <b>묶음이 하나도 없다</b> — 탭이 「아직 실행된 적이
    /// 없습니다」 한 줄을 그린다.
    /// </summary>
    [Fact]
    public void 실행이_없으면_묶음도_없다()
    {
        Assert.Empty(AiRunFileTurns.Build(AiTaskTurns.Build(Task(), [])));
        Assert.Equal(0, AiRunFileTurns.DistinctCount([]));
    }

    /// <summary>
    /// 그 칸이 생기기 전(2026-10-12)의 차수는 <c>diff_stat</c> 으로 되읽는다.
    /// <b>차수별로 갈라도 그 뒷길은 살아 있어야 한다.</b>
    /// </summary>
    [Fact]
    public void 옛_차수는_diff_stat_으로_되읽는다()
    {
        var old = Run(1, changedFiles: null);

        old.DiffStat = " web/old.razor | 12 ++--\n 1 file changed";

        var turns = AiRunFileTurns.Build(AiTaskTurns.Build(Task(), [old]));

        Assert.Equal("web/old.razor", turns[0].Files[0].Path);
        Assert.Equal(AiRunFileKind.Unknown, turns[0].Files[0].Kind);
    }

    /// <summary>
    /// <b>탭이 차수 묶음을 받는다.</b> 한 벌짜리 목록으로 돌아가면 앞 차수가
    /// 다시 조용히 사라지므로, 마크업에 그 자리를 박아 둔다.
    /// </summary>
    [Fact]
    public void 탭이_차수_묶음을_그린다()
    {
        var razor = RazorSource.Read(Path.Combine(
            RepoRoot(), "src", "Apps", "JSini.Web.ProjMng", "Components", "Shared",
            "AiTaskFilesTab.razor"));

        Assert.Contains("foreach (var turn in Turns", razor);
        Assert.Contains("pm-peek__chg-turn", razor);

        // **차수 머리는 묶음이 둘 이상일 때만** — 「1차」 한 줄은 가르는 것이
        // 없이 자리만 먹는다.
        Assert.Contains("ShowTurnHeads", razor);
    }

    /// <summary>
    /// 차수를 가르는 선은 <b>「지시와 결과」의 그것과 같은 모양</b>이어야 한다 —
    /// 같은 탭 줄 안에서 차수를 가르는 선이 둘이면 보는 사람이 다른 것으로 읽는다.
    /// </summary>
    [Fact]
    public void 차수를_가르는_선이_있다()
    {
        var css = File.ReadAllText(Path.Combine(
            RepoRoot(), "src", "Apps", "JSini.Web.ProjMng", "wwwroot", "projmng.css"));

        var at = css.IndexOf(".pm-peek__chg-turn + .pm-peek__chg-turn {", StringComparison.Ordinal);

        Assert.True(at >= 0, "차수 묶음을 가르는 규칙이 없습니다.");
        Assert.Contains("border-top", css[at..(css.IndexOf('}', at) + 1)]);
    }

    private static string RepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);

        while (dir is not null && !Directory.Exists(Path.Combine(dir.FullName, "src")))
        {
            dir = dir.Parent;
        }

        Assert.NotNull(dir);

        return dir!.FullName;
    }

    private static IReadOnlyList<AiRunFileTurn> Build(params AiTaskRunDto[] runs) =>
        AiRunFileTurns.Build(AiTaskTurns.Build(Task(), runs));

    private static AiTaskDto Task() => new()
    {
        TaskKey = 1,
        TaskStatus = "done",
        RequestFlag = "none",
        Contents = "적어 보낸 글",
    };

    private static AiTaskRunDto Run(int seq, string? changedFiles) => new()
    {
        RunKey = 100 + seq,
        Seq = seq,
        RunStatus = "succeeded",
        StartedAt = new DateTime(2026, 10, 18, 0, seq, 0, DateTimeKind.Utc),
        FinishedAt = new DateTime(2026, 10, 18, 0, seq, 30, DateTimeKind.Utc),
        Instruction = $"{seq}차 지시",
        ResultText = $"{seq}차 답",
        ChangedFiles = changedFiles,
    };
}
