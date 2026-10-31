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
    public void 공통_팝업은_진행중이면_이어서_요청을_예약으로_보낸다()
    {
        var popupText = ReadFile(ContinuePopupPath);
        Assert.Contains("현재 작업이", popupText, StringComparison.Ordinal);
        Assert.Contains("Click=\"@SavePendingContinueAsync\"", popupText, StringComparison.Ordinal);
    }

    /// <summary>
    /// 요청이 끝난 자리에서 <c>Close()</c> 를 부르면 <c>_continuing</c> 에 걸려 창이 안 닫히고,
    /// 글상자만 비워진 채 남아 「새 창이 다시 떴다」로 보였다. 그 자리는 <c>Hide()</c> 로 닫고,
    /// 임시본을 치울 때 글상자는 건드리지 않는다.
    /// </summary>
    [Fact]
    public void 요청이_끝나면_창이_닫히고_글상자를_먼저_비우지_않는다()
    {
        var popupCsText = ReadFile(ContinuePopupPath + ".cs");

        Assert.Contains("private void Hide()", popupCsText, StringComparison.Ordinal);
        Assert.DoesNotContain("ContinueDrafts.ClearAsync(ContinueSelector, t.TaskKey)", popupCsText, StringComparison.Ordinal);
    }

    [Fact]
    public void 부모_재렌더링시_Visible_파라미터_부재로_팝업이_닫히지_않도록_SetParametersAsync_를_재정의한다()
    {
        var popupCsPath = ContinuePopupPath + ".cs";
        var popupCsText = ReadFile(popupCsPath);

        Assert.Contains("override async Task SetParametersAsync(ParameterView parameters)", popupCsText, StringComparison.Ordinal);
        Assert.Contains("parameters.TryGetValue<bool>(nameof(Visible)", popupCsText, StringComparison.Ordinal);
        Assert.Contains("_hasVisibleParam", popupCsText, StringComparison.Ordinal);
        Assert.Contains("OnCommVisibleChanged", popupCsText, StringComparison.Ordinal);
    }

    [Fact]
    public void 이어서_지시_팝업에서_불필요한_동일_AI_설명문구가_제거되었다()
    {
        var popupCsPath = ContinuePopupPath + ".cs";
        var popupCsText = ReadFile(popupCsPath);

        // "지난 회차와 같은 AI 가 이어서 처리합니다." 문구 제거 확인
        Assert.DoesNotContain("지난 회차와 같은 AI 가 이어서 처리합니다.", popupCsText, StringComparison.Ordinal);

        var popupRazorText = ReadFile(ContinuePopupPath);
        Assert.Contains("!string.IsNullOrWhiteSpace(ContinueKindNote)", popupRazorText, StringComparison.Ordinal);
    }

    [Fact]
    public void 공통_팝업_CommPopup은_데스크톱_크기조절과_모바일_화면채움을_지원한다()
    {
        var commPopupCsPath = "src/Shared/JSini.Web.Components/Data/CommPopup.razor.cs";
        var commPopupCsText = ReadFile(commPopupCsPath);

        Assert.Contains("public bool AllowResize { get; set; } = true;", commPopupCsText, StringComparison.Ordinal);
        Assert.Contains("public bool FullOnMobile { get; set; } = true;", commPopupCsText, StringComparison.Ordinal);
        Assert.Contains("public string? Width { get; set; }", commPopupCsText, StringComparison.Ordinal);
        Assert.Contains("public string? Height { get; set; }", commPopupCsText, StringComparison.Ordinal);
        Assert.Contains("public string? MinWidth { get; set; }", commPopupCsText, StringComparison.Ordinal);
        Assert.Contains("public string? MinHeight { get; set; }", commPopupCsText, StringComparison.Ordinal);
        Assert.Contains("jsini-popup--full-mobile", commPopupCsText, StringComparison.Ordinal);

        var commPopupRazorPath = "src/Shared/JSini.Web.Components/Data/CommPopup.razor";
        var commPopupRazorText = ReadFile(commPopupRazorPath);
        Assert.Contains("AllowResize=\"@AllowResize\"", commPopupRazorText, StringComparison.Ordinal);
        Assert.Contains("Height=\"@Height\"", commPopupRazorText, StringComparison.Ordinal);
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



