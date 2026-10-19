using System.Text.RegularExpressions;
using Xunit;

namespace JSini.Web.Architecture.Tests;

/// <summary>JS 를 부를 때 쓰는 식별자에 괄호를 넣지 않는다. Blazor 는 식별자 글자를 . 으로 쪼개 이름을 하나씩 찾으므로, getElementById('…') 같은 괄호가 들어가면 던진다.
/// 예외는 브라우저 콘솔에만 찍혀 화면은 아무 일도 안 일어나고, 빌드와 시험은 통과한다. 고치는 법은 인자를 받는 함수(jsiniForm.submit(id))로 부르는 것이다.</summary>
public class JsIdentifierTests
{
    /// <summary>InvokeVoidAsync("…") · InvokeAsync&lt;T&gt;("…") 의 첫 인자를 뽑는다. 보간 문자열($"…")도 잡아야 한다 — 여는 따옴표를 역참조로 묶으면 보간 문자열을 하나도 못 잡는다.</summary>
    private static readonly Regex Call = new(
        """Invoke(?:Void)?Async(?:<[^>]*>)?\(\s*[$]?["](?<id>[^"]*)["]""",
        RegexOptions.Compiled);

    [Fact]
    public void JS_식별자에_괄호를_넣지_않는다()
    {
        var offenders = new List<string>();

        foreach (var file in SourceFiles())
        {
            // **줄 단위로 본다.** 주석을 건너뛰어야 하기 때문이다 — 고친 자리에
            // 「전에는 이렇게 적혀 있었다」를 남겨 두는 것이 이 저장소의 관례이고,
            // 그 줄까지 잡으면 함정을 설명해 둔 주석을 지우게 된다.
            var lines = RazorSource.ReadLines(file);

            for (var i = 0; i < lines.Length; i++)
            {
                var line = lines[i];

                if (line.TrimStart().StartsWith("//", StringComparison.Ordinal))
                {
                    continue;
                }

                foreach (var match in Call.Matches(line).Cast<Match>())
                {
                    var id = match.Groups["id"].Value;

                    if (!id.Contains('(') && !id.Contains(')'))
                    {
                        continue;
                    }

                    offenders.Add($"{Path.GetFileName(file)}:{i + 1} → \"{id}\"");
                }
            }
        }

        Assert.True(offenders.Count == 0,
            "JS 식별자에 괄호가 들어 있다. Blazor 는 식별자를 '.' 으로 쪼개 이름을 "
            + "하나씩 찾으므로 이렇게 적으면 **던지고, 그 예외는 콘솔에만 찍힌다** — "
            + "화면은 아무 일도 없는 것처럼 보인다.\n"
            + "인자를 받는 함수로 바꾼다 (theme.js 의 jsiniForm.submit 이 그 본보기다).\n\n"
            + string.Join('\n', offenders));
    }

    /// <summary>
    /// 검사할 <c>.cs</c>·<c>.razor</c> 파일들. 빌드 산출물은 뺀다.
    /// <c>HttpClientNamingTests</c> 와 같은 방식이다.
    /// </summary>
    private static IEnumerable<string> SourceFiles()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);

        while (dir is not null && !Directory.Exists(Path.Combine(dir.FullName, "src")))
        {
            dir = dir.Parent;
        }

        Assert.NotNull(dir);

        return Directory
            .EnumerateFiles(Path.Combine(dir!.FullName, "src"), "*.*", SearchOption.AllDirectories)
            .Where(f => f.EndsWith(".cs", StringComparison.OrdinalIgnoreCase)
                     || f.EndsWith(".razor", StringComparison.OrdinalIgnoreCase))
            .Where(f => !f.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}")
                     && !f.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}"));
    }
}
