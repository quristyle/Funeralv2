using JSini.Web.ProjMng.Api;
using JSini.Web.ProjMng.Components.Shared;
using Xunit;

namespace JSini.Web.Architecture.Tests;

/// <summary>
/// 「지시와 결과」를 <b>차수마다 한 묶음</b>으로 가르는 규칙(2026-10-15).
/// </summary>
/// <remarks>
/// <para>
/// 화면으로는 확인하기 나쁜 자리다 — 2차·3차를 눈으로 보려면 실행기가 운영
/// 장비에서 같은 건을 두 번 세 번 돌아야 한다. 그래서 서버가 짓는 두 가지
/// 지시문 모양(<c>AiTaskService.BuildContinuation</c> 의 「## 이번에 할 일」과
/// <c>RetryAsync</c> 의 「## 재시도 추가 지시」)을 여기 박아 둔다.
/// </para>
/// <para>
/// <b>이 가르기가 틀리면 조용히 틀린다.</b> 1차 자리에 3차의 글이 앉아도
/// 화면은 멀쩡히 그려지고, 읽는 사람은 그것을 1차에 적은 글로 믿는다.
/// </para>
/// </remarks>
public sealed class AiTaskTurnsTests
{
    /// <summary>1차의 지시문에는 머리표가 없다 — 통째로 사람이 적은 글이다.</summary>
    [Fact]
    public void 머리표가_없으면_통째로_사람의_글이다()
    {
        var (ask, lead) = AiTaskAsk.Split("목록 화면의 정렬을 날짜 역순으로 바꿔라.");

        Assert.Equal("목록 화면의 정렬을 날짜 역순으로 바꿔라.", ask);
        Assert.Null(lead);
    }

    /// <summary>이어서 지시한 차수는 <b>「## 이번에 할 일」 뒤만</b> 본문이다.</summary>
    [Fact]
    public void 이어서_지시는_이번에_할_일_뒤만_집는다()
    {
        var (ask, lead) = AiTaskAsk.Split(
            """
            ## 이전까지의 진행

            ### 1차 (2026-10-15 09:00, succeeded)

            지시: 목록 정렬을 바꿔라

            ## 이번에 할 일

            정렬 바꾼 김에 쪽수도 20개로 늘려라.
            """);

        Assert.Equal("정렬 바꾼 김에 쪽수도 20개로 늘려라.", ask);
        Assert.NotNull(lead);
        Assert.StartsWith("## 이전까지의 진행", lead);

        // **덧댄 글에 이번 글이 섞여 들어가면 안 된다** — 접어 둔 자리를 펴
        // 보는 사람에게 같은 글이 두 번 보인다.
        Assert.DoesNotContain("쪽수도 20개로", lead);
    }

    /// <summary>재시도는 본문 뒤에 머리표를 붙인다 — 그 뒤만 이번에 적은 글이다.</summary>
    [Fact]
    public void 재시도_추가_지시도_같은_규칙이다()
    {
        var (ask, lead) = AiTaskAsk.Split(
            "목록 정렬을 바꿔라\n\n---\n## 재시도 추가 지시\n\n이번에는 테스트까지 돌려라.");

        Assert.Equal("이번에는 테스트까지 돌려라.", ask);
        Assert.Contains("목록 정렬을 바꿔라", lead);
    }

    /// <summary>
    /// 머리표가 둘 다 있으면 <b>마지막 것</b>이다 — 이어서 지시한 건을 다시
    /// 재시도하면 사람이 마지막에 적은 것은 뒤엣것이다.
    /// </summary>
    [Fact]
    public void 머리표가_둘이면_마지막_것을_집는다()
    {
        var (ask, _) = AiTaskAsk.Split(
            "## 이번에 할 일\n\n쪽수를 늘려라\n\n---\n## 재시도 추가 지시\n\n빌드부터 고쳐라.");

        Assert.Equal("빌드부터 고쳐라.", ask);
    }

