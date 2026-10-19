using System.Text.RegularExpressions;
using Xunit;

namespace JSini.Web.Architecture.Tests;

/// <summary>조건이 있는 조회 판은 접힌 줄에 적을 글자를 함께 준다. 휴대폰에서는 Fields 가 있는 CommSch 가 모두 접히는데 MobileSummary 를 빠뜨려도 아무 일이 없어 눈으로는 못 지킨다.
/// 접지 않기로 한 판(Foldable="false")은 뺀다.</summary>
public sealed class CommSchFoldTests
{
    /// <summary>CommSch 여는 태그. 여러 줄에 걸친 것도 잡는다. CommSchItem 은 빼야 해서 태그 이름 뒤를 공백·> 로 끊는다.</summary>
    private static readonly Regex OpeningTag =
        new(@"<CommSch(?=[\s>])[^>]*>", RegexOptions.Singleline | RegexOptions.Compiled);

    [Fact]
    public void 조건이_있는_조회판은_접힌_줄에_적을_글자를_준다()
    {
        var offenders = new List<string>();

        foreach (var file in RazorFiles())
        {
            // 부품 자신은 뺀다 — 여기 있는 태그는 머리말 속 보기다.
            if (Path.GetFileName(file) is "CommSch.razor" or "CommCont.razor")
            {
                continue;
            }

            var text = RazorSource.Read(file);

            foreach (Match match in OpeningTag.Matches(text))
            {
                var tag = match.Value;

                // 조건 칸이 없는 판(단추만 있는 조작줄)은 접지 않는다.
                if (!HasFields(text, match.Index))
                {
                    continue;
                }

                if (tag.Contains("MobileSummary", StringComparison.Ordinal)
                    || tag.Contains("Foldable=\"false\"", StringComparison.Ordinal))
                {
                    continue;
                }

                var line = text.Take(match.Index).Count(c => c == '\n') + 1;
                offenders.Add($"{Path.GetFileName(file)}:{line}");
            }
        }

        Assert.True(
            offenders.Count == 0,
            "조건이 있는 조회 판에 MobileSummary 가 없습니다. 접힌 머리줄에 적을 글자를\n"
            + "화면이 만들어 주십시오(고른 조건을 ` · ` 로 이은 한 줄, SchSummary 참고).\n"
            + "접지 않는 것이 맞는 판이면 Foldable=\"false\" 로 끄고 그 까닭을 태그 위에 적으십시오.\n  "
            + string.Join("\n  ", offenders));
    }

    /// <summary>이 판에 조건 칸(Fields)이 있는가. 여는 태그 다음부터 닫는 태그 전까지에서 찾는다.</summary>
    private static bool HasFields(string text, int tagIndex)
    {
        var close = text.IndexOf("</CommSch>", tagIndex, StringComparison.Ordinal);
        if (close < 0)
        {
            return false;
        }

        return text.IndexOf("<Fields>", tagIndex, close - tagIndex, StringComparison.Ordinal) >= 0;
    }

    /// <summary>검사할 .razor 파일들. 빌드 산출물은 뺀다(RazorCommentTests 와 같은 길찾기다).</summary>
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
