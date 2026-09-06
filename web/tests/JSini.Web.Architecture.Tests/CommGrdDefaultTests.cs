using System.Text.RegularExpressions;
using Xunit;

namespace JSini.Web.Architecture.Tests;

/// <summary>
/// 화면이 <c>CommGrd</c> 의 <b>기본값과 같은 값</b>을 다시 적지 않는다.
///
/// <para>
/// [왜 막는가]
/// </para>
///
/// <para>
/// 공통 그리드를 둔 이유가 「화면마다 손으로 적지 않기」다. 그런데 기본값을
/// 화면에도 적어 두면 <b>기본값을 바꿔도 그 화면만 안 따라온다.</b> 실제로
/// 칸별검색을 숨김으로 바꿨을 때 37개 화면이 <c>ShowFilterRow="false"</c> 를
/// 들고 있어서, 무엇이 기본값이고 무엇이 그 화면의 뜻인지 구분되지 않았다.
/// </para>
///
/// <para>
/// 기본값과 <b>다른</b> 값은 얼마든지 적어도 된다 — 그것이 그 화면의 뜻이다.
/// 여기서 막는 것은 <b>같은 값</b>뿐이다.
/// </para>
/// </summary>
public sealed class CommGrdDefaultTests
{
    /// <summary>
    /// <c>CommGrd.razor</c> 가 <c>DxGrid</c> 에 주는 값과 자기 파라미터의 기본값.
    ///
    /// 여기를 손으로 옮겨 적는 것이 아니라 <see cref="ReadDefaults"/> 가
    /// 부품 파일에서 읽는다 — 기본값을 고치면 이 테스트가 따라온다.
    /// </summary>
    private static readonly Lazy<IReadOnlyDictionary<string, string>> Defaults = new(ReadDefaults);

    /// <summary>
    /// 화면이 정할 몫이라 대조에서 빼는 것들.
    ///
    /// <list type="bullet">
    /// <item><c>Data</c>·<c>TItem</c> 은 화면마다 다르다.</item>
    /// <item>펼침 칸 둘은 <c>DetailRowTemplate</c> 유무로 정해지는 계산값이다.</item>
    /// </list>
    /// </summary>
    private static readonly HashSet<string> Ignored =
    [
        "Data", "TItem", "DetailRowDisplayMode", "DetailExpandButtonDisplayMode",
    ];

    [Fact]
    public void 화면이_CommGrd_기본값을_다시_적지_않는다()
    {
        var offenders = new List<string>();

        foreach (var file in RazorFiles().Where(f => Path.GetFileName(f) != "CommGrd.razor"))
        {
            var text = File.ReadAllText(file);

            foreach (var tag in OpeningTags(text, "CommGrd"))
            {
                foreach (Match attr in Regex.Matches(tag.Text, "\\s([A-Za-z][A-Za-z0-9]*)=\"([^\"]*)\""))
                {
                    var name = attr.Groups[1].Value;

                    if (Ignored.Contains(name)
                        || !Defaults.Value.TryGetValue(name, out var fallback)
                        || fallback != attr.Groups[2].Value)
                    {
                        continue;
                    }

                    var line = text.Take(tag.Start).Count(c => c == '\n') + 1;
                    offenders.Add($"{Path.GetFileName(file)}:{line}  {name}=\"{fallback}\"");
                }
            }
        }

        Assert.True(
            offenders.Count == 0,
            "CommGrd 의 기본값과 같은 값을 화면에서 다시 적고 있습니다. 지우십시오.\n"
            + "(그대로 두면 기본값을 바꿔도 이 화면들만 따라오지 않습니다)\n  "
            + string.Join("\n  ", offenders));
    }

    /// <summary>
    /// <c>CommGrd.razor</c> 에서 기본값을 읽는다.
    ///
    /// 두 자리를 본다 — <c>DxGrid</c> 여는 태그에 적힌 값과, <c>[Parameter]</c>
    /// 속성의 초기값이다. 식(<c>@…</c>)으로 준 것은 계산값이라 뺀다.
    /// </summary>
    private static IReadOnlyDictionary<string, string> ReadDefaults()
    {
        var text = File.ReadAllText(Path.Combine(
            SolutionRoot(), "src", "Shared", "JSini.Web.Components", "Data", "CommGrd.razor"));

        var found = new Dictionary<string, string>(StringComparer.Ordinal);

        var grid = OpeningTags(text, "DxGrid").FirstOrDefault();
        Assert.NotNull(grid.Text);

        foreach (Match attr in Regex.Matches(grid.Text, "\\s([A-Za-z][A-Za-z0-9]*)=\"([^\"@]*)\""))
        {
            found[attr.Groups[1].Value] = attr.Groups[2].Value;
        }

        foreach (Match p in Regex.Matches(
            text, """\[Parameter\]\s*public\s+\S+\s+(\w+)\s*\{[^}]*\}\s*=\s*(?:"([^"]*)"|(true|false|\d+));"""))
        {
            found[p.Groups[1].Value] = p.Groups[2].Success ? p.Groups[2].Value : p.Groups[3].Value;
        }

        Assert.NotEmpty(found);
        return found;
    }

    /// <summary>
    /// 여는 태그를 통째로 집어낸다. 따옴표 안의 <c>&gt;</c> 는 끝이 아니다 —
    /// <c>Click="@(() =&gt; …)"</c> 같은 값이 흔하다.
    /// </summary>
    private static IEnumerable<(int Start, string Text)> OpeningTags(string text, string name)
    {
        foreach (Match open in Regex.Matches(text, $@"<{name}\b"))
        {
            var i = open.Index + open.Length;
            var quoted = false;

            while (i < text.Length && (quoted || text[i] != '>'))
            {
                if (text[i] == '"')
                {
                    quoted = !quoted;
                }

                i++;
            }

            yield return (open.Index, text[open.Index..Math.Min(i, text.Length)]);
        }
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
