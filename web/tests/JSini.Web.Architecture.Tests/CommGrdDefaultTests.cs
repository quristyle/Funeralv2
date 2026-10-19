using System.Text.RegularExpressions;
using Xunit;

namespace JSini.Web.Architecture.Tests;

/// <summary>화면이 CommGrd 의 기본값과 같은 값을 다시 적지 않는다 — 적어 두면 기본값을 바꿔도 그 화면만 안 따라온다. 기본값과 다른 값은 그 화면의 뜻이라 적어도 된다.</summary>
public sealed class CommGrdDefaultTests
{
    /// <summary>CommGrd.razor 가 DxGrid 에 주는 값과 자기 파라미터의 기본값. 손으로 옮겨 적지 않고 ReadDefaults 가 부품 파일에서 읽는다.</summary>
    private static readonly Lazy<IReadOnlyDictionary<string, string>> Defaults = new(ReadDefaults);

    /// <summary>화면이 정할 몫이라 대조에서 뺀다 — Data·TItem 과 펼침 칸 둘(DetailRowTemplate 유무로 정해지는 계산값).</summary>
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
            var text = RazorSource.Read(file);

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

    /// <summary>CommGrd.razor 에서 기본값을 읽는다 — DxGrid 여는 태그의 값과 [Parameter] 초기값을 본다. 식(@…)으로 준 것은 계산값이라 뺀다.</summary>
    private static IReadOnlyDictionary<string, string> ReadDefaults()
    {
        var text = RazorSource.Read(Path.Combine(
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

    /// <summary>여는 태그를 통째로 집어낸다. 따옴표 안의 > 는 끝이 아니다(Click="@(() => …)" 같은 값이 흔하다).</summary>
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
