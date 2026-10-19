using System.Text.RegularExpressions;
using Xunit;

namespace JSini.Web.Architecture.Tests;

/// <summary>팝업 편집 폼 안의 DevExpress 편집기는 @bind- 로 묶는다. 값을 Checked + CheckedChanged 로 따로 주면 EditContext 의 검증 식을 만들 수 없어 CheckedExpression 오류로 죽는데,
/// 화면에는 팝업이 그냥 안 열리고 빌드·테스트는 통과한다. XxxExpression 을 손으로 줄 때도 순수한 멤버 접근이어야 한다(형변환·메서드 호출이 끼면 UnaryExpression 오류). 형이 안 맞으면 DTO 에 형이 맞는 창을 하나 낸다.</summary>
public sealed class EditFormBindingTests
{
    /// <summary>편집 폼 안쪽. <c>CommGrd</c> 는 이 이름으로 팝업을 그린다.</summary>
    private static readonly Regex EditFormBlock =
        new(@"<EditFormTemplate\b.*?</EditFormTemplate>", RegexOptions.Singleline | RegexOptions.Compiled);

    /// <summary>DevExpress 편집기가 시작하는 자리. 태그 끝은 OpeningTags 가 찾는다 — 정규식으로 > 를 찾으면 Click="@(() => …)" 의 화살표를 태그 끝으로 읽는다.</summary>
    private static readonly Regex EditorOpen =
        new(@"<Dx[A-Za-z]+\b", RegexOptions.Compiled);

    /// <summary>값을 따로 받는 콜백 — <c>CheckedChanged</c> · <c>ValuesChanged</c> …</summary>
    private static readonly Regex ChangedAttribute =
        new(@"\s([A-Za-z][A-Za-z0-9]*)Changed=", RegexOptions.Compiled);

    [Fact]
    public void 편집_폼_안에서는_bind_로_묶는다()
    {
        var offenders = new List<string>();

        foreach (var file in RazorFiles())
        {
            var text = RazorSource.Read(file);

            foreach (Match form in EditFormBlock.Matches(text))
            {
                foreach (var (start, tag) in OpeningTags(form.Value))
                {
                    // 검증을 끈 편집기는 식이 필요 없다.
                    if (tag.Contains("ValidationEnabled=\"false\"", StringComparison.Ordinal))
                    {
                        continue;
                    }

                    foreach (Match changed in ChangedAttribute.Matches(tag))
                    {
                        var name = changed.Groups[1].Value;

                        // 식을 직접 준 경우는 넘어간다. 그 식이 멤버 접근인지까지는
                        // 여기서 못 본다 — 그것은 화면을 열어야 드러난다.
                        if (tag.Contains($"{name}Expression=", StringComparison.Ordinal))
                        {
                            continue;
                        }

                        var line = text.Take(form.Index + start).Count(c => c == '\n') + 1;
                        offenders.Add($"{Path.GetFileName(file)}:{line}  {name}Changed");
                    }
                }
            }
        }

        Assert.True(
            offenders.Count == 0,
            "편집 폼 안의 편집기가 @bind- 없이 값과 콜백을 따로 받고 있습니다.\n"
            + "(그대로 두면 등록·수정 단추를 눌러도 팝업이 말없이 안 열립니다.\n"
            + " 형이 안 맞으면 DTO 에 형이 맞는 속성을 하나 내십시오)\n  "
            + string.Join("\n  ", offenders));
    }

    /// <summary>여는 태그를 통째로 집어낸다. 따옴표 안의 <c>&gt;</c> 는 끝이 아니다.</summary>
    private static IEnumerable<(int Start, string Text)> OpeningTags(string text)
    {
        foreach (Match open in EditorOpen.Matches(text))
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
