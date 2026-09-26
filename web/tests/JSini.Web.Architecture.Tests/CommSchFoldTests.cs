using System.Text.RegularExpressions;
using Xunit;

namespace JSini.Web.Architecture.Tests;

/// <summary>
/// 조건이 있는 조회 판은 <b>접힌 줄에 적을 글자를 함께 준다.</b>
///
/// <para>
/// [왜 테스트로 막는가]
/// </para>
///
/// <para>
/// 휴대폰(≤767px)에서는 <c>Fields</c> 가 있는 <c>CommSch</c> 가 모두 접힌다.
/// 접히는 것 자체는 <c>MobileSummary</c> 와 무관하다 — 안 줘도 접힌다. 그래서
/// <b>빠뜨려도 아무 일도 일어나지 않는다.</b> 머리줄에 「조회 조건 ⌄」만 남고,
/// 지금 무엇으로 걸러 본 목록인지 알려면 매번 펴 보아야 한다 — 접은 값이
/// 절반은 사라진다. 화면이 여든이 넘어 눈으로는 못 지킨다.
/// </para>
///
/// <para>
/// 접지 않기로 한 판(<c>Foldable="false"</c>)은 적을 자리가 없으므로 뺀다.
/// 조건이 한 칸뿐이거나, 그 칸이 거르개가 아니라 화면의 주 조작인 판들이다 —
/// 그 까닭은 화면마다 태그 위에 적혀 있다.
/// </para>
/// </summary>
public sealed class CommSchFoldTests
{
    /// <summary>
    /// <c>CommSch</c> 여는 태그. 여러 줄에 걸친 것도 잡는다.
    /// <c>CommSchItem</c> 은 빼야 해서 태그 이름 뒤를 공백·<c>&gt;</c> 로 끊는다.
    /// </summary>
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

    /// <summary>
    /// 이 판에 조건 칸이 있는가. 여는 태그 다음부터 <c>&lt;/CommSch&gt;</c>
    /// 전까지에서 <c>&lt;Fields&gt;</c> 를 찾는다.
    /// </summary>
    private static bool HasFields(string text, int tagIndex)
    {
        var close = text.IndexOf("</CommSch>", tagIndex, StringComparison.Ordinal);
        if (close < 0)
        {
            return false;
        }

        return text.IndexOf("<Fields>", tagIndex, close - tagIndex, StringComparison.Ordinal) >= 0;
    }

    /// <summary>
    /// 검사할 <c>.razor</c> 파일들. 빌드 산출물은 뺀다
    /// (<see cref="RazorCommentTests"/> 와 같은 길찾기다).
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
