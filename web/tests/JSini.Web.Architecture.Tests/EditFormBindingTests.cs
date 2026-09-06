using System.Text.RegularExpressions;
using Xunit;

namespace JSini.Web.Architecture.Tests;

/// <summary>
/// 팝업 편집 폼 안의 DevExpress 편집기는 <c>@bind-</c> 로 묶는다.
///
/// <para>
/// [증상이 「아무 일도 안 일어남」이라 막는다]
/// </para>
///
/// <para>
/// 편집 폼 안에는 <c>EditContext</c> 가 흐른다. 그 안의 편집기는 자기가 어느
/// 필드인지 알아야 해서 검증 식을 만드는데, 값을 <c>Checked</c> +
/// <c>CheckedChanged</c> 처럼 <b>따로</b> 주면 만들 수 없다 —
/// </para>
///
/// <code>
/// CheckBoxModel requires a value for the 'CheckedExpression' property.
/// </code>
///
/// <para>
/// <b>화면에는 아무 표시도 나지 않는다.</b> 등록·수정 단추를 눌러도 팝업이
/// 그냥 안 열리고, 빌드도 다른 테스트도 전부 통과한다. 원인은 브라우저
/// 콘솔에만 있어서, 화면을 열고 개발자 도구를 봐야 비로소 보인다.
/// 실제로 두 화면에서 같은 길로 헤맸다(UserList · NoticeList).
/// </para>
///
/// <para>
/// [식을 직접 주는 것으로는 부족하다]
/// </para>
///
/// <para>
/// <c>XxxExpression</c> 을 손으로 줄 수도 있지만 그 식은 <b>순수한 멤버
/// 접근</b>이어야 한다. 형변환(<c>(IEnumerable&lt;string&gt;)a.RoleIds</c>)이나
/// 메서드 호출(<c>ToDate(a.BirthDate)</c>)을 끼우면 똑같이 죽는다 —
/// <c>contains a UnaryExpression which is not supported</c>. 형이 안 맞으면
/// <b>DTO 에 형이 맞는 창을 하나 내는 것</b>이 답이다
/// (<c>AccountDto.Roles</c> · <c>NoticeDto.IsActive</c> 가 그 예다).
/// </para>
///
/// <para>
/// 검증이 정말 필요 없는 자리라면 <c>ValidationEnabled="false"</c> 를 적는다.
/// 그러면 이 검사도 넘어간다 — 다만 그렇게 적은 것이 눈에 보여야 한다.
/// </para>
/// </summary>
public sealed class EditFormBindingTests
{
    /// <summary>편집 폼 안쪽. <c>CommGrd</c> 는 이 이름으로 팝업을 그린다.</summary>
    private static readonly Regex EditFormBlock =
        new(@"<EditFormTemplate\b.*?</EditFormTemplate>", RegexOptions.Singleline | RegexOptions.Compiled);

    /// <summary>
    /// DevExpress 편집기가 시작하는 자리. 태그 끝은 <see cref="OpeningTags"/> 가 찾는다 —
    /// 정규식으로 <c>&gt;</c> 를 찾으면 <c>Click="@(() =&gt; …)"</c> 의 화살표를
    /// 태그 끝으로 읽어 뒤쪽 속성을 통째로 놓친다. <b>실제로 그래서 오탐이 났다.</b>
    /// </summary>
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
            var text = File.ReadAllText(file);

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
