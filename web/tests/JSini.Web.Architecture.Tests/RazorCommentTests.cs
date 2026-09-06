using System.Text.RegularExpressions;
using Xunit;

namespace JSini.Web.Architecture.Tests;

/// <summary>
/// 여는 태그의 <b>속성 목록 안</b>에 Razor 주석을 두지 않는다.
///
/// <para>
/// [왜 테스트로 막는가 - 두 번 밟았다]
/// </para>
///
/// <para>
/// Razor 는 속성 자리에 놓인 주석을 <b>속성 이름으로 삼는다.</b>
/// <c>AddComponentParameter("(주석 글자)", true)</c> 가 만들어지고, 그 이름이
/// 부품의 미매칭 수집으로 흘러 들어가 브라우저에서 이렇게 터진다 —
/// </para>
///
/// <code>
/// InvalidCharacterError: Failed to execute 'setAttribute' on 'Element':
/// '@* ... *@' is not a valid attribute name.
/// </code>
///
/// <para>
/// <b>빌드는 멀쩡히 통과한다.</b> 화면을 열어야 터지고, 회로가 통째로 끊겨서
/// 「화면이 안 열린다」로 나타난다. 원인을 코드에서 찾기 어려운 종류다.
/// </para>
///
/// <para>
/// 설명이 필요하면 <b>태그 위</b>에 적는다. 위에 두면 같은 내용을 그대로
/// 쓸 수 있고 아무 일도 일어나지 않는다.
/// </para>
/// </summary>
public sealed class RazorCommentTests
{
    /// <summary>
    /// 여는 태그 안에서 <c>@*</c> 를 찾는다.
    ///
    /// 태그 이름이 대문자로 시작하는 것(= 부품)만 본다. HTML 요소에서는 같은
    /// 실수가 그냥 무시되므로 굳이 막을 이유가 없고, 막으면 오탐만 는다.
    /// </summary>
    private static readonly Regex CommentInsideOpeningTag =
        new(@"<[A-Z][A-Za-z0-9]*\s[^<>]*?@\*", RegexOptions.Singleline | RegexOptions.Compiled);

    [Fact]
    public void 여는_태그_안에_Razor_주석을_두지_않는다()
    {
        var offenders = new List<string>();

        foreach (var file in RazorFiles())
        {
            var text = File.ReadAllText(file);

            foreach (Match match in CommentInsideOpeningTag.Matches(text))
            {
                var line = text.Take(match.Index).Count(c => c == '\n') + 1;
                offenders.Add($"{Path.GetFileName(file)}:{line}");
            }
        }

        Assert.True(
            offenders.Count == 0,
            "여는 태그의 속성 목록 안에 Razor 주석이 있습니다. 설명은 태그 위로 옮기십시오.\n"
            + "(그대로 두면 주석이 속성 이름이 되어 화면을 열 때 회로가 끊깁니다)\n  "
            + string.Join("\n  ", offenders));
    }

    /// <summary>
    /// 검사할 <c>.razor</c> 파일들. 빌드 산출물은 뺀다.
    ///
    /// 테스트 어셈블리 위치에서 저장소 뿌리를 거슬러 올라가 찾는다 —
    /// 실행 디렉터리가 CI 와 로컬에서 다르기 때문이다.
    /// </summary>
    private static IEnumerable<string> RazorFiles()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);

        while (dir is not null && !Directory.Exists(Path.Combine(dir.FullName, "src")))
        {
            dir = dir.Parent;
        }

        Assert.NotNull(dir);

        return Directory
            .EnumerateFiles(Path.Combine(dir!.FullName, "src"), "*.razor", SearchOption.AllDirectories)
            .Where(f => !f.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}")
                     && !f.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}"));
    }
}
