using System.Text.RegularExpressions;
using Xunit;

namespace JSini.Web.Architecture.Tests;

/// <summary>
/// 동작의 결과는 <b>토스트 한 곳</b>으로만 나간다.
///
/// <para>
/// [왜 막는가]
/// </para>
///
/// <para>
/// 「저장했습니다」 · 「지우지 못했습니다」를 화면 132개가 각자
/// <c>&lt;PageNotice Text="@Notice" Tone="@Tone" /&gt;</c> 로 그리고 있었다.
/// 그것을 <c>Toasts</c> 로 옮겼는데, 막아 두지 않으면 <b>새 화면이 옛 줄을
/// 다시 들고 온다</b> — 옆 화면을 보고 베끼는 것이 화면을 만드는 제일 흔한
/// 방법이라서다.
/// </para>
///
/// <para>
/// 그렇게 되면 어떤 화면은 토스트로, 어떤 화면은 안내 줄로 같은 말을 하게
/// 되고 <b>파일만 보면 둘 다 정상으로 보인다.</b>
/// </para>
///
/// <para>
/// [안내 줄 자체를 막는 것이 아니다]
/// </para>
///
/// <para>
/// <c>PageNotice</c> 는 그대로 쓴다 — 「왼쪽에서 메뉴를 고르십시오」처럼
/// <b>사라지면 안 되는</b> 문구가 그쪽이다. 여기서 막는 것은 <c>DataPage</c>
/// 의 결과 갈래(<c>@Notice</c> · <c>@Tone</c>)를 안내 줄로 되돌리는 것뿐이다.
/// </para>
/// </summary>
public sealed class ToastTests
{
    /// <summary>
    /// 옛 결과 줄. 속성 차례는 보지 않고 <c>@Notice</c> 를 싣는지만 본다.
    ///
    /// <para>
    /// <b><c>@Tone</c> 만으로는 잡지 않는다.</b> 안내 줄을 감싸는 부품이
    /// 자기 파라미터를 그 이름으로 넘기는 자리가 있어서
    /// (<c>ProjMng/Components/Shared/Notice.razor</c>), 그것까지 잡으면
    /// 테스트가 엉뚱한 것을 가리킨다. 결과 갈래의 표시는 <c>@Notice</c> 다.
    /// </para>
    /// </summary>
    private static readonly Regex ResultNotice = new(
        @"<PageNotice\b[^>]*""@Notice\b",
        RegexOptions.Compiled);

    [Fact]
    public void 화면이_결과를_안내줄로_그리지_않는다()
    {
        var offenders = RazorFiles()
            .Where(f => ResultNotice.IsMatch(File.ReadAllText(f)))
            .Select(Relative)
            .ToArray();

        Assert.True(
            offenders.Length == 0,
            "동작의 결과는 `PageNotice` 가 아니라 토스트로 나간다. "
            + "`DataPage` 가 `RunAsync`·`LoadAsync`·`Say` 에서 알아서 띄우므로 "
            + "화면에서는 그 줄을 지우면 된다. 사라지면 안 되는 안내라면 그 화면의 "
            + "값으로 직접 그린다(`PageNotice` 머리말).\n  "
            + string.Join("\n  ", offenders));
    }

    /// <summary>
    /// 토스트를 그리는 판(<c>DxToastProvider</c>)은 <b>레이아웃 한 곳</b>이다.
    ///
    /// <para>
    /// 화면마다 놓으면 화면을 옮길 때 뜬 것이 함께 사라지고(저장하고 바로
    /// 옮기면 결과를 못 본다), 레이아웃과 겹쳐 두 벌이 뜨는 화면도 생긴다.
    /// </para>
    /// </summary>
    [Fact]
    public void 토스트_판은_레이아웃_한_곳이다()
    {
        var hosts = RazorFiles()
            .Where(f => File.ReadAllText(f).Contains("<DxToastProvider", StringComparison.Ordinal))
            .Select(Relative)
            .OrderBy(f => f, StringComparer.Ordinal)
            .ToArray();

        Assert.Equal(["src/Shared/JSini.Web.Components/Layout/MainLayout.razor"], hosts);
    }

    /// <summary>
    /// 토스트를 손으로 그리지 않는다.
    ///
    /// <para>
    /// 한 번 직접 만들었다가 걷어냈다 — DevExpress 가 테마 스물둘의 색과
    /// 고른 크기를 이미 따라가는데, 손으로 만든 판은 그것을 따로 좇아야 하고
    /// 테마를 올릴 때 <b>토스트만 옛 색으로 남는</b> 쪽으로 어긋난다.
    /// 흔적(옛 CSS 이름)이 되살아나는 것을 막는다.
    /// </para>
    /// </summary>
    [Fact]
    public void 토스트를_손으로_그리지_않는다()
    {
        var css = Path.Combine(
            SolutionRoot(), "src", "Shared", "JSini.Web.Components", "wwwroot", "app.css");

        Assert.DoesNotContain("jsini-toast", File.ReadAllText(css), StringComparison.Ordinal);
    }

    private static string Relative(string path) =>
        Path.GetRelativePath(SolutionRoot(), path).Replace(Path.DirectorySeparatorChar, '/');

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
