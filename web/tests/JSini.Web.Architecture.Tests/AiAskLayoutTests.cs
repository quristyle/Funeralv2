using Xunit;

namespace JSini.Web.Architecture.Tests;

/// <summary>
/// 「빠른 지시」(<c>AiAskPanel</c>)의 상단 입력부 고정 및 하단 목록 스크롤 구조를 검사한다.
/// </summary>
public sealed class AiAskLayoutTests
{
    [Fact]
    public void 상단_입력부와_하단_목록_판이_분리되고_목록은_스크롤_영역을_갖는다()
    {
        var razor = PanelRazor();
        var code = PanelCode();

        Assert.Contains("CssClass=\"@WritePaneCss\"", razor, StringComparison.Ordinal);
        Assert.Contains("CssClass=\"@ListPaneCss\"", razor, StringComparison.Ordinal);
        Assert.Contains("class=\"pm-ask__scroll\"", razor, StringComparison.Ordinal);

        var colsAt = razor.IndexOf("class=\"pm-ask__cols\"", StringComparison.Ordinal);
        var scrollAt = razor.IndexOf("class=\"pm-ask__scroll\"", StringComparison.Ordinal);
        var listAt = razor.IndexOf("class=\"pm-ask__list\"", StringComparison.Ordinal);
        var sentinelAt = razor.IndexOf("class=\"pm-ask__more-sentinel\"", StringComparison.Ordinal);

        Assert.True(colsAt >= 0 && scrollAt > colsAt, "단 수 고르개(.pm-ask__cols)가 스크롤 영역 위에 있어야 한다");
        Assert.True(listAt > scrollAt, "목록(.pm-ask__list)이 스크롤 영역(.pm-ask__scroll) 안에 있어야 한다");
        Assert.True(sentinelAt > listAt, "더보기 감지점(.pm-ask__more-sentinel)이 스크롤 영역 안에 있어야 한다");

        Assert.Contains("\"pm-ask__write\"", code, StringComparison.Ordinal);
        Assert.Contains("\"pm-ask__recent\"", code, StringComparison.Ordinal);
    }

    [Fact]
    public void projmng_css_가_입력부를_고정하고_아래_목록을_스크롤시킨다()
    {
        var css = ProjMngCss();

        var containerRule = Rule(css, ".jsini-shell__content:has(> .pm-ask:not(:has(> .pm-ask__view))),");
        Assert.Contains("overflow: hidden", containerRule, StringComparison.Ordinal);

        var recentRule = Rule(css, ".pm-ask__recent {");
        Assert.Contains("flex: 1 1 0", recentRule, StringComparison.Ordinal);
        Assert.Contains("min-height: 0", recentRule, StringComparison.Ordinal);

        var scrollRule = Rule(css, ".pm-ask__scroll {");
        Assert.Contains("flex: 1 1 0", scrollRule, StringComparison.Ordinal);
        Assert.Contains("min-height: 0", scrollRule, StringComparison.Ordinal);
        Assert.Contains("overflow-y: auto", scrollRule, StringComparison.Ordinal);
    }

    [Fact]
    public void 빠른지시_목록의_카드를_누르면_상세화면_주소로_이동한다()
    {
        var code = PanelCode();

        Assert.Contains("Navigation.NavigateTo($\"/projmng/ai/task/{taskKey}\")", code, StringComparison.Ordinal);
        Assert.DoesNotContain("if (!_isPhone)", code, StringComparison.Ordinal);
    }

    [Fact]
    public void 완료건_일괄확인_버튼과_4단_순환_아이콘이_목록_상단에_제공된다()
    {
        var razor = PanelRazor();
        var code = PanelCode();

        Assert.Contains("class=\"pm-ask__batch-btn\"", razor, StringComparison.Ordinal);
        Assert.Contains("ConfirmCompletedBatchAsync", razor, StringComparison.Ordinal);
        Assert.Contains("class=\"pm-ask__col-btn\"", razor, StringComparison.Ordinal);
        Assert.Contains("CycleCols", razor, StringComparison.Ordinal);

        var batchAt = razor.IndexOf("class=\"pm-ask__batch-btn\"", StringComparison.Ordinal);
        var colsAt = razor.IndexOf("class=\"pm-ask__cols\"", StringComparison.Ordinal);
        var scrollAt = razor.IndexOf("class=\"pm-ask__scroll\"", StringComparison.Ordinal);

        Assert.True(batchAt >= 0 && scrollAt > batchAt, "완료건 일괄확인 버튼이 스크롤 영역 위에 있어야 한다");
        Assert.True(colsAt > batchAt, "완료건 일괄확인 버튼이 단 수 버튼보다 좌측(앞)에 있어야 한다");

        Assert.Contains("ColChoices = [1, 2, 3, 4]", code, StringComparison.Ordinal);
        Assert.Contains("ConfirmCompletedBatchAsync()", code, StringComparison.Ordinal);
        Assert.Contains("CycleCols()", code, StringComparison.Ordinal);
    }

