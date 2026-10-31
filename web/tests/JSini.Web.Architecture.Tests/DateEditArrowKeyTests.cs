using System.Text.RegularExpressions;
using Xunit;

namespace JSini.Web.Architecture.Tests;

/// <summary>날짜 칸은 방향키로 연·월·일을 올리고 내린다. DxDateEdit 의 방향키 조절은 <c>Mask</c> 가 걸렸을 때만 동작해서,
/// 마스크를 빠뜨린 칸은 오류 없이 방향키만 조용히 안 먹는다.</summary>
public sealed class DateEditArrowKeyTests
{
    [Fact]
    public void 모든_DxDateEdit_에_마스크와_방향키_조절이_걸려_있다()
    {
        var offenders = new List<string>();

        foreach (var file in RazorFiles())
        {
            var text = File.ReadAllText(file);

            foreach (var (start, tag) in OpeningTags(text, "DxDateEdit"))
            {
                if (Regex.IsMatch(tag, @"\sMask=""[^""]+""") && tag.Contains("AllowUpDownArrowKeys=\"true\"", StringComparison.Ordinal))
                {
                    continue;
                }

                var line = text.Take(start).Count(c => c == '\n') + 1;
                offenders.Add($"{Path.GetFileName(file)}:{line}");
            }
        }

        Assert.True(
            offenders.Count == 0,
            "DxDateEdit 에 Mask 와 AllowUpDownArrowKeys=\"true\" 가 없습니다.\n"
            + "(마스크가 없으면 방향키로 날짜를 바꿀 수 없습니다 — 날짜만이면 Mask=\"yyyy-MM-dd\",\n"
            + " 시간까지면 Format 과 같은 값을 줍니다)\n  "
            + string.Join("\n  ", offenders));
    }

    /// <summary>여는 태그를 통째로 집어낸다. 따옴표 안의 > 는 끝이 아니다.</summary>
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
