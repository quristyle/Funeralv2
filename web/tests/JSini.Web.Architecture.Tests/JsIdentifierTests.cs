using System.Text.RegularExpressions;
using Xunit;

namespace JSini.Web.Architecture.Tests;

/// <summary>
/// JS 를 부를 때 쓰는 <b>식별자</b>에 괄호를 넣지 않는다.
///
/// [실제로 세 곳이 조용히 아무 일도 하지 않고 있었다]
///
/// 로그아웃이 이렇게 적혀 있었다.
///
/// <code>
/// Js.InvokeVoidAsync("document.getElementById('jsini-logout-form').submit")
/// </code>
///
/// 읽으면 「그 폼을 제출한다」로 보이는데 <b>Blazor 는 그 글자를 <c>.</c> 으로
/// 쪼개 이름을 하나씩 찾는다.</b> 그래서 <c>document</c> 안에서
/// <c>getElementById('jsini-logout-form')</c> 라는 <b>이름의 속성</b>을 뒤지고,
/// 없으니 던진다 —
/// <c>Could not find '…' ('getElementById('…')' was undefined)</c>.
///
/// [이 함정이 특히 나쁜 이유]
///
/// <b>예외가 화면에 드러나지 않는다.</b> 브라우저 콘솔에만 찍히고 화면은 아무
/// 일도 일어나지 않는다. 그래서 헤더의 「로그아웃」과 비밀번호를 바꾼 뒤의
/// 로그아웃이 <b>누르면 반응이 없는 상태</b>로 남아 있었다 — 빌드도, 다른
/// 시험도, 코드를 읽는 것도 전부 통과했다.
///
/// 사이드바에 「나가기」를 붙이면서 같은 줄을 복사해 세 번째가 되었고, 그때
/// 브라우저에서 눌러 보고 나서야 알았다.
///
/// [고친 방법]
///
/// <c>theme.js</c> 의 <c>jsiniForm.submit(id)</c> 처럼 <b>인자를 받는 함수</b>로
/// 부른다. 식별자에 괄호가 없으면 쪼개기에 걸릴 것이 없다.
/// </summary>
public class JsIdentifierTests
{
    /// <summary>
    /// <c>InvokeVoidAsync("…")</c> · <c>InvokeAsync&lt;T&gt;("…")</c> 의
    /// 첫 인자를 뽑는다.
    /// </summary>
    /// <remarks>
    /// 첫 인자만 본다 — 그 뒤는 함수에 넘기는 값이라 괄호가 있어도 된다.
    /// </remarks>
    /// <remarks>
    /// <para>
    /// <c>$"…"</c>(보간 문자열)도 잡아야 한다. 열쇠를 상수에서 끼워 넣는
    /// 자리가 실제로 그랬다 — <c>$"document.getElementById('{FormId}')…"</c>.
    /// </para>
    ///
    /// <para>
    /// <b>여는 따옴표를 역참조로 묶으면 안 된다.</b> 처음에
    /// <c>(?&lt;q&gt;[$]?")…\k&lt;q&gt;</c> 로 적었는데, 그러면 <c>$"</c> 로 열린
    /// 것은 닫는 쪽도 <c>$"</c> 여야 해서 <b>보간 문자열을 하나도 못 잡았다.</b>
    /// 시험이 통과하는데 아무것도 안 보는 상태였고, 일부러 되돌려 보고서야 알았다.
    /// </para>
    /// </remarks>
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
            var lines = File.ReadAllLines(file);

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
