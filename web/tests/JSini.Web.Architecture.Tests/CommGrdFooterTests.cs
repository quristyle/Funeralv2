using System.Text.RegularExpressions;
using Xunit;

namespace JSini.Web.Architecture.Tests;

/// <summary>
/// <c>CommGrd</c> · <c>CommTree</c> 의 아래 띠(<c>FooterLeft</c> · <c>FooterRight</c>)에
/// <b>동작</b>을 두지 않는다.
///
/// <para>
/// [왜 막는가]
/// </para>
///
/// <para>
/// 표의 동작(등록 · 다시 읽기 · 칸별 검색 · 엑셀)은 오른쪽 클릭 창에 있다. 그런데
/// 화면이 띠에 단추를 따로 두면 <b>같은 표의 동작이 두 자리로 갈린다</b> —
/// 회사 관리의 사용자 탭이 그랬다(다시 읽기는 창에, 「사람 넣기」는 띠에).
/// 화면만의 동작은 <c>ContextMenuItems</c> 안에 <c>CommMenuItem</c> 으로 적는다.
/// 띠에는 「저장 중…」 같은 누를 것이 아닌 표시만 남긴다.
/// </para>
///
/// </summary>
public sealed class CommGrdFooterTests
{
    private static readonly Regex Footer = new(
        @"<(FooterLeft|FooterRight)>([\s\S]*?)</\1>", RegexOptions.Compiled);

    private static readonly Regex Action = new(
        @"<(DxButton|button|a)\b", RegexOptions.Compiled);

    [Fact]
    public void 표와_나무의_아래_띠에_동작을_두지_않는다()
    {
        var offenders = new List<string>();

        foreach (var file in RazorFiles())
        {
            var text = RazorSource.Read(file);

            foreach (Match footer in Footer.Matches(text))
            {
                if (!Action.IsMatch(footer.Groups[2].Value) || OwnerOf(text, footer.Index) is null)
                {
                    continue;
                }

                var line = text.Take(footer.Index).Count(c => c == '\n') + 1;
                offenders.Add($"{Path.GetFileName(file)}:{line}  <{footer.Groups[1].Value}>");
            }
        }

        Assert.True(
            offenders.Count == 0,
            "CommGrd · CommTree 의 아래 띠에 단추·링크가 있습니다. ContextMenuItems 안의 CommMenuItem 으로 옮기십시오.\n  "
            + string.Join("\n  ", offenders));
    }

    /// <summary>그 띠가 어느 부품 것인가 — 앞에서 가장 가까운, 아직 안 닫힌 여는 태그.</summary>
    private static string? OwnerOf(string text, int index)
    {
        var before = text[..index];
        string? owner = null;
        var best = -1;

        foreach (var name in new[] { "CommGrd", "CommTree" })
        {
            var open = before.LastIndexOf($"<{name}", StringComparison.Ordinal);
            var close = before.LastIndexOf($"</{name}>", StringComparison.Ordinal);

            if (open > close && open > best)
            {
                best = open;
                owner = name;
            }
        }

        return owner;
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
