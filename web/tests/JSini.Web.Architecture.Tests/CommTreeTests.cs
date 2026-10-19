using System.Reflection;
using System.Text.RegularExpressions;
using DevExpress.Blazor;
using JSini.Web.Components.Data;
using Microsoft.AspNetCore.Components;
using Xunit;

namespace JSini.Web.Architecture.Tests;

/// <summary>CommTree 에 걸어 두는 두 검사 — 표(CommGrd)에 걸어 둔 것과 같다. CommTree 도 선언하지 않은 파라미터를 DxTreeList 로 흘리므로,
/// 기본값을 화면에 적으면 그 화면만 안 따라오고, EventCallback 을 splat 하면 그리기 전에 500 으로 죽는다. 둘 다 빌드로는 잡히지 않는다.</summary>
public sealed class CommTreeTests
{
    /// <summary>CommTree.razor 가 DxTreeList 에 주는 값과 자기 파라미터의 기본값. 부품 파일에서 읽는다.</summary>
    private static readonly Lazy<IReadOnlyDictionary<string, string>> Defaults = new(ReadDefaults);

    /// <summary>화면이 정할 몫이라 대조에서 빼는 것들.</summary>
    private static readonly HashSet<string> Ignored = ["Data", "TItem"];

    [Fact]
    public void 화면이_CommTree_기본값을_다시_적지_않는다()
    {
        var offenders = new List<string>();

        foreach (var file in RazorFiles().Where(f => Path.GetFileName(f) != "CommTree.razor"))
        {
            var text = RazorSource.Read(file);

            foreach (var (start, tag) in OpeningTags(text, "CommTree"))
            {
                foreach (Match attr in Regex.Matches(tag, "\\s([A-Za-z][A-Za-z0-9]*)=\"([^\"]*)\""))
                {
                    var name = attr.Groups[1].Value;

                    if (Ignored.Contains(name)
                        || !Defaults.Value.TryGetValue(name, out var fallback)
                        || fallback != attr.Groups[2].Value)
                    {
                        continue;
                    }

                    var line = text.Take(start).Count(c => c == '\n') + 1;
                    offenders.Add($"{Path.GetFileName(file)}:{line}  {name}=\"{fallback}\"");
                }
            }
        }

        Assert.True(
            offenders.Count == 0,
            "CommTree 의 기본값과 같은 값을 화면에서 다시 적고 있습니다. 지우십시오.\n"
            + "(그대로 두면 기본값을 바꿔도 이 화면들만 따라오지 않습니다)\n  "
            + string.Join("\n  ", offenders));
    }

    [Fact]
    public void 화면이_CommTree_에_EventCallback_을_splat_하지_않는다()
    {
        var forbidden = SplattableEventCallbacks();
        Assert.NotEmpty(forbidden);

        var offenders = new List<string>();

        foreach (var file in RazorFiles().Where(f => Path.GetFileName(f) != "CommTree.razor"))
        {
            var text = RazorSource.Read(file);

            foreach (var (start, tag) in OpeningTags(text, "CommTree"))
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
            "CommTree 가 선언하지 않은 EventCallback 파라미터를 splat 으로 넘기고 있습니다.\n"
            + "(넘어가는 것은 EventCallback 이 아니라 맨 대리자라 화면이 500 으로 죽습니다)\n"
            + "CommTree 에 그 파라미터를 선언하고 화면은 그 이름을 쓰십시오.\n  "
            + string.Join("\n  ", offenders));
    }

    /// <summary>DxTreeList 의 EventCallback 파라미터 중 CommTree 가 선언하지 않은 것들. 목록을 손으로 적지 않는다.</summary>
    private static HashSet<string> SplattableEventCallbacks()
    {
        var declared = Parameters(typeof(CommTree<object>)).Select(p => p.Name).ToHashSet(StringComparer.Ordinal);

        return Parameters(typeof(DxTreeList))
            .Where(p => IsEventCallback(p.PropertyType) && !declared.Contains(p.Name))
            .Select(p => p.Name)
            .ToHashSet(StringComparer.Ordinal);
    }

    /// <summary>CommTree.razor 에서 기본값을 읽는다 — DxTreeList 여는 태그의 값과 [Parameter] 초기값을 본다. 식(@…)으로 준 것은 뺀다.</summary>
    private static IReadOnlyDictionary<string, string> ReadDefaults()
    {
        var text = RazorSource.Read(Path.Combine(
            SolutionRoot(), "src", "Shared", "JSini.Web.Components", "Data", "CommTree.razor"));

        var found = new Dictionary<string, string>(StringComparer.Ordinal);

        var tree = OpeningTags(text, "DxTreeList").FirstOrDefault();
        Assert.NotNull(tree.Text);

        foreach (Match attr in Regex.Matches(tree.Text, "\\s([A-Za-z][A-Za-z0-9]*)=\"([^\"@]*)\""))
        {
            found[attr.Groups[1].Value] = attr.Groups[2].Value;
        }

        foreach (Match p in Regex.Matches(
            text, """\[Parameter\]\s*public\s+\S+\s+(\w+)\s*\{[^}]*\}\s*=\s*(?:"([^"]*)"|(true|false|\d+));"""))
        {
            found[p.Groups[1].Value] = p.Groups[2].Success ? p.Groups[2].Value : p.Groups[3].Value;
        }

        Assert.NotEmpty(found);
        return found;
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
