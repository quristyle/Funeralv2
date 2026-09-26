using System.Text.RegularExpressions;
using Xunit;

namespace JSini.Web.Architecture.Tests;

/// <summary>
/// 화면은 마크업(<c>X.razor</c>)과 코드(<c>X.razor.cs</c>)로 나눈다.
/// </summary>
/// <remarks>
/// <para>
/// razor 에 남아도 되는 <c>@code</c> 는 <b>마크업 템플릿을 돌려주는 멤버</b>뿐이다
/// (<c>@&lt;div&gt;…</c> · <c>__builder =&gt; { &lt;X /&gt; }</c>). C# 파일에서는
/// 마크업을 쓸 수 없기 때문이다. 나머지 코드와 주입은 코드비하인드에 둔다.
/// </para>
/// <para>
/// 업무 모듈 화면의 코드비하인드는 <c>BasePage</c> 계열(<c>BasePage</c> ·
/// <c>DataPage</c> · <c>AutoRefreshPage</c>)을 상속한다 — 주입과 권한 판정
/// (<c>Can</c>)을 화면마다 다시 적지 않게.
/// </para>
/// </remarks>
public sealed class CodeBehindTests
{
    private static readonly Regex CodeBlock = new(@"^[ \t]*@code\s*\{", RegexOptions.Multiline);

    [Fact]
    public void razor_에_inject_를_두지_않는다()
    {
        var offenders = RazorFiles()
            .Where(f => Path.GetFileName(f) != "_Imports.razor")
            .Where(f => Regex.IsMatch(File.ReadAllText(f), @"^@inject\s", RegexOptions.Multiline))
            .Select(Relative)
            .ToList();

        Assert.True(offenders.Count == 0,
            "주입은 코드비하인드에 [Inject] 속성으로 둔다:\n  " + string.Join("\n  ", offenders));
    }

    [Fact]
    public void razor_의_code_는_마크업_템플릿만_담는다()
    {
        var offenders = new List<string>();

        foreach (var file in RazorFiles())
        {
            var text = File.ReadAllText(file);

            foreach (Match m in CodeBlock.Matches(text))
            {
                var body = text[m.Index..];

                // 템플릿을 담은 블록이면 괜찮다.
                if (body.Contains("@<") || body.Contains("__builder")
                    || Regex.IsMatch(body, @"^\s*</?[A-Za-z]", RegexOptions.Multiline))
                {
                    continue;
                }

                offenders.Add(Relative(file));
            }
        }

        Assert.True(offenders.Count == 0,
            "마크업이 없는 @code 는 X.razor.cs 로 옮긴다:\n  " + string.Join("\n  ", offenders.Distinct()));
    }

    [Fact]
    public void 업무_화면의_코드비하인드는_BasePage_계열을_상속한다()
    {
        var bases = new[] { "BasePage", "DataPage", "AutoRefreshPage" };
        var apps = Path.Combine(SolutionRoot(), "src", "Apps");

        var offenders = RazorFiles()
            .Where(f => f.StartsWith(apps, StringComparison.OrdinalIgnoreCase))
            .Where(f => File.Exists(f + ".cs"))
            .Where(f => Regex.IsMatch(File.ReadAllText(f), @"^@page\s", RegexOptions.Multiline))
            .Where(f =>
            {
                var inherits = Regex.Match(File.ReadAllText(f), @"^@inherits\s+(\S+)", RegexOptions.Multiline);
                return !inherits.Success || !bases.Contains(inherits.Groups[1].Value);
            })
            .Select(Relative)
            .ToList();

        Assert.True(offenders.Count == 0,
            "@inherits BasePage(또는 DataPage · AutoRefreshPage)를 적는다:\n  " + string.Join("\n  ", offenders));
    }

    private static IEnumerable<string> RazorFiles() => Directory
        .EnumerateFiles(Path.Combine(SolutionRoot(), "src"), "*.razor", SearchOption.AllDirectories)
        .Where(f => !f.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}")
                 && !f.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}"));

    private static string Relative(string path) => Path.GetRelativePath(SolutionRoot(), path);

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
}
