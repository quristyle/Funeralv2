using System.Reflection;
using JSini.Web.Components.Data;
using JSini.Web.ProjMng.Api;
using JSini.Web.ProjMng.Components.Pages;
using JSini.Web.ProjMng.Components.Shared;
using Xunit;

namespace JSini.Web.Architecture.Tests;

public sealed class ProjectSchedulerColumnTests
{
    [Fact]
    public void ProjectScheduler_그리드에_시작_종료_기간_알림_요청자_담당자_컬럼이_노출되고_나머지는_숨김처리된다()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "../../../../../src/Apps/JSini.Web.ProjMng/Components/Pages/ProjectScheduler.razor");
        Assert.True(File.Exists(path), $"파일이 존재해야 합니다: {path}");

        var content = File.ReadAllText(path);

        // 노출 컬럼 (시작, 종료, 기간, 알림, 요청자, 담당자)
        Assert.Contains("Caption=\"시작\"", content);
        Assert.Contains("Caption=\"종료\"", content);
        Assert.Contains("Caption=\"기간\"", content);
        Assert.Contains("Caption=\"알림\"", content);
        Assert.Contains("Caption=\"요청자\" Width=\"110\"", content);
        Assert.DoesNotContain("Caption=\"요청자\" Width=\"110\"\n                                              Visible=\"false\"", content);
        Assert.Contains("Caption=\"담당자\" Width=\"130\">", content);
        Assert.DoesNotContain("Caption=\"담당자\" Width=\"130\"\n                                              Visible=\"false\"", content);

        // 종료 컬럼은 숨김(Visible="false")되지 않고 노출되어야 함
        Assert.DoesNotContain("Caption=\"종료\" Width=\"150\"\n                                              FixedPosition=\"@PinnedColumn\"\n                                              Visible=\"false\"", content);

        // 진행상태는 노출하고, 추가대상처럼 누를 때마다 바뀌는 단추로 고친다.
        Assert.Contains("Caption=\"진행상태\" Width=\"125\">", content);
        Assert.Contains("CycleRowState(w)", content);

        // 숨김 컬럼 (구분, 종류, 메모, 원문)
        Assert.Contains("Caption=\"구분\" Width=\"110\"\n                                              Visible=\"false\"", content);
        Assert.Contains("Caption=\"종류\" Width=\"110\"\n                                              Visible=\"false\"", content);
        Assert.Contains("Caption=\"메모\" MinWidth=\"140\"\n                                              TextAlignment=\"GridTextAlignment.Left\"\n                                              Visible=\"false\"", content);
        Assert.Contains("Caption=\"원문\" MinWidth=\"140\"\n                                              TextAlignment=\"GridTextAlignment.Left\"\n                                              Visible=\"false\"", content);
    }

    [Fact]
    public void ProjectScheduler_알림컬럼은_가변너비로_관리컬럼이_오른쪽에_고정된다()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "../../../../../src/Apps/JSini.Web.ProjMng/Components/Pages/ProjectScheduler.razor");
        Assert.True(File.Exists(path), $"파일이 존재해야 합니다: {path}");

        var content = File.ReadAllText(path);

        // 시작, 기간은 왼쪽에 고정되고 알림 컬럼은 고정 Width 없이 가변 너비로 공간을 채워 관리 컬럼이 오른쪽에 고정되도록 함
        Assert.Contains("FieldName=\"@nameof(WbsItemDto.ProcNm)\" Caption=\"알림\" MinWidth=\"180\"", content);
        Assert.DoesNotContain("FieldName=\"@nameof(WbsItemDto.ProcNm)\" Caption=\"알림\" Width=\"240\"", content);

        var commGrdPath = Path.Combine(AppContext.BaseDirectory, "../../../../../src/Shared/JSini.Web.Components/Data/CommGrd.razor");
        Assert.True(File.Exists(commGrdPath), $"CommGrd 파일이 존재해야 합니다: {commGrdPath}");

        var commGrdContent = File.ReadAllText(commGrdPath);
        Assert.Contains("Name=\"@ActionsColumnName\"", commGrdContent);
        Assert.Contains("FixedPosition=\"@(Carded ? GridColumnFixedPosition.None : GridColumnFixedPosition.Right)\"", commGrdContent);
    }

    [Theory]
    [InlineData(null, "")]
    [InlineData(0.0, "0시간")]
    [InlineData(4.0, "4시간")]
    [InlineData(24.0, "24시간")]
    [InlineData(28.0, "28시간")]
    [InlineData(48.0, "48시간")]
    [InlineData(72.5, "72시간 30분")]
    public void HoursText_총기간_시간_포맷_표현을_검증한다(double? hours, string expected)
    {
        var method = typeof(ProjectScheduler).GetMethod("HoursText", BindingFlags.NonPublic | BindingFlags.Static);
        Assert.NotNull(method);

        var result = (string?)method.Invoke(null, [hours]);
        Assert.Equal(expected, result);
    }

    [Fact]
    public void ProjectScheduler_다크모드_인라인_편집창_배경색과_글자색_대비_스타일이_정의되어_있다()
    {
        var cssPath = Path.Combine(AppContext.BaseDirectory, "../../../../../src/Apps/JSini.Web.ProjMng/wwwroot/projmng.css");
        Assert.True(File.Exists(cssPath), $"파일이 존재해야 합니다: {cssPath}");

        var css = File.ReadAllText(cssPath);

        // 고정 흰색 배경(#fff)으로 하드코딩되어 다크 모드 글자색과 충돌하던 문제가 없어야 함
        Assert.DoesNotContain("var(--jsini-bg-surface, #fff)", css);

        // 다크모드 선택자에서 입력박스 배경과 글자색 스타일 정의 여부
        Assert.Contains(":root[data-theme='dark'] .pm-inline-edit", css);
        Assert.Contains(":root[data-bs-theme='dark'] .pm-inline-edit", css);
        Assert.Contains("--dxbl-text-edit-color: var(--tb-text", css);
        Assert.Contains("color: var(--tb-text, var(--jsini-text, #dee2e6));", css);
    }

    [Theory]
    [InlineData("READY", null, "RUNNING")]
    [InlineData("RUNNING", null, "COMP")]
    [InlineData("COMP", "COMP", "DISCARD")]
    [InlineData("DISCARD", null, "READY")]
    public void ProjectScheduler_일정목록_상태가_대기_진행_완료_폐기_순으로_순환한다(
        string state, string? completedState, string expected)
    {
        var item = new WbsItemDto
        {
            WbsState = state,
            DevSdt = state == "READY" ? null : new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            DevEdt = completedState is null ? null : new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
        };
        var method = typeof(ProjectScheduler).GetMethod("NextAgendaStateCode", BindingFlags.NonPublic | BindingFlags.Static);
        Assert.NotNull(method);

        var result = (string?)method.Invoke(null, [item]);

        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData(null, "지연", "대기", "RUNNING")]
    [InlineData("2026-01-01", "지연", "진행중", "COMP")]
    public void ProjectScheduler_일정목록_지연이어도_단추는_실제_상태를_적고_다음_상태로_간다(
        string? devSdt, string expectedName, string expectedToggle, string expectedNext)
    {
        var item = new WbsItemDto
        {
            WbsState = devSdt is null ? "READY" : "RUNNING",
            DevSdt = devSdt is null ? null : DateTime.SpecifyKind(DateTime.Parse(devSdt), DateTimeKind.Utc),
            PlanEdt = new DateTime(2000, 1, 1, 0, 0, 0, DateTimeKind.Utc),
        };

        var name = (string?)typeof(ProjectScheduler).GetMethod("AgendaStateName", BindingFlags.NonPublic | BindingFlags.Static)!.Invoke(null, [item]);
        var toggle = (string?)typeof(ProjectScheduler).GetMethod("AgendaToggleName", BindingFlags.NonPublic | BindingFlags.Static)!.Invoke(null, [item]);
        var next = (string?)typeof(ProjectScheduler).GetMethod("NextAgendaStateCode", BindingFlags.NonPublic | BindingFlags.Static)!.Invoke(null, [item]);

        Assert.Equal(expectedName, name);
        Assert.Equal(expectedToggle, toggle);
        Assert.Equal(expectedNext, next);
    }

    [Fact]
    public void ProjectScheduler_처음부터_오늘목록을_열고_왼쪽_만들기_단추는_없다()
    {
        var razorPath = Path.Combine(AppContext.BaseDirectory, "../../../../../src/Apps/JSini.Web.ProjMng/Components/Pages/ProjectScheduler.razor");
        var codePath = Path.Combine(AppContext.BaseDirectory, "../../../../../src/Apps/JSini.Web.ProjMng/Components/Pages/ProjectScheduler.razor.cs");
        Assert.True(File.Exists(razorPath), $"파일이 존재해야 합니다: {razorPath}");
        Assert.True(File.Exists(codePath), $"파일이 존재해야 합니다: {codePath}");

        var razor = File.ReadAllText(razorPath);
        var code = File.ReadAllText(codePath);

        Assert.Contains("private bool _dayPane = true;", code);
        Assert.DoesNotContain("class=\"pm-gc__create\"", razor);
        Assert.Contains("CycleAgendaStateAsync(w)", razor);
        Assert.Contains("@w.Comm", razor);
    }

    [Fact]
    public void ProjectScheduler_일정보기_상태단추와_수정팝업_상태선택이_대기_진행중_완료_폐기를_쓴다()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "../../../../../src/Apps/JSini.Web.ProjMng/Components/Pages/ProjectScheduler.razor");
        var razor = File.ReadAllText(path);
        var popupPath = Path.Combine(AppContext.BaseDirectory, "../../../../../src/Apps/JSini.Web.ProjMng/Components/Shared/ScheduleEditPopup.razor");
        var popup = File.ReadAllText(popupPath);

        var agendaStart = razor.IndexOf("else if (_view == CalView.Agenda || _isPhone)", StringComparison.Ordinal);
        var schedulerStart = agendaStart < 0
            ? -1
            : razor.IndexOf("else if (_storage is not null)", agendaStart, StringComparison.Ordinal);
        Assert.True(agendaStart >= 0 && schedulerStart > agendaStart);
        Assert.Contains("@ItemRow(w, toggle: true, day: day.Date)", razor[agendaStart..schedulerStart]);
        Assert.Contains("CycleAgendaStateAsync(w)", razor);
        Assert.Contains("Caption=\"진행 상태\"", popup);
        Assert.Contains("Data=\"@StateOptions\"", popup);
        Assert.DoesNotContain("<DxCheckBox", popup);

        var names = ScheduleEditPopup.StateOptions.Select(option => option.Name);
        Assert.Equal(new[] { "대기", "진행중", "완료", "폐기" }, names);
    }

    /// <summary>일정 보기의 제목 칸은 화면 단축키(t·d·w·m·a·←→)를 들은 판 안에 있다 — 글쇠가 번지면 「t」를 칠 때 오늘로 넘어간다.</summary>
    [Fact]
    public void ProjectScheduler_일정보기_제목을_바로_고치는_칸은_글쇠를_판으로_번지지_않게_끊는다()
    {
        var razor = RazorSource.Read(Path.Combine(AppContext.BaseDirectory, "../../../../../src/Apps/JSini.Web.ProjMng/Components/Pages/ProjectScheduler.razor"));

        Assert.Contains("StartTitleEdit(w, day)", razor);
        Assert.Contains("class=\"pm-gc-agenda__title-input\"", razor);
        Assert.Contains("@onkeydown:stopPropagation=\"true\"", razor);
        Assert.Contains("CommitTitleAsync(w)", razor);
    }

    [Fact]
    public void ProjectScheduler_등록수정_창은_공통_ScheduleEditPopup_한_벌이다()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "../../../../../src/Apps/JSini.Web.ProjMng/Components/Pages/ProjectScheduler.razor");
        var razor = File.ReadAllText(path);

        // 달력·목록·표가 같은 창을 연다 — 표의 기본 편집 창(EditFormTemplate)이나 화면 안의 편집 CommPopup 을 두지 않는다.
        Assert.Single(System.Text.RegularExpressions.Regex.Matches(razor, "<ScheduleEditPopup "));
        Assert.DoesNotContain("<EditFormTemplate", razor);
        Assert.DoesNotContain("<WbsEditForm", razor);
        Assert.Contains("OnEditOpen=\"@OnGridEditOpen\"", razor);

        var popupPath = Path.Combine(AppContext.BaseDirectory, "../../../../../src/Apps/JSini.Web.ProjMng/Components/Shared/ScheduleEditPopup.razor");
        var popup = File.ReadAllText(popupPath);
        Assert.Contains("Caption=\"프로젝트\"", popup);
        Assert.Contains("Caption=\"구분\"", popup);
        Assert.Contains("Text=\"오늘로 옮겨 저장\"", popup);
        Assert.Contains("Text=\"복제\"", popup);
        Assert.Contains("Text=\"삭제\"", popup);
    }

    [Theory]
    [InlineData("READY")]
    [InlineData("RUNNING")]
    [InlineData("COMP")]
    public void ProjectScheduler_편집팝업_선택상태를_개발시작종료일로_저장한다(string state)
    {
        var today = AppTime.Today;
        var item = new WbsItemDto
        {
            WbsState = "COMP",
            DevSdt = today.AddDays(-2).ToDateTime(TimeOnly.MinValue),
            DevEdt = today.AddDays(-1).ToDateTime(TimeOnly.MinValue),
        };
        var popup = new ScheduleEditPopup();
        typeof(ScheduleEditPopup).GetField("_form", BindingFlags.NonPublic | BindingFlags.Instance)!.SetValue(popup, item);
        typeof(ScheduleEditPopup).GetField("_formState", BindingFlags.NonPublic | BindingFlags.Instance)!.SetValue(popup, state);

        typeof(ScheduleEditPopup).GetMethod("ApplyFormState", BindingFlags.NonPublic | BindingFlags.Instance)!.Invoke(popup, null);

        switch (state)
        {
            case "READY":
                Assert.Null(item.DevSdt);
                Assert.Null(item.DevEdt);
                break;
            case "RUNNING":
                Assert.Equal(today.AddDays(-2).ToDateTime(TimeOnly.MinValue), item.DevSdt);
                Assert.Null(item.DevEdt);
                break;
            case "COMP":
                Assert.Equal(today.AddDays(-2).ToDateTime(TimeOnly.MinValue), item.DevSdt);
                Assert.Equal(today.AddDays(-1).ToDateTime(TimeOnly.MinValue), item.DevEdt);
                break;
        }
    }

    [Fact]
    public void ProjectScheduler_폐기한_일정은_표의_줄_전체가_폐기_표시를_단다()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "../../../../../src/Apps/JSini.Web.ProjMng/Components/Pages/ProjectScheduler.razor");
        var razor = File.ReadAllText(path);
        var css = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "../../../../../src/Apps/JSini.Web.ProjMng/wwwroot/projmng.css"));

        Assert.Contains("RowCssClass=\"@TableRowClass\"", razor);
        Assert.Contains("tr.pm-row--discard", css);
        Assert.Contains("line-through", css);
        // 밝은 회색 바탕·글자는 다크모드에서 흰 띠가 되거나 묻힌다 — 두 테마 표지 모두에 덮어쓰기가 있어야 한다.
        Assert.Contains(":root[data-theme='dark'] .commgrd .dxbl-grid .dxbl-grid-table > tbody > tr.pm-row--discard", css);
        Assert.Contains(":root[data-bs-theme='dark'] .commgrd .dxbl-grid .dxbl-grid-table > tbody > tr.pm-row--discard", css);
        Assert.Contains(":root[data-bs-theme='dark'] .jsini-badge.pm-badge--discard", css);
        Assert.Contains(":root[data-bs-theme='dark'] .pm-gc-bar.pm-gc-bar--DISCARD", css);

        // 폐기는 개발 날짜를 지우지 않는다 — 되돌리면 그 전 상태(완료)로 돌아간다.
        var w = new WbsItemDto { DevSdt = new DateTime(2026, 10, 1), DevEdt = new DateTime(2026, 10, 2) };
        ScheduleEditPopup.ApplyState(w, "DISCARD", new DateTime(2026, 10, 31));
        Assert.True(w.Discarded);
        Assert.Equal("DISCARD", ScheduleEditPopup.StateCode(w));
        Assert.NotNull(w.DevEdt);

        ScheduleEditPopup.ApplyState(w, "COMP", new DateTime(2026, 10, 31));
        Assert.False(w.Discarded);
        Assert.Equal("COMP", ScheduleEditPopup.StateCode(w));
        Assert.Equal(new DateTime(2026, 10, 2), w.DevEdt);
    }

    [Fact]
    public void ProjectScheduler_표의_진행상태_단추는_대기_진행중_완료_폐기를_돌고_지나간_상태의_날짜를_남기지_않는다()
    {
        var page = new ProjectScheduler();
        var cycle = typeof(ProjectScheduler).GetMethod("CycleRowState", BindingFlags.NonPublic | BindingFlags.Instance)!;
        var devSdt = new DateTime(2026, 10, 1, 0, 0, 0, DateTimeKind.Utc);
        var planEdt = AppTime.TodayDate.AddDays(5);
        // WbsId 0 은 저장을 건너뛴다 — 회로 없이 차례만 본다.
        var w = new WbsItemDto { WbsState = "READY", PlanSdt = AppTime.TodayDate.AddDays(1), PlanEdt = planEdt, DevSdt = null };

        var seen = new List<string>();
        for (var i = 0; i < 5; i++)
        {
            cycle.Invoke(page, [w]);
            seen.Add(ScheduleEditPopup.StateCode(w));
        }

        Assert.Equal(new[] { "RUNNING", "COMP", "DISCARD", "READY", "RUNNING" }, seen);

        // 완료를 지나 폐기에 멈추면 완료가 당긴 계획 종료일이 처음 값으로 돌아와 있어야 한다.
        var page2 = new ProjectScheduler();
        var w2 = new WbsItemDto { WbsState = "RUNNING", DevSdt = devSdt, PlanEdt = planEdt };
        cycle.Invoke(page2, [w2]);
        Assert.Equal("COMP", ScheduleEditPopup.StateCode(w2));
        cycle.Invoke(page2, [w2]);
        Assert.Equal("DISCARD", ScheduleEditPopup.StateCode(w2));
        Assert.Null(w2.DevEdt);
        Assert.Equal(planEdt, w2.PlanEdt);
        Assert.Equal(devSdt, w2.DevSdt);
    }
}
