using System.Reflection;
using JSini.Web.Components.Data;
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
    /// <b>표를 그리는 자리는 이제 <c>CommGrd</c> 하나다</b>(2026-10-11).
    ///
    /// <para>
    /// 한동안 셋이었다 — 헬프데스크 <c>AutoGrid</c> 와 뉴스속보가 맨
    /// <c>DxGrid</c> 를 직접 그려서 카드 표시를 손으로 달고 있었다. 둘 다
    /// 공통 표 위로 옮겼으므로 글자를 손으로 적는 자리가 없어졌다.
    /// 새로 들이는 것은 아래 <see cref="맨_DxGrid_를_쓰는_화면이_없다"/> 가 막는다.
    /// </para>
    /// </summary>
    [Fact]
    public void 표를_그리는_자리는_공용_규칙을_쓴다()
    {
        var source = RazorSource.Read(Path.Combine(
            SolutionRoot(), "src", "Shared", "JSini.Web.Components", "Data", "CommGrd.razor"));

        Assert.Contains("GridCards.CssClass", source);
        Assert.Contains("GridCards.Label", source);
    }

    /// <summary>
    /// <b>맨 <c>DxGrid</c> 를 쓰는 화면이 하나도 없다.</b>
    ///
    /// <para>
    /// 위 테스트는 「표시를 달았나」만 보므로, 표시를 손으로 달아 두면
    /// 통과한다. 여기서는 <b>감싼 부품을 거치는가</b>를 본다 — 거치지 않으면
    /// 표시 말고도 머리줄의 건수 · 오른쪽 클릭 창 · 줄무늬 · 관리 칸이
    /// 통째로 빠지고, 그것은 화면을 열어 보기 전에는 아무 표도 안 난다.
    /// </para>
    /// </summary>
    [Fact]
    public void 맨_DxGrid_를_쓰는_화면이_없다()
    {
        var offenders = new List<string>();

        foreach (var file in RazorFiles())
        {
            // `CommGrd` 자신이 그 감싼 부품이다 — 여기서 세면 제 발에 걸린다.
            if (Path.GetFileName(file) == "CommGrd.razor")
            {
                continue;
            }

            var text = RazorSource.Read(file);

            for (var i = 0; (i = text.IndexOf("<DxGrid", i, StringComparison.Ordinal)) >= 0; i += 7)
            {
                // `<DxGridDataColumn` · `<DxGridSelectionColumn` 은 칸이라 괜찮다.
                // 이름이 거기서 끝나는 것만 표다.
                var after = i + 7 < text.Length ? text[i + 7] : ' ';

                if (char.IsLetterOrDigit(after))
                {
                    continue;
                }

                var line = text.Take(i).Count(c => c == '\n') + 1;
                offenders.Add($"{Path.GetFileName(file)}:{line}");
            }
        }

        Assert.True(
            offenders.Count == 0,
            "맨 DxGrid 를 쓰는 화면이 있습니다. CommGrd 로 감싸십시오.\n"
            + "(안 감싸면 머리줄 건수 · 오른쪽 클릭 창 · 줄무늬 · 카드 펴기가 통째로 빠집니다)\n  "
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
    }

    /// <summary>
    /// 모바일 환경에서 카드로 펴졌을 때는 「표형태로보기」, 표로 전환되었을 때는
    /// 「카드형태로 변환」 항목이 오른쪽 클릭 창(ContextMenu)에 뜬다.
    /// </summary>
    [Fact]
    public void 모바일_카드_표_전환_메뉴_항목이_CommGrd_에_있다()
    {
        var grid = RazorSource.Read(Path.Combine(
            SolutionRoot(), "src", "Shared", "JSini.Web.Components", "Data", "CommGrd.razor"));

        Assert.Contains("표형태로보기", grid);
        Assert.Contains("카드형태로 변환", grid);
        Assert.Contains("ToggleCardView", grid);
        Assert.Contains("HasCardClass", grid);
    }

    [Fact]
    public async Task 모바일에서_카드와_표_보기를_전환할_수_있다()
    {
#pragma warning disable BL0005
        var grid = new CommGrd<object>
        {
            ShowExcelExport = false,
        };
#pragma warning restore BL0005

        var isPhoneField = typeof(CommGrd<object>).GetField("_isPhone", BindingFlags.NonPublic | BindingFlags.Instance);
        var cardedProp = typeof(CommGrd<object>).GetProperty("Carded", BindingFlags.NonPublic | BindingFlags.Instance);
        var buildMenuMethod = typeof(CommGrd<object>).GetMethod("BuildMenu", BindingFlags.NonPublic | BindingFlags.Instance);

        Assert.NotNull(isPhoneField);
        Assert.NotNull(cardedProp);
        Assert.NotNull(buildMenuMethod);

        // 1. 데스크톱(폰 아님)에서는 전환 항목이 뜨지 않는다.
        isPhoneField!.SetValue(grid, false);
        var desktopItems = (IEnumerable<object>)buildMenuMethod!.Invoke(grid, null)!;
        var desktopTexts = desktopItems.Select(i => (string)i.GetType().GetProperty("Text")!.GetValue(i)!).ToList();
        Assert.DoesNotContain("표형태로보기", desktopTexts);
        Assert.DoesNotContain("카드형태로 변환", desktopTexts);

        // 2. 모바일 환경(폰)이고 기본 MobileCards=true 일 때: 카드로 펴져 있고 「표형태로보기」가 뜬다.
        isPhoneField.SetValue(grid, true);
        Assert.True((bool)cardedProp!.GetValue(grid)!);

        var phoneItems = (IEnumerable<object>)buildMenuMethod.Invoke(grid, null)!;
        var toTableItem = phoneItems.FirstOrDefault(i => (string)i.GetType().GetProperty("Text")!.GetValue(i)! == "표형태로보기");
        Assert.NotNull(toTableItem);

        // 3. 「표형태로보기」 실행: 표 형태로 바뀌고 Carded 가 false 가 된다.
        var runToTable = (Func<Task>)toTableItem!.GetType().GetProperty("Run")!.GetValue(toTableItem)!;
        await runToTable();

        Assert.False((bool)cardedProp.GetValue(grid)!);

        // 4. 표 형태로 바뀐 상태에서는 「카드형태로 변환」이 뜬다.
        var tableModeItems = (IEnumerable<object>)buildMenuMethod.Invoke(grid, null)!;
        var toCardItem = tableModeItems.FirstOrDefault(i => (string)i.GetType().GetProperty("Text")!.GetValue(i)! == "카드형태로 변환");
        Assert.NotNull(toCardItem);

        // 5. 「카드형태로 변환」 실행: 다시 카드 형태로 복귀하고 Carded 가 true 가 된다.
        var runToCard = (Func<Task>)toCardItem!.GetType().GetProperty("Run")!.GetValue(toCardItem)!;
        await runToCard();

        Assert.True((bool)cardedProp.GetValue(grid)!);
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