    /// <summary>
    /// 머리표만 있고 뒤가 비었다. <b>가르면 본문이 통째로 사라지므로</b> 그대로 둔다.
    /// </summary>
    [Fact]
    public void 머리표_뒤가_비면_통째로_둔다()
    {
        var (ask, lead) = AiTaskAsk.Split("## 이전까지의 진행\n\n(없음)\n\n## 이번에 할 일\n\n   ");

        Assert.Contains("이전까지의 진행", ask);
        Assert.Null(lead);
    }

    /// <summary>차수는 오래된 것이 앞이다 — 서버는 최근 것을 앞에 준다.</summary>
    [Fact]
    public void 오래된_차수가_앞이다()
    {
        var turns = AiTaskTurns.Build(
            Task("succeeded"),
            [Run(3, "## 이번에 할 일\n\n셋째"), Run(2, "## 이번에 할 일\n\n둘째"), Run(1, "첫째")]);

        Assert.Equal([1, 2, 3], turns.Select(t => t.No));
        Assert.Equal(["첫째", "둘째", "셋째"], turns.Select(t => t.Ask));

        Assert.True(turns[0].IsFirst);
        Assert.False(turns[0].IsLast);
        Assert.True(turns[^1].IsLast);
    }

    /// <summary>
    /// 보냈는데 아직 아무도 안 집어 간 차수에도 <b>자리를 만든다.</b>
    /// </summary>
    /// <remarks>
    /// 보낸 글이 어디에도 안 보이면 보는 사람은 그것이 갔는지조차 알 수 없다.
    /// </remarks>
    [Fact]
    public void 아직_안_돈_차수도_자리를_얻는다()
    {
        var task = Task("queued");
        task.Contents = "## 이전까지의 진행\n\n…\n\n## 이번에 할 일\n\n셋째 것을 마저 해라.";

        var turns = AiTaskTurns.Build(task, [Run(2, "둘째"), Run(1, "첫째")]);

        Assert.Equal(3, turns.Count);
        Assert.Null(turns[^1].Run);
        Assert.Equal("셋째 것을 마저 해라.", turns[^1].Ask);
        Assert.True(turns[^1].IsLast);
    }

    /// <summary>
    /// 돌고 있는 차수가 있으면 <b>빈 차수를 덧붙이지 않는다</b> — 그 실행 줄이 곧 이 차수다.
    /// </summary>
    [Fact]
    public void 도는_중에는_빈_차수를_덧붙이지_않는다()
    {
        var running = Run(2, "둘째");
        running.RunStatus = "running";
        running.FinishedAt = null;

        var turns = AiTaskTurns.Build(Task("running"), [running, Run(1, "첫째")]);

        Assert.Equal(2, turns.Count);
        Assert.NotNull(turns[^1].Run);
    }

    /// <summary>
    /// 실행 줄에 지시문이 안 남은 옛 건. <b>한 번만 돈 건이면</b> 지금 본문이 그때 간 글이다.
    /// </summary>
    [Fact]
    public void 지시문이_없는_옛_건은_한_번_돈_것만_본문으로_메운다()
    {
        var task = Task("succeeded");
        task.Contents = "그때 적어 보낸 글";

        var one = Run(1, instruction: null);

        Assert.Equal("그때 적어 보낸 글", AiTaskTurns.Build(task, [one]).Single().Ask);

        // 이어서 지시한 건은 본문이 **마지막 차수의 것으로 덮여** 있어
        // 끌어다 쓰면 1차 자리에 2차 글이 앉는다.
        var two = AiTaskTurns.Build(task, [Run(2, "둘째"), Run(1, instruction: null)]);

        Assert.Null(two[0].Ask);
    }

