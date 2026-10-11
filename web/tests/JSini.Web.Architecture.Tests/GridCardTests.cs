using Xunit;

namespace JSini.Web.Architecture.Tests;

/// <summary>
/// 휴대폰에서 <b>표를 카드로 펴는</b> 약속을 지킨다.
///
/// <para>
/// 이 장치는 브라우저(CSS)와 서버(C#)가 <b>글자로 맞잡고 있다</b> —
/// 클래스 이름 하나, 속성 이름 하나. 한쪽만 고쳐도 빌드는 통과하고,
/// 증상은 <b>그 표만 이름 없는 카드가 되거나 카드가 아예 안 서는 것</b>이라
/// 넓은 화면에서는 아무 표도 안 난다.
/// </para>
/// </summary>
public sealed class GridCardTests
{
    /// <summary>카드 규칙이 적힌 곳. 여기 없으면 어떤 표도 안 펴진다.</summary>
    private static string AppCss() =>
        File.ReadAllText(Path.Combine(
            SolutionRoot(), "src", "Shared", "JSini.Web.Components", "wwwroot", "app.css"));

    [Fact]
    public void 카드_규칙이_app_css_에_있다()
    {
        var css = AppCss();

        Assert.Contains(".commgrd--cards", css);
        Assert.Contains("attr(data-caption)", css);

        // 경계는 셸의 휴대폰 경계와 같은 767px 다. 어긋나면 화면은 휴대폰
        // 모양인데 표만 표로 서 있는 폭이 생긴다.
        Assert.Contains("@media (max-width: 767px)", css);
    }

    /// <summary>
    /// 칸 이름은 <c>::after</c> 로 그린다. <c>::before</c> 는 DevExpress 가
    /// <b>고른 줄의 바탕을 칠하는 데</b> 쓰고 자릿수가 우리보다 높아서,
    /// 거기에 이름을 담으면 <b>고른 카드에서만 이름이 통째로 사라진다</b>
    /// (2026-10-11 에 재어 보고 알았다).
    /// </summary>
    [Fact]
    public void 칸_이름은_after_로_그린다()
    {
        var css = AppCss();

        var index = css.IndexOf("attr(data-caption)", StringComparison.Ordinal);
        Assert.True(index > 0, "app.css 에 칸 이름을 그리는 규칙이 없습니다.");

        // 그 선언이 속한 선택자를 거슬러 찾는다.
        var open = css.LastIndexOf('{', index);
        var selectorStart = css.LastIndexOf('}', open) + 1;
        var selector = css[selectorStart..open];

        Assert.Contains("::after", selector);
        Assert.DoesNotContain("::before", selector);
    }

    /// <summary>
    /// 표를 그리는 자리는 셋이고(<c>CommGrd</c> · 헬프데스크 <c>AutoGrid</c> ·
    /// 뉴스속보), <b>셋 다 공용 규칙을 거쳐야 한다.</b> 글자를 손으로 적으면
    /// 한쪽만 고치는 날이 온다.
    /// </summary>
    [Theory]
    [InlineData("src/Shared/JSini.Web.Components/Data/CommGrd.razor")]
    [InlineData("src/Apps/JSini.Web.HelpDesk/Components/Shared/AutoGrid.razor")]
    [InlineData("src/Apps/JSini.Web.Admin/Components/Pages/NewsBreaking.razor")]
    public void 표를_그리는_자리는_공용_규칙을_쓴다(string relative)
    {
        var source = RazorSource.Read(Path.Combine(SolutionRoot(), relative.Replace('/', Path.DirectorySeparatorChar)));

        Assert.Contains("GridCards.CssClass", source);
        Assert.Contains("GridCards.Label", source);
    }

    /// <summary>
    /// <b>맨 <c>DxGrid</c> 를 새로 들이지 않는다.</b> 들이려면 표시를 손으로
    /// 달아야 하고(위 셋이 그렇다), 안 달면 그 화면만 휴대폰에서 가로로 구른다.
    /// </summary>
    [Fact]
    public void 표시_없는_맨_DxGrid_가_없다()
    {
        var offenders = new List<string>();

        foreach (var file in RazorFiles())
        {
            // `CommGrd` 는 표시를 **감싸개에** 단다(`<div class="commgrd …">`) —
            // 그 감싸개가 곧 이 부품이라 여기서 세면 제 발에 걸린다.
            if (Path.GetFileName(file) == "CommGrd.razor")
            {
                continue;
            }

            var text = RazorSource.Read(file);
            var index = 0;

            while ((index = text.IndexOf("<DxGrid ", index, StringComparison.Ordinal)) >= 0)
            {
                var close = text.IndexOf('>', index);
                var tag = close < 0 ? text[index..] : text[index..close];

                if (!tag.Contains("GridCards.CssClass", StringComparison.Ordinal))
                {
                    var line = text.Take(index).Count(c => c == '\n') + 1;
                    offenders.Add($"{Path.GetFileName(file)}:{line}");
                }

                index = close < 0 ? text.Length : close;
            }
        }

        Assert.True(
            offenders.Count == 0,
            "표시 없는 맨 DxGrid 가 있습니다. CommGrd 를 쓰거나 CssClass=\"@GridCards.CssClass\" 와 "
            + "CustomizeElement=\"@GridCards.Label\" 을 다십시오.\n  "
            + string.Join("\n  ", offenders));
    }

    /// <summary>
    /// 카드일 때는 <b>가상 스크롤을 끄고 쪽 넘김을 켠다.</b> 켜 둔 채로 두면
    /// DevExpress 가 전체 높이를 열 배 넘게 잘못 어림해(카드는 줄 높이가 290px)
    /// 굴림 막대가 깨알만 해진다.
    /// </summary>
    [Fact]
    public void 카드에서는_가상_스크롤을_끈다()
    {
        var grid = RazorSource.Read(Path.Combine(
            SolutionRoot(), "src", "Shared", "JSini.Web.Components", "Data", "CommGrd.razor"));

        Assert.Contains("DxLayoutBreakpoint", grid);
        Assert.Contains("ApplyScrollMode", grid);

        // 쪽 넘김 둘은 `@attributes` **뒤**에 있어야 이 부품이 화면의 값을 이긴다.
        var splat = grid.IndexOf("@attributes=\"_forwarded\"", StringComparison.Ordinal);
        Assert.True(splat > 0);
        Assert.True(grid.IndexOf("PagerVisible=\"@_pagerVisible\"", StringComparison.Ordinal) > splat);
        Assert.True(grid.IndexOf("PageSize=\"@_pageSize\"", StringComparison.Ordinal) > splat);

        var auto = RazorSource.Read(Path.Combine(
            SolutionRoot(), "src", "Apps", "JSini.Web.HelpDesk", "Components", "Shared", "AutoGrid.razor"));

        Assert.Contains("DxLayoutBreakpoint", auto);
        Assert.Contains("_isPhone", auto);
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

    private static IEnumerable<string> RazorFiles() =>
        Directory
            .EnumerateFiles(Path.Combine(SolutionRoot(), "src"), "*.razor", SearchOption.AllDirectories)
            .Where(f => !f.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}")
                     && !f.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}"));
}
