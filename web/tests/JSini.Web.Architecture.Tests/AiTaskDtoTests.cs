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
        // 실행이 끝난 건에 이어서 지시 초안을 작성하던 도중 미요청 상태가 되어도
        // 기존 실행 이력이 있는 건은 완전한 초안(삭제 가능한 임시글)으로 취급되지 않아야 한다.
        var continued = new AiTaskDto
        {
            TaskStatus = "idle",
            RequestFlag = "none",
            LastRunKey = 546,
        };

        Assert.False(continued.IsDraft);
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

        Assert.Equal("완료", succeeded.StatusText);
        Assert.Equal("on", succeeded.StatusTone);
    }
}