    [Fact]
    public void 최근_리스트에서_쓸어내려_새로고침하는_인디케이터와_처리기_연결을_갖는다()
    {
        var razor = PanelRazor();
        var code = PanelCode();
        var css = ProjMngCss();

        // 1. 마크업: 스크롤 영역 안에 pull 인디케이터가 있고 목록 위에 위치한다.
        Assert.Contains("class=\"pm-ask__pull\"", razor, StringComparison.Ordinal);
        Assert.Contains("class=\"pm-ask__pull-inner\"", razor, StringComparison.Ordinal);
        Assert.Contains("class=\"pm-ask__pull-arrow\"", razor, StringComparison.Ordinal);
        Assert.Contains("class=\"pm-ask__pull-spinner\"", razor, StringComparison.Ordinal);
        Assert.Contains("class=\"pm-ask__pull-text\"", razor, StringComparison.Ordinal);

        var scrollAt = razor.IndexOf("class=\"pm-ask__scroll\"", StringComparison.Ordinal);
        var pullAt = razor.IndexOf("class=\"pm-ask__pull\"", StringComparison.Ordinal);
        var listAt = razor.IndexOf("class=\"pm-ask__list\"", StringComparison.Ordinal);

        Assert.True(pullAt > scrollAt, "새로고침 인디케이터(.pm-ask__pull)가 스크롤 영역(.pm-ask__scroll) 안에 있어야 한다");
        Assert.True(listAt > pullAt, "새로고침 인디케이터(.pm-ask__pull)가 카드 목록(.pm-ask__list) 위에 위치해야 한다");

        // 2. C# 코드: 모듈 부착 및 JSInvokable 콜백
        Assert.Contains("ask-pull.js", code, StringComparison.Ordinal);
        Assert.Contains("attachPullRefresh", code, StringComparison.Ordinal);
        Assert.Contains("AttachPullRefreshAsync()", code, StringComparison.Ordinal);
        Assert.Contains("RefreshRecentFromSwipeDownAsync()", code, StringComparison.Ordinal);

        // 3. CSS: pull 인디케이터 스타일 및 애니메이션 정의 (카드 뒤에 묻히지 않도록 z-index 부여 및 캡슐 배지 스타일)
        var pullRule = Rule(css, ".pm-ask__pull {");
        Assert.Contains("--pm-ask-pull-y", pullRule, StringComparison.Ordinal);
        Assert.Contains("overflow: hidden", pullRule, StringComparison.Ordinal);
        Assert.Contains("z-index:", pullRule, StringComparison.Ordinal);
        Assert.Contains(".pm-ask__pull-inner {", css, StringComparison.Ordinal);
        Assert.Contains(".pm-ask__pull.pm-ask__pull--ready", css, StringComparison.Ordinal);
        Assert.Contains(".pm-ask__pull.pm-ask__pull--refreshing", css, StringComparison.Ordinal);
    }

    [Fact]
    public void 상세화면_상단_빠른지시_돌아가기_링크는_제거된다()
    {
        var razor = TaskViewRazor();

        Assert.DoesNotContain("pm-taskview__back", razor, StringComparison.Ordinal);
        Assert.DoesNotContain("← 빠른 지시", razor, StringComparison.Ordinal);
    }

    [Fact]
    public void 최근_목록에서_2단_이상일_때_프로필이_숨겨지고_2차_이상일_때_처리차수_배지가_표시된다()
    {
        var razor = PanelRazor();
        var css = ProjMngCss();

        // 1. 2단 이상일 때 프로필 숨김 (1단에서만 UserFaceMark 노출)
        Assert.Contains("@if (_cols < 2)", razor, StringComparison.Ordinal);
        var faceIfAt = razor.IndexOf("@if (_cols < 2)", StringComparison.Ordinal);
        var faceMarkAt = razor.IndexOf("<UserFaceMark LoginId=\"@t.CreId\" CssClass=\"pm-face\" />", StringComparison.Ordinal);
        Assert.True(faceIfAt >= 0 && faceMarkAt > faceIfAt, "UserFaceMark는 _cols < 2 조건 안에서만 그려져야 한다");

        // 2. 2차 이상일 때 처리차수 배지 표기
        Assert.Contains("@if (t.TurnCount >= 2)", razor, StringComparison.Ordinal);
        Assert.Contains("class=\"pm-ask__turn-badge\"", razor, StringComparison.Ordinal);
        Assert.Contains("@($\"{t.TurnCount}차\")", razor, StringComparison.Ordinal);

        // 3. CSS 규칙 존재
        var turnRule = Rule(css, ".pm-ask__turn-badge {");
        Assert.Contains("white-space: nowrap", turnRule, StringComparison.Ordinal);
        Assert.Contains("border-radius: 999px", turnRule, StringComparison.Ordinal);
    }