    /// <summary>
    /// 보낼 때 기계가 붙인 문구는 <b>본문에서 걷는다</b>(<c>AiTaskAlways.Strip</c>).
    /// </summary>
    /// <remarks>
    /// 사람이 적은 것이 아니고 건마다 똑같아서, 안 걷으면 <b>적은 적도 없는
    /// 네 문단이 제가 적은 두 줄보다 길게</b> 선다 — 차수를 셋 쌓은 건에서는
    /// 그것이 세 번 선다.
    /// </remarks>
    [Fact]
    public void 늘_따라붙는_문구는_본문에서_걷는다()
    {
        var (ask, lead) = AiTaskAsk.Split(
            AiTaskAlways.Append("목록 화면의 정렬을 날짜 역순으로 바꿔라."));

        Assert.Equal("목록 화면의 정렬을 날짜 역순으로 바꿔라.", ask);
        Assert.Null(lead);
    }

    /// <summary>
    /// <b>옛 판의 문구도 걷는다.</b> 표에 쌓인 지시문은 그때그때의 판을 그대로
    /// 들고 있다 — 지금 글과 통째로 견주면 옛 건에서는 하나도 안 걷힌다.
    /// </summary>
    [Fact]
    public void 문단이_덜_붙던_옛_판도_걷는다()
    {
        // 2026-09-24 의 첫 판 — 커밋·push 와 DB 두 문단뿐이었다.
        var old = string.Join("\n\n", AiTaskAlways.Blocks[0], AiTaskAlways.Blocks[2]);

        var (ask, _) = AiTaskAsk.Split($"쪽수를 20개로 늘려라.\n\n{old}");

        Assert.Equal("쪽수를 20개로 늘려라.", ask);
    }

    /// <summary>이어서 지시한 차수의 본문에서도 걷는다.</summary>
    [Fact]
    public void 이어서_지시한_차수에서도_걷는다()
    {
        var (ask, lead) = AiTaskAsk.Split(
            "## 이전까지의 진행\n\n### 1차\n\n지시: 정렬을 바꿔라\n\n"
            + "## 이번에 할 일\n\n"
            + AiTaskAlways.Append("쪽수도 20개로 늘려라."));

        Assert.Equal("쪽수도 20개로 늘려라.", ask);
        Assert.StartsWith("## 이전까지의 진행", lead);
    }

    /// <summary>
    /// <b>접어 둔 글에는 문구가 남는다.</b> 그쪽은 그때 실행기에게 실제로 간
    /// 글이라, 「왜 AI 가 그렇게 했나」를 되짚을 때 빠져 있으면 안 된다.
    /// </summary>
    [Fact]
    public void 앞에_덧댄_글에는_문구가_그대로_남는다()
    {
        var (ask, lead) = AiTaskAsk.Split(
            AiTaskAlways.Append("목록 정렬을 바꿔라")
            + "\n\n---\n## 재시도 추가 지시\n\n"
            + AiTaskAlways.Append("이번에는 테스트까지 돌려라."));

        Assert.Equal("이번에는 테스트까지 돌려라.", ask);
        Assert.Contains(AiTaskAlways.Blocks[0], lead, StringComparison.Ordinal);
    }

    /// <summary>
    /// 문구밖에 없는 글은 <b>통째로 둔다.</b> 걷고 나서 아무것도 안 남으면
    /// 화면에 「적어 둔 내용이 없습니다」가 서는데, 그것은 보낸 적이 없다는
    /// 말이라 거짓이다.
    /// </summary>
    [Fact]
    public void 문구밖에_없는_글은_통째로_둔다()
    {
        var (ask, _) = AiTaskAsk.Split(AiTaskAlways.Text);

        Assert.Equal(AiTaskAlways.Text, ask);
    }

    private static AiTaskDto Task(string status) => new()
    {
        TaskKey = 1,
        TaskStatus = status,
        RequestFlag = "none",
        Contents = "적어 보낸 글",
    };

    private static AiTaskRunDto Run(int seq, string? instruction) => new()
    {
        RunKey = 100 + seq,
        Seq = seq,
        RunStatus = "succeeded",
        StartedAt = new DateTime(2026, 10, 15, 0, seq, 0, DateTimeKind.Utc),
        FinishedAt = new DateTime(2026, 10, 15, 0, seq, 30, DateTimeKind.Utc),
        Instruction = instruction,
        ResultText = $"{seq}차 답",
    };
}
