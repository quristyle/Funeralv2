using System.Text.RegularExpressions;
using Xunit;

namespace JSini.Web.Architecture.Tests;

/// <summary>
/// 요청 등록 화면의 <b>임시 보관</b>(<c>js/request-draft.js</c>)이 조용히
/// 끊기지 않게 지킨다.
///
/// <para>
/// [왜 글자로 검사하나]
/// </para>
///
/// <para>
/// 이 기능은 <b>끊어져도 화면이 멀쩡하다.</b> 빌드도 통과하고 예외도 안 나고
/// 로그도 안 남는다 — 새로고침했을 때 쓰던 글이 없을 뿐이다. 그리고 그것을
/// 보는 사람은 <b>이미 글을 잃은 뒤</b>다.
/// </para>
///
/// <para>
/// 어긋날 자리가 언어 둘에 흩어져 있다. 화면(C#)은 이름표를 <c>class</c> 로
/// 붙이고 JS 는 그 이름으로 칸을 찾는다. 이름을 한쪽만 고치면
/// <c>attach</c> 가 거짓을 주고 <b>아무 일도 일어나지 않는다</b> — 컴파일러가
/// 대조해 줄 수 없는 종류다(<see cref="JsIdentifierTests"/> 와 같은 함정이다).
/// </para>
/// </summary>
public sealed class RequestDraftTests
{
    /// <summary>
    /// 화면이 부르는 JS 이름이 <b>모듈에 다 있는가</b>.
    /// </summary>
    /// <remarks>
    /// 없는 이름을 부르면 브라우저 콘솔에만 찍히고 화면은 멀쩡하다.
    /// </remarks>
    [Fact]
    public void 임시보관_화면이_부르는_이름이_모듈에_있다()
    {
        var called = Regex.Matches(Page(), @"_draftJs\.Invoke(?:Void)?Async(?:<[^>]*>)?\(\s*""(?<name>[^""]+)""")
            .Select(m => m.Groups["name"].Value)
            .Distinct()
            .ToList();

        Assert.NotEmpty(called);

        var exported = Regex.Matches(Script(), @"export function (?<name>\w+)")
            .Select(m => m.Groups["name"].Value)
            .ToHashSet(StringComparer.Ordinal);

        foreach (var name in called)
        {
            Assert.True(exported.Contains(name),
                $"request-draft.js 가 '{name}' 을(를) 내놓지 않는다. 부르면 콘솔에만 찍히고 화면은 멀쩡하다.");
        }
    }

    /// <summary>
    /// JS 에 넘기는 <b>이름표가 화면에 실제로 붙어 있는가</b>.
    /// </summary>
    /// <remarks>
    /// 제목 칸 쪽이 특히 잘 끊긴다 — 그 이름표는 <b>오직 이 기능 때문에</b>
    /// 붙어 있어서, 화면을 손보는 사람 눈에는 지워도 되는 글자로 보인다.
    /// </remarks>
    [Fact]
    public void 임시보관이_짚는_이름표가_화면에_붙어_있다()
    {
        var page = Page();

        foreach (var name in new[] { "TitleSelector", "EditorSelector" })
        {
            var declared = Regex.Match(page, name + @"\s*=\s*""\.(?<css>[\w-]+)""");
            Assert.True(declared.Success, $"{name} 이 클래스 이름 한 개가 아니다.");

            var css = declared.Groups["css"].Value;

            Assert.True(Regex.IsMatch(page, @"(class|CssClass)=""[^""]*\b" + css + @"\b"),
                $"화면에 `{css}` 가 붙은 칸이 없다. JS 가 그 칸을 못 찾으면 "
                + "임시 보관이 **아무 말 없이** 안 된다.");
        }
    }

    /// <summary>
    /// 등록에 성공하면 <b>적어 둔 것을 버리는가</b>.
    /// </summary>
    /// <remarks>
    /// 남겨 두면 다음에 이 화면을 열 때 <b>이미 등록한 글이 되살아나</b>
    /// 같은 요청이 두 번 들어간다. 임시 보관이 만드는 유일한 새 사고다.
    /// </remarks>
    [Fact]
    public void 등록에_성공하면_적어_둔_것을_버린다()
    {
        Assert.Matches(
            @"CreateAsync\([\s\S]{0,600}?ForgetDraftAsync\(andStop:\s*true\)",
            Page());
    }

    /// <summary>
    /// 열쇠에 <b>로그인한 사람</b>이 들어가는가.
    /// </summary>
    /// <remarks>
    /// 빠지면 공용 PC 에서 <b>남이 쓰다 만 글이 내 화면에 뜬다.</b> 헬프데스크에
    /// 오는 글에는 고객사 이름과 장애 내용이 들어 있다.
    /// </remarks>
    [Fact]
    public void 임시보관_열쇠는_사람마다_다르다()
    {
        Assert.Matches(@"DraftKeyPrefix\s*\+[\s\S]{0,200}?JsiniUserId|JsiniUserId[\s\S]{0,200}?DraftKeyPrefix\s*\+", Page());
    }

    private static string Page() => RazorSource.Read(Path.Combine(
        SolutionRoot(), "src", "Apps", "JSini.Web.HelpDesk", "Components", "Pages", "RequestNew.razor"));

    private static string Script() => RazorSource.Read(Path.Combine(
        SolutionRoot(), "src", "Apps", "JSini.Web.HelpDesk", "wwwroot", "js", "request-draft.js"));

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
