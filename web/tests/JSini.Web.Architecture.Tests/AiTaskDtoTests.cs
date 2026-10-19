using JSini.Web.ProjMng.Api;
using Xunit;

namespace JSini.Web.Architecture.Tests;

/// <summary>
/// <see cref="AiTaskDto"/> 파생 상태값 검증.
/// </summary>
public sealed class AiTaskDtoTests
{
    [Fact]
    public void IsDraft_실행이력이_없는_idle_작업만_초안이다()
    {
        var draft = new AiTaskDto
        {
            TaskStatus = "idle",
            RequestFlag = "none",
            LastRunKey = null,
        };

        Assert.True(draft.IsDraft);
        Assert.Equal("작성중", draft.StatusText);
    }

    [Fact]
    public void IsDraft_실행이력이_있는_작업은_idle상태여도_초안이_아니다()
    {
        // 실행 이력이 있는 건은 이어서 지시 초안을 쓰다 미요청이 되어도 완전한 초안(삭제 가능한 임시글)이 아니다. 아직 요청 전이라 큐 대기가 아닌 작성중이다.
        var continued = new AiTaskDto
        {
            TaskStatus = "idle",
            RequestFlag = "none",
            LastRunKey = 546,
        };

        Assert.False(continued.IsDraft);
        Assert.False(continued.IsContinuing);
        Assert.Equal("작성중", continued.StatusText);
        Assert.Equal("off", continued.StatusTone);
    }

    [Fact]
    public void IsContinuing_이어서지시_후_요청되어_queued상태인_작업도_이어서대기_상태이다()
    {
        // 이어서 지시가 요청(RequestAsync)되어 큐에 대기 중인 건
        var queued = new AiTaskDto
        {
            TaskStatus = "queued",
            RequestFlag = "requested",
            LastRunKey = 546,
        };

        Assert.True(queued.IsContinuing);
        Assert.Equal("이어서 대기", queued.StatusText);
        Assert.Equal("warn", queued.StatusTone);
    }

    [Fact]
    public void IsContinuing_신규작업_queued상태는_일반_대기이다()
    {
        var newQueued = new AiTaskDto
        {
            TaskStatus = "queued",
            RequestFlag = "requested",
            LastRunKey = null,
        };

        Assert.False(newQueued.IsContinuing);
        Assert.Equal("대기", newQueued.StatusText);
        Assert.Equal("warn", newQueued.StatusTone);
    }

    [Fact]
    public void IsContinuing_재시도나_계속대기중_작업은_이어서대기가_아니다()
    {
        var retrying = new AiTaskDto
        {
            TaskStatus = "queued",
            RequestFlag = "requested",
            LastRunKey = 546,
            FinishedAt = DateTime.UtcNow,
        };

        Assert.True(retrying.IsRetrying);
        Assert.False(retrying.IsContinuing);
        Assert.Equal("실패·재시도", retrying.StatusText);
        Assert.Equal("err", retrying.StatusTone);

        var unclaimed = new AiTaskDto
        {
            TaskStatus = "queued",
            RequestFlag = "requested",
            LastRunKey = 546,
            FinishedAt = null,
            LastError = "실행기가 집어가지 않았습니다.",
        };

        Assert.True(unclaimed.IsUnclaimed);
        Assert.False(unclaimed.IsContinuing);
        Assert.Equal("계속대기중", unclaimed.StatusText);
        Assert.Equal("err", unclaimed.StatusTone);
    }

    [Fact]
    public void StatusText_완료상태는_완료로_표시된다()
    {
        var succeeded = new AiTaskDto
        {
            TaskStatus = "succeeded",
            RequestFlag = "none",
            LastRunKey = 546,
        };

        Assert.False(succeeded.IsContinuing);
        Assert.Equal("완료", succeeded.StatusText);
        Assert.Equal("on", succeeded.StatusTone);
    }

    [Fact]
    public void TurnTitles_2회이상인_지시의_차수목록을_담을수있다()
    {
        var task = new AiTaskDto
        {
            TaskKey = 100,
            Title = "2차 작업 제목",
            TurnTitles =
            [
                new() { No = 1, Title = "1차 작업 제목", Status = "succeeded" },
                new() { No = 2, Title = "2차 작업 제목", Status = "succeeded" },
            ]
        };

        Assert.Equal(2, task.TurnTitles.Count);
        Assert.Equal(1, task.TurnTitles[0].No);
        Assert.Equal("1차 작업 제목", task.TurnTitles[0].Title);
        Assert.Equal(2, task.TurnTitles[1].No);
        Assert.Equal("2차 작업 제목", task.TurnTitles[1].Title);
    }

    [Fact]
    public void TurnCount_기본값은_1차이고_2차이상일_때만_배지대상이다()
    {
        var task1 = new AiTaskDto();
        Assert.Equal(1, task1.TurnCount);

        var task2 = new AiTaskDto { TurnCount = 2 };
        Assert.True(task2.TurnCount >= 2);
    }
}
