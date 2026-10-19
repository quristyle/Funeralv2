using System.Text.RegularExpressions;
using Xunit;

namespace JSini.Web.Architecture.Tests;

/// <summary>DelegatingHandler 안에서 만드는 요청은 절대 주소여야 한다. BaseAddress 는 HttpClient 의 것이라, 핸들러가 base.SendAsync 로 보내는 상대 경로는 그 클라이언트를 건너뛰어 주소 없이 던져진다.
/// AuthTokenHandler 의 토큰 갱신이 그래서 한 번도 성공하지 못했고, 드러나는 조건이 토큰 만료뿐이라 평소엔 멀쩡해 보였다. 빌드는 통과한다.</summary>
public sealed class HandlerRequestUriTests
{
    /// <summary>new HttpRequestMessage(HttpMethod.X, "...") 에서 그 문자열 리터럴을 집는다. 변수나 식으로 넘긴 것은 판정할 수 없다.</summary>
    private static readonly Regex LiteralUri = new(
        @"new\s+HttpRequestMessage\s*\(\s*HttpMethod\.[A-Za-z]+\s*,\s*""([^""]*)""",
        RegexOptions.Compiled | RegexOptions.Singleline);

    [Fact]
    public void DelegatingHandler_안에서_만드는_요청은_절대_주소다()
    {
        var offenders = new List<string>();

        foreach (var file in SourceFiles().Where(f => f.EndsWith(".cs", StringComparison.Ordinal)))
        {
            var text = RazorSource.Read(file);

            // 핸들러가 아닌 파일은 볼 이유가 없다 — HttpClient 가 BaseAddress 를 붙여 주므로 상대 경로가 정상이다.
            if (!text.Contains(": DelegatingHandler", StringComparison.Ordinal)
                && !text.Contains(": System.Net.Http.DelegatingHandler", StringComparison.Ordinal))
            {
                continue;
            }

            foreach (Match match in LiteralUri.Matches(text))
            {
                var uri = match.Groups[1].Value;

                if (Uri.TryCreate(uri, UriKind.Absolute, out _))
                {
                    continue;
                }

                var line = text.Take(match.Index).Count(c => c == '\n') + 1;
                offenders.Add($"{Relative(file)}:{line} — \"{uri}\"");
            }
        }

        Assert.True(
            offenders.Count == 0,
            "핸들러 안에서 만드는 요청에 상대 경로를 쓰면 보낼 때 던진다 — "
            + "`BaseAddress` 는 HttpClient 의 것이라 `base.SendAsync` 에는 닿지 않는다. "
            + "게이트웨이 주소를 받아 `new Uri(baseAddress, \"...\")` 로 조립한다.\n  "
            + string.Join("\n  ", offenders));
    }

    /// <summary>검사할 <c>.cs</c> 파일들. 빌드 산출물은 뺀다.</summary>
    private static IEnumerable<string> SourceFiles() =>
        Directory
            .EnumerateFiles(Path.Combine(SolutionRoot(), "src"), "*.cs", SearchOption.AllDirectories)
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
