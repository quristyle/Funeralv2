using Xunit;

namespace JSini.Web.Architecture.Tests;

/// <summary>
/// 「이어서 지시」 팝업 공통화 검증.
///
/// <para>
/// 「빠른 지시」( <c>AiTaskActions</c> )와 「AI 작업」( <c>AiTaskList</c> )의
/// 이어서 지시 팝업이 <c>AiTaskContinuePopup</c> 공통 컴포넌트로 일원화되어
/// 동일한 UI/UX 를 제공하는지 검사한다.
/// </para>
/// </summary>
public sealed class AiTaskContinuePopupTests
{
    private const string TaskListPath = "src/Apps/JSini.Web.ProjMng/Components/Pages/AiTaskList.razor";
    private const string TaskActionsPath = "src/Apps/JSini.Web.ProjMng/Components/Shared/AiTaskActions.razor";
    private const string ContinuePopupPath = "src/Apps/JSini.Web.ProjMng/Components/Shared/AiTaskContinuePopup.razor";

    [Fact]
    public void 모든_이어서_지시는_AiTaskContinuePopup_을_사용한다()
    {
        var taskListText = ReadFile(TaskListPath);
        var taskActionsText = ReadFile(TaskActionsPath);

        Assert.Contains("AiTaskContinuePopup", taskListText, StringComparison.Ordinal);
        Assert.Contains("AiTaskContinuePopup", taskActionsText, StringComparison.Ordinal);
    }

    [Fact]
    public void 개별_화면에_중복_이어서_지시_CommPopup_이_없다()
    {
        var taskListText = ReadFile(TaskListPath);
        var taskActionsText = ReadFile(TaskActionsPath);

        Assert.DoesNotContain("HeaderText=\"이어서 지시\"", taskListText, StringComparison.Ordinal);
        Assert.DoesNotContain("HeaderText=\"이어서 지시\"", taskActionsText, StringComparison.Ordinal);

        var popupText = ReadFile(ContinuePopupPath);
        Assert.Contains("HeaderText=\"이어서 지시\"", popupText, StringComparison.Ordinal);
    }

    [Fact]
    public void 공통_팝업은_AiTaskAlways_Append_를_호출한다()
    {
        var popupText = ReadFile(ContinuePopupPath);
        Assert.Contains("AiTaskAlways.Append", popupText, StringComparison.Ordinal);
    }

    [Fact]
    public void 이어서_지시_단추는_완료뿐만_아니라_IsBusy_상태에서도_표시된다()
    {
        var taskActionsText = ReadFile(TaskActionsPath);
        Assert.Contains("t.TaskStatus == \"succeeded\" || t.IsBusy", taskActionsText, StringComparison.Ordinal);
    }

    [Fact]
    public void 공통_팝업은_진행중_안내와_자동_요청을_지원한다()
    {
        var popupText = ReadFile(ContinuePopupPath);
        Assert.Contains("_autoSubmitOnComplete", popupText, StringComparison.Ordinal);
        Assert.Contains("현재 작업이", popupText, StringComparison.Ordinal);
    }

    private static string ReadFile(string relative)
    {
        var path = Path.Combine(SolutionRoot(), relative.Replace('/', Path.DirectorySeparatorChar));
        Assert.True(File.Exists(path), $"파일이 존재하지 않습니다: {relative}");
        return RazorSource.Read(path);
    }

    private static string SolutionRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);

        while (dir is not null && !Directory.Exists(Path.Combine(dir.FullName, "src")))
        {
            dir = dir.Parent;
        }

        Assert.NotNull(dir);
        return dir!.FullName;
    }
}
