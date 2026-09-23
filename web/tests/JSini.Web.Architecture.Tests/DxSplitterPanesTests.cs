using System.Reflection;
using System.Text.RegularExpressions;
using DevExpress.Blazor;
using Microsoft.AspNetCore.Components;
using Xunit;

namespace JSini.Web.Architecture.Tests;

/// <summary>
/// <c>DxSplitter</c> 의 판은 <b>반드시 <c>&lt;Panes&gt;</c> 안에</b> 적는다.
///
/// <para>
/// [증상이 「회색 빈 칸」 하나라 원인이 안 보인다]
/// </para>
///
/// <para>
/// <c>DxSplitter</c> 에는 <c>ChildContent</c> 파라미터가 <b>없다.</b> 판이
/// 들어갈 자리는 <c>Panes</c> 하나뿐이다. 그런데 이 부품은 <c>Attributes</c>
/// 를 <c>CaptureUnmatchedValues</c> 로 받으므로, <c>&lt;Panes&gt;</c> 없이
/// 적은 판들은 <c>ChildContent</c> 라는 <b>짝 없는 값</b>이 되어 그 사전으로
/// 조용히 빨려 들어간다.
/// </para>
///
/// <para>
/// <b>빌드가 통과하고 예외도 안 난다.</b> 나오는 것은 이것뿐이다 —
/// </para>
///
/// <code>
/// &lt;dxbl-splitter class="dxbl-splitter pm-query-splitter dxbl-splitter-vertical"&gt;&lt;/dxbl-splitter&gt;
/// </code>
///
/// <para>
/// 속이 완전히 빈 상자다. 화면에는 회색 칸만 남고 그 안의 것이 전부 사라진다.
/// 쿼리 테스터(<c>DbTester</c>)의 편집기와 결과 표, <c>SplitPane</c> 을 쓰는
/// 마스터-디테일 화면 셋이 실제로 그 상태였다.
/// </para>
///
/// <para>
/// <b>증상이 「높이가 안 잡혀 판이 접힌 것」과 똑같이 보인다.</b> 그래서
/// CSS 를 먼저 의심하게 되는데, <b>그릴 것이 없으므로</b> CSS 로는 한 걸음도
/// 나아가지 않는다. 실제로 그 길로 한 번 돌아왔다.
/// </para>
/// </summary>
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
            var text = MaskComments(File.ReadAllText(file));

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

    /// <summary>
    /// razor 주석(<c>@*…*@</c>) 안을 빈칸으로 덮는다. <b>글자 수와 줄바꿈은
    /// 그대로 둔다</b> — 자리와 줄 번호가 원본과 어긋나면 안 된다.
    ///
    /// <para>
    /// 덮지 않으면 <b>이 검사 자신이 오검출한다.</b> 이 함정을 밟은 화면 둘이
    /// 머리말에 <c>&lt;DxSplitter&gt;</c> 를 글자로 적어 두었고(왜 그렇게
    /// 두는지를 설명하는 자리다), 고친 뒤에도 그 두 줄이 그대로 걸렸다.
    /// </para>
    /// </summary>
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

    /// <summary>
    /// 여는 태그를 통째로 집어낸다. 따옴표 안의 <c>&gt;</c> 는 끝이 아니다 —
    /// <c>Orientation="@(_narrow ? … : …)"</c> 같은 값이 흔하다.
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
