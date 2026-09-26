using System.Text.RegularExpressions;
using Xunit;

namespace JSini.Web.Architecture.Tests;

/// <summary>
/// 우리가 쓰는 CSS 의 중괄호가 맞아떨어지는가.
///
/// <para>
/// [왜 이것을 빌드 때 검사하나 — 실제로 운영까지 나갔다]
/// </para>
///
/// <para>
/// <c>app.css</c> 에 닫는 괄호가 <b>하나 더</b> 들어간 적이 있다(구역을 다시
/// 쓰면서 <c>@@media</c> 의 닫는 괄호를 남긴 채 새 블록을 넣었다). CSS 파서는
/// 짝 없는 <c>}</c> 를 만나면 그것을 버리고 회복하는데, 그 과정에서
/// <b>바로 다음 규칙 하나를 통째로 함께 버린다.</b>
/// </para>
///
/// <para>
/// 하필 그 다음 규칙이 <c>#components-reconnect-modal { display: none }</c>
/// 였다. 그래서 <b>모든 화면 바닥에 빈 상자가 하나씩 깔린 채로</b> 배포됐다 —
/// 연결이 끊겼을 때만 떠야 하는 대화상자다.
/// </para>
///
/// <para>
/// <b>아무것도 이것을 잡아 주지 않았다.</b> 빌드가 통과했고(CSS 는 컴파일
/// 대상이 아니다), 아키텍처 테스트도 통과했고, 화면도 열렸다. 사람이 탭을
/// 눌러 보고서야 드러났다. 괄호 세는 일은 기계가 훨씬 잘하므로 여기 둔다.
/// </para>
///
/// <para>
/// <b>DevExpress 테마는 보지 않는다.</b> 우리가 고치는 파일만 센다 —
/// 남의 산출물까지 검사하면 그쪽 판올림에 우리 테스트가 흔들린다.
/// </para>
/// </summary>
public sealed class StyleSheetBraceTests
{
    [Fact]
    public void 우리가_쓰는_CSS_의_중괄호가_맞아떨어진다()
    {
        var broken = new List<string>();

        foreach (var path in StyleSheets())
        {
            var (depth, stray) = Balance(RazorSource.Read(path));
            var name = Path.GetRelativePath(SolutionRoot(), path);

            if (stray.Count > 0)
            {
                broken.Add($"{name}: 짝 없는 '}}' — {stray.Count}개 (줄 {string.Join(", ", stray)})");
            }

            if (depth != 0)
            {
                broken.Add($"{name}: 닫히지 않은 '{{' 가 {depth}개 남았다");
            }
        }

        Assert.True(broken.Count == 0, string.Join("\n", broken));
    }

    /// <summary>
    /// 중괄호를 센다. 주석과 따옴표 안은 빼고 본다 — 아이콘이 data URI 로
    /// 들어 있어서(<c>url("data:image/svg+xml,…")</c>) 그 안의 글자를 세면
    /// 엉뚱한 곳을 가리킨다.
    /// </summary>
    private static (int Depth, List<int> Stray) Balance(string css)
    {
        var depth = 0;
        var line = 1;
        var stray = new List<int>();

        for (var i = 0; i < css.Length; i++)
        {
            var c = css[i];

            switch (c)
            {
                case '\n':
                    line++;
                    continue;

                // 주석. CSS 주석은 겹치지 않으므로 처음 만나는 */ 가 끝이다.
                case '/' when i + 1 < css.Length && css[i + 1] == '*':
                {
                    var end = css.IndexOf("*/", i + 2, StringComparison.Ordinal);
                    var stop = end < 0 ? css.Length : end + 2;
                    line += css.AsSpan(i, stop - i).Count('\n');
                    i = stop - 1;
                    continue;
                }

                case '"':
                case '\'':
                {
                    var quote = c;
                    i++;
                    while (i < css.Length && css[i] != quote)
                    {
                        if (css[i] == '\\') i++;
                        else if (css[i] == '\n') line++;
                        i++;
                    }
                    continue;
                }

                case '{':
                    depth++;
                    continue;

                case '}':
                    if (depth == 0) stray.Add(line);
                    else depth--;
                    continue;
            }
        }

        return (depth, stray);
    }

    private static IEnumerable<string> StyleSheets() =>
        Directory
            .EnumerateFiles(Path.Combine(SolutionRoot(), "src"), "*.css", SearchOption.AllDirectories)
            .Where(p => !p.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}")
                        && !p.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}")
                        // 남의 산출물은 세지 않는다(머리말).
                        && !p.Contains("bootstrap", StringComparison.OrdinalIgnoreCase)
                        && !p.EndsWith(".min.css", StringComparison.OrdinalIgnoreCase))
            .OrderBy(p => p, StringComparer.Ordinal);

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
