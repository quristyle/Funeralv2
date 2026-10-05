using System.Text.RegularExpressions;
using Xunit;

namespace JSini.Web.Architecture.Tests;

/// <summary>
/// <see cref="System.Net.Http.DelegatingHandler"/> 안에서 만드는 요청은
/// <b>절대 주소여야 한다.</b>
///
/// <para>
/// [왜 테스트로 막는가 — 이레에 한 번씩 오류 화면이 떴다]
/// </para>
///
/// <para>
/// <c>BaseAddress</c> 는 <c>HttpClient</c> 의 것이지 핸들러의 것이 아니다.
/// 핸들러가 <c>base.SendAsync</c> 로 보내는 요청은 그 클라이언트를 건너뛰고
/// 안쪽 핸들러로 바로 들어가므로, <b>상대 경로를 절대 주소로 붙여 줄 사람이
/// 아무도 없다.</b> <c>SocketsHttpHandler</c> 가 그 자리에서 던진다 —
/// <c>An invalid request URI was provided…</c>
/// </para>
///
/// <para>
/// <c>AuthTokenHandler</c> 의 토큰 갱신이 그렇게 적혀 있었고, 그래서
/// <b>갱신은 단 한 번도 성공하지 못했다.</b> 드러나는 조건이 「access token 이
/// 만료될 때」 하나뿐이라(이레) 평소에는 멀쩡해 보였고, 겪은 사람도
/// 「새로고침하니 되더라」로 넘어갔다. 로그에 남은 단서는
/// <c>Sending HTTP request POST *</c> 한 줄 — 주소가 비어 있다는 뜻이다.
/// </para>
///
/// <para>
/// 빌드도 통과하고 코드만 보면 멀쩡해 보인다. 이 저장소가 그런 종류를
/// 테스트로 막아 온 것과 같은 자리다.
/// </para>
/// </summary>
public sealed class HandlerRequestUriTests
{
    /// <summary>
    /// <c>new HttpRequestMessage(HttpMethod.X, "...")</c> 에서 그 문자열을 집는다.
    ///
    /// <para>
    /// 문자열 리터럴만 본다. 변수나 식으로 넘기는 것은 여기서 판정할 수 없고,
    /// 실제로 밟은 모양이 리터럴이었다.
    /// </para>
    /// </summary>
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

            // 핸들러가 아닌 파일은 볼 이유가 없다 — 그쪽은 HttpClient 가
            // BaseAddress 를 붙여 주므로 상대 경로가 정상이다.
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
