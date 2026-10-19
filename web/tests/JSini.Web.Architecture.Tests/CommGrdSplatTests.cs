using System.Reflection;
using System.Text.RegularExpressions;
using DevExpress.Blazor;
using JSini.Web.Components.Data;
using Microsoft.AspNetCore.Components;
using Xunit;

namespace JSini.Web.Architecture.Tests;

/// <summary>CommGrd 에 EventCallback 파라미터를 splat 으로 넘기지 않는다. CommGrd 는 선언하지 않은 파라미터를 Extra 로 받아 DxGrid 에 넘기는데,
/// Razor 는 그 값의 형을 몰라 대리자를 맞춰 주지 못하고 화면이 그리기 전에 500 으로 죽는다. 빌드와 다른 테스트는 통과하므로 기계가 막는다 — 답은 CommGrd 가 그 파라미터를 선언하는 것이다.</summary>
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
            var text = RazorSource.Read(file);

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

    /// <summary>관리 칸은 「⋮」 하나라 폭이 늘 같아 ActionsWidth 를 걷었다. 화면에 남으면 선언 없는 이름이 DxGrid 로 흘러가 그 화면이 열리는 순간 죽는다.</summary>
    [Fact]
    public void 화면이_CommGrd_에_ActionsWidth_를_적지_않는다()
    {
        var offenders = new List<string>();

        foreach (var file in RazorFiles().Where(f => Path.GetFileName(f) != "CommGrd.razor"))
        {
            var text = RazorSource.Read(file);

            foreach (var (start, tag) in OpeningTags(text, "CommGrd"))
            {
                if (Regex.IsMatch(tag, "\\sActionsWidth="))
                {
                    var line = text.Take(start).Count(c => c == '\n') + 1;
                    offenders.Add($"{Path.GetFileName(file)}:{line}");
                }
            }
        }

        Assert.True(
            offenders.Count == 0,
            "CommGrd 에 ActionsWidth 가 남아 있습니다 — 관리 칸은 「⋮」 하나라 폭을 정하지 않습니다.\n  "
            + string.Join("\n  ", offenders));
    }

    /// <summary>DxGrid 의 EventCallback 파라미터 중 CommGrd 가 선언하지 않은 것들. 목록을 손으로 적지 않는다 — 새 이름이 생기면 여기서 드러나고, CommGrd 가 선언하면 저절로 빠진다.</summary>
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