    [Fact]
    public void 최근_목록에서_1단부터_4단까지_단별_표현방식이_정상_적용된다()
    {
        var razor = PanelRazor();
        var css = ProjMngCss();

        // 1단: 3번째 줄로 내려가지 않도록 2번째 줄 nowrap 처리, 대상 표시
        var subRule = Rule(css, ".pm-ask__line--sub {");
        Assert.Contains("flex-wrap: nowrap", subRule, StringComparison.Ordinal);
        Assert.Contains("@if (_cols == 1 && TargetName(t) is { } targetName)", razor, StringComparison.Ordinal);

        // 2단: 2줄 유지 및 대상 숨김 (1단에서만 표시)
        Assert.Contains(".pm-ask__list[data-cols='2'] .pm-ask__to", css, StringComparison.Ordinal);

        // 3단: 1번줄 처리시간 숨김, 2번줄 시간, 3번째줄 제목 분리
        Assert.Contains("@if (_cols < 3)", razor, StringComparison.Ordinal);
        var spanIfAt = razor.IndexOf("@if (_cols < 3)", StringComparison.Ordinal);
        var spanAt = razor.IndexOf("<span class=\"pm-ask__span\">@AiTaskWhen.Elapsed(t)</span>", StringComparison.Ordinal);
        Assert.True(spanIfAt >= 0 && spanAt > spanIfAt, "처리시간은 _cols < 3 조건에서만 표시되어야 한다");
        Assert.Contains("class=\"pm-ask__line pm-ask__line--title\"", razor, StringComparison.Ordinal);
        Assert.Contains(".pm-ask__list[data-cols='3'] .pm-ask__span", css, StringComparison.Ordinal);

        // 4단: 1번줄 모델배지·상태만 (차수배지 _cols < 4, 처리시간 _cols < 3), 3~4번줄 제목 2줄 표시
        Assert.Contains("@if (_cols < 4)", razor, StringComparison.Ordinal);
        Assert.Contains("-webkit-line-clamp: 2", css, StringComparison.Ordinal);
        Assert.Contains(".pm-ask__list[data-cols='4'] .pm-ask__title", css, StringComparison.Ordinal);
    }

    [Fact]
    public void Breadcrumb_은_모바일에서_Trail이_없어도_활성탭이_있으면_화면메뉴를_표시한다()
    {
        var razor = BreadcrumbRazor();
        var code = BreadcrumbCode();

        Assert.Contains("else if (IsPhone && ActiveTab is { } activeTab)", razor, StringComparison.Ordinal);
        Assert.Contains("TabMenu.Request(args)", razor, StringComparison.Ordinal);
        Assert.Contains("ActiveTab => Tabs.ActiveTab", code, StringComparison.Ordinal);
        Assert.Contains("Tabs.Changed += OnTabsChanged", code, StringComparison.Ordinal);
    }

    private static string Rule(string css, string selector)
    {
        var at = css.IndexOf(selector, StringComparison.Ordinal);
        Assert.True(at >= 0, $"규칙을 못 찾았다: {selector}");

        var end = css.IndexOf('}', at);
        Assert.True(end > at, $"규칙이 안 닫혔다: {selector}");

        return css[at..end];
    }

    private static string PanelRazor() => RazorSource.Read(Path.Combine(
        SolutionRoot(), "src", "Apps", "JSini.Web.ProjMng", "Components", "Shared", "AiAskPanel.razor"));

    private static string PanelCode() => RazorSource.Read(Path.Combine(
        SolutionRoot(), "src", "Apps", "JSini.Web.ProjMng", "Components", "Shared", "AiAskPanel.razor.cs"));

    private static string ProjMngCss() => RazorSource.Read(Path.Combine(
        SolutionRoot(), "src", "Apps", "JSini.Web.ProjMng", "wwwroot", "projmng.css"));

    private static string TaskViewRazor() => RazorSource.Read(Path.Combine(
        SolutionRoot(), "src", "Apps", "JSini.Web.ProjMng", "Components", "Pages", "AiTaskViewPage.razor"));

    private static string BreadcrumbRazor() => RazorSource.Read(Path.Combine(
        SolutionRoot(), "src", "Shared", "JSini.Web.Components", "Layout", "Breadcrumb.razor"));

    private static string BreadcrumbCode() => RazorSource.Read(Path.Combine(
        SolutionRoot(), "src", "Shared", "JSini.Web.Components", "Layout", "Breadcrumb.razor.cs"));

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
