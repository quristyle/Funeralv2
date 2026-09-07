using System.Reflection;
using System.Text.RegularExpressions;
using DevExpress.Blazor;
using JSini.Web.Components.Data;
using Microsoft.AspNetCore.Components;
using Xunit;

namespace JSini.Web.Architecture.Tests;

/// <summary>
/// <c>CommGrd</c> 에 <c>EventCallback</c> 파라미터를 splat 으로 넘기지 않는다.
///
/// <para>
/// [증상이 「화면이 아예 안 열림」이라 막는다]
/// </para>
///
/// <para>
/// <c>CommGrd</c> 는 선언하지 않은 파라미터를 전부 <c>Extra</c> 로 받아
/// <c>DxGrid</c> 에 그대로 넘긴다. 편한 대신 <b>Razor 가 그 값의 형을 모른다.</b>
/// 문자열이 어긋나는 것은 <c>Coerce</c> 가 맞춰 주지만, <b>대리자</b>는 맞춰
/// 줄 수 없다 — 감싸 주면 알림 받는이가 화면이 아니라 <c>CommGrd</c> 가 되어
/// 화면이 다시 그려지지 않는다.
/// </para>
///
/// <code>
/// SelectedDataItemChanged="@((object? item) => OnGroupChangedAsync(item))"
/// </code>
///
/// <para>
/// 이렇게 적으면 맨 <c>Func&lt;object, Task&gt;</c> 가 넘어가고, DevExpress 가
/// 대입할 때 형변환에 실패한다. 화면은 <b>그리기도 전에</b> 500 으로 죽는다 —
/// </para>
///
/// <code>
/// Unable to set property 'SelectedDataItemChanged' on object of type 'DxGrid'.
/// Unable to cast object of type 'System.Func`2[…]' to type 'EventCallback`1[…]'.
/// </code>
///
/// <para>
/// <b>빌드도 다른 테스트도 전부 통과한다.</b> splat 은 검사할 형이 없기
/// 때문이다. 실제로 공통코드(<c>PortalCommonCode</c>)와
/// 기기관리(<c>DeviceList</c>) 두 화면이 이 한 줄 때문에 안 열리고 있었고,
/// 파일만 보면 둘 다 정상으로 보였다.
/// </para>
///
/// <para>
/// 답은 <b><c>CommGrd</c> 가 그 파라미터를 선언하는 것</b>이다. 선언하면 Razor
/// 가 화면 쪽에서 <c>EventCallback</c> 으로 감싸 주고, 받는이도 화면이 된다.
/// 선택은 그렇게 <c>SelectedItem</c> · <c>SelectedItemChanged</c> 로 옮겼다.
/// </para>
/// </summary>
public sealed class CommGrdSplatTests
{
    [Fact]
    public void 화면이_CommGrd_에_EventCallback_을_splat_하지_않는다()
    {
        var forbidden = SplattableEventCallbacks();
        Assert.NotEmpty(forbidden);

        var offenders = new List<string>();

        foreach (var file in RazorFiles().Where(f => Path.GetFileName(f) != "CommGrd.razor"))
        {
            var text = File.ReadAllText(file);

            foreach (var (start, tag) in OpeningTags(text, "CommGrd"))
            {
                foreach (Match attr in Regex.Matches(tag, "\\s([A-Za-z][A-Za-z0-9]*)="))
                {
                    if (!forbidden.Contains(attr.Groups[1].Value))
                    {
                        continue;
                    }

                    var line = text.Take(start).Count(c => c == '\n') + 1;
                    offenders.Add($"{Path.GetFileName(file)}:{line}  {attr.Groups[1].Value}");
                }
            }
        }

        Assert.True(
            offenders.Count == 0,
            "CommGrd 가 선언하지 않은 EventCallback 파라미터를 splat 으로 넘기고 있습니다.\n"
            + "(넘어가는 것은 EventCallback 이 아니라 맨 대리자라 화면이 500 으로 죽습니다)\n"
            + "CommGrd 에 그 파라미터를 선언하고 화면은 그 이름을 쓰십시오.\n  "
            + string.Join("\n  ", offenders));
    }

    /// <summary>
    /// <c>DxGrid</c> 의 <c>EventCallback</c> 파라미터 중 <c>CommGrd</c> 가
    /// 선언하지 않은 것들. <b>목록을 손으로 적지 않는다</b> — DevExpress 를
    /// 올리면 새 이름이 생기고, <c>CommGrd</c> 가 하나를 선언하면 그 이름은
    /// 여기서 저절로 빠진다.
    /// </summary>
    private static HashSet<string> SplattableEventCallbacks()
    {
        var declared = Parameters(typeof(CommGrd<object>)).Select(p => p.Name).ToHashSet(StringComparer.Ordinal);

        return Parameters(typeof(DxGrid))
            .Where(p => IsEventCallback(p.PropertyType) && !declared.Contains(p.Name))
            .Select(p => p.Name)
            .ToHashSet(StringComparer.Ordinal);
    }

    private static IEnumerable<PropertyInfo> Parameters(Type type) =>
        type
            .GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(p => p.IsDefined(typeof(ParameterAttribute), inherit: true));

    private static bool IsEventCallback(Type type) =>
        type == typeof(EventCallback)
        || (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(EventCallback<>));

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
