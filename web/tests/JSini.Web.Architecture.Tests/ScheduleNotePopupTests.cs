using System.Text.RegularExpressions;
using JSini.Web.Components.Layout;
using Xunit;

namespace JSini.Web.Architecture.Tests;

/// <summary>
/// AI 일정 메모 공통 팝업 및 레이아웃 상단 아이콘 구현 검증.
/// 본문작성부와 저장 단추, 프로젝트 선택 항목만 존재하고,
/// 저장 시 AI 가 날짜·제목·요약을 자동으로 채워 일정에 등록한다.
/// </summary>
public sealed class ScheduleNotePopupTests
{
    private static string SolutionRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "jsini.sln")))
        {
            dir = dir.Parent;
        }

        Assert.NotNull(dir);
        return dir!.FullName;
    }

    private static string Read(params string[] parts) =>
        File.ReadAllText(Path.Combine([SolutionRoot(), .. parts]));

    [Fact]
    public void 기본_헤더_아이콘에_schedule_note가_등록되어_있다()
    {
        var icon = HeaderIconManager.DefaultIcons.FirstOrDefault(x => x.IconKey == "schedule_note");

        Assert.NotNull(icon);
        Assert.Equal("AI 일정 메모", icon.Title);
        Assert.Equal("jsini-icon-calendar", icon.Icon);
        Assert.True(icon.DefaultEnabled);
    }

    [Fact]
    public void HeaderTools에_상단_아이콘과_토글_메서드가_연결되어_있다()
    {
        var razor = Read("web", "src", "Shared", "JSini.Web.Components", "Layout", "HeaderTools.razor");
        var code = Read("web", "src", "Shared", "JSini.Web.Components", "Layout", "HeaderTools.razor.cs");

        Assert.Contains("CanShowScheduleNote", razor, StringComparison.Ordinal);
        Assert.Contains("OpenScheduleNote", razor, StringComparison.Ordinal);
        Assert.Contains("jsini-icon-calendar", razor, StringComparison.Ordinal);

        Assert.Contains("ScheduleNoteReveal ScheduleNote", code, StringComparison.Ordinal);
        Assert.Contains("HeaderIcons.IsIconEnabled(\"schedule_note\")", code, StringComparison.Ordinal);
        Assert.Contains("ScheduleNote.Toggle()", code, StringComparison.Ordinal);
    }

    [Fact]
    public void MainLayout에_ScheduleNotePopupHost가_포함되어_있다()
    {
        var layout = Read("web", "src", "Shared", "JSini.Web.Components", "Layout", "MainLayout.razor");
        Assert.Contains("<ScheduleNotePopupHost />", layout, StringComparison.Ordinal);
    }

    [Fact]
    public void JSiniWebApp에_ScheduleNoteReveal이_등록되어_있다()
    {
        var webApp = Read("web", "src", "Shared", "JSini.Web.Components", "JSiniWebApp.cs");
        Assert.Contains("ScheduleNoteReveal", webApp, StringComparison.Ordinal);
    }

    [Fact]
    public void ProjMngModule에_ScheduleNotePopupContent가_등록되어_있다()
    {
        var module = Read("web", "src", "Apps", "JSini.Web.ProjMng", "ProjMngModule.cs");
        Assert.Contains("ScheduleNotePopupContent", module, StringComparison.Ordinal);
        Assert.Contains("ScheduleNotePopup", module, StringComparison.Ordinal);
    }

    [Fact]
    public void ScheduleNotePopup은_프로젝트_본문_저장단추만_존재한다()
    {
        var razor = Read("web", "src", "Apps", "JSini.Web.ProjMng", "Components", "Shared", "ScheduleNotePopup.razor");
        var code = Read("web", "src", "Apps", "JSini.Web.ProjMng", "Components", "Shared", "ScheduleNotePopup.razor.cs");

        // 프로젝트 선택 항목
        Assert.Contains("CodeSelect CodeId=\"projlist\"", razor, StringComparison.Ordinal);

        // 본문 작성부
        Assert.Contains("DxMemo @bind-Text=\"_body\"", razor, StringComparison.Ordinal);

        // 저장 버튼
        Assert.Contains("Click=\"@SaveAsync\"", razor, StringComparison.Ordinal);

        // 상세 입력칸(시작·종료·제목·요약)은 공통 팝업 마크업에 없어야 함 (AI가 자동 처리)
        Assert.DoesNotContain("DxDateEdit", razor, StringComparison.Ordinal);
        Assert.DoesNotContain("DxTextBox", razor, StringComparison.Ordinal);

        // 코드에서 AI DraftAsync 호출 및 WbsClient.CreateAsync 호출 확인
        Assert.Contains("Assist.DraftAsync", code, StringComparison.Ordinal);
        Assert.Contains("Api.CreateAsync", code, StringComparison.Ordinal);
    }

    [Fact]
    public void ScheduleNotePopup_본문작성창에서_Ctrl_Enter_입력시_저장을_수행한다()
    {
        var razor = Read("web", "src", "Apps", "JSini.Web.ProjMng", "Components", "Shared", "ScheduleNotePopup.razor");
        var code = Read("web", "src", "Apps", "JSini.Web.ProjMng", "Components", "Shared", "ScheduleNotePopup.razor.cs");

        // 본문 작성창 영역에 onkeydown 핸들러가 연결되어 있어야 함
        Assert.Contains("@onkeydown=\"@OnKeyDownAsync\"", razor, StringComparison.Ordinal);

        // 코드 비하인드에 OnKeyDownAsync 가 있고, CtrlKey/MetaKey 와 SaveAsync 가 연결되어 있어야 함
        Assert.Contains("OnKeyDownAsync(KeyboardEventArgs", code, StringComparison.Ordinal);
        Assert.Contains("e.CtrlKey", code, StringComparison.Ordinal);
        Assert.Contains("SaveAsync()", code, StringComparison.Ordinal);
    }
}
