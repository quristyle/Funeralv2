using System.Reflection;
using System.Text.RegularExpressions;
using DevExpress.Blazor;
using Microsoft.AspNetCore.Components;
using Xunit;

namespace JSini.Web.Architecture.Tests;

/// <summary>DxSplitter 의 판은 반드시 Panes 안에 적는다. ChildContent 파라미터가 없고 Attributes 가 CaptureUnmatchedValues 라, Panes 없이 적은 판들은 짝 없는 값이 되어 조용히 사전으로 빨려 들어간다.
/// 빌드는 통과하고 속이 빈 상자만 남아 「높이가 안 잡혀 접힌 것」과 똑같이 보인다.</summary>
public sealed class DxSplitterPanesTests
{
    [Fact]
    public void DxSplitter_는_ChildContent_를_받지_않는다()
    {
        var parameters = Parameters(typeof(DxSplitter)).Select(p => p.Name).ToHashSet(StringComparer.Ordinal);

        Assert.Contains("Panes", parameters);

        Assert.False(
            parameters.Contains("ChildContent"),
            "DevExpress 가 DxSplitter 에 ChildContent 를 붙였습니다.\n"
            + "그렇다면 <Panes> 를 강제하는 아래 검사도, 화면들의 머리말도 다시 봐야 합니다.");
    }

    [Fact]
    public void 화면이_DxSplitter_의_판을_Panes_밖에_적지_않는다()
    {
        var offenders = new List<string>();

        foreach (var file in RazorFiles())
        {
            var text = MaskComments(RazorSource.Read(file));

            foreach (var (start, tag) in OpeningTags(text, "DxSplitter"))
            {
                // 자기 자신으로 닫는 태그에는 판이 없다.
                if (tag.TrimEnd().EndsWith('/'))
                {
                    continue;
                }

                var rest = SkipTrivia(text, start + tag.Length + 1);

                if (rest.StartsWith("<Panes>", StringComparison.Ordinal))
                {
                    continue;
                }

                var line = text.Take(start).Count(c => c == '\n') + 1;
                offenders.Add($"{Path.GetFileName(file)}:{line}");
            }
        }

        Assert.True(
            offenders.Count == 0,
            "DxSplitter 의 판을 <Panes> 밖에 적었습니다.\n"
            + "(DxSplitter 에는 ChildContent 가 없어서, 그 판들은 Attributes 사전으로\n"
            + " 조용히 빨려 들어가고 화면에는 속이 빈 회색 상자만 남습니다)\n  "
            + string.Join("\n  ", offenders));
    }

    /// <summary>razor 주석(@*…*@) 안을 빈칸으로 덮는다 — 글자 수와 줄바꿈은 그대로 둔다. 덮지 않으면 머리말에 적힌 DxSplitter 글자에 이 검사가 오검출한다.</summary>
    private static string MaskComments(string text)
    {
        var buffer = text.ToCharArray();
        var i = 0;

        while ((i = text.IndexOf("@*", i, StringComparison.Ordinal)) >= 0)
        {
            var end = text.IndexOf("*@", i + 2, StringComparison.Ordinal);
            var stop = end < 0 ? text.Length : end + 2;

            for (var j = i; j < stop; j++)
            {
                if (buffer[j] != '\n' && buffer[j] != '\r')
                {
                    buffer[j] = ' ';
                }
            }

            i = stop;
        }

        return new string(buffer);
    }

    /// <summary>여는 태그 바로 뒤의 빈칸을 건너뛴다.</summary>
    private static string SkipTrivia(string text, int i)
    {
        while (i < text.Length && char.IsWhiteSpace(text[i]))
        {
            i++;
        }

        return text[Math.Min(i, text.Length)..];
    }

    private static IEnumerable<PropertyInfo> Parameters(Type type) =>
        type
            .GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(p => p.IsDefined(typeof(ParameterAttribute), inherit: true));

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
