using Xunit;

namespace JSini.Web.Architecture.Tests;

/// <summary>
/// <c>EditForm</c> 안에 <c>&lt;AntiforgeryToken /&gt;</c> 를 두지 않는다.
///
/// <para>
/// [실제로 밟았다 — 소개 사이트의 문의 폼이 늘 400 이었다]
/// </para>
///
/// <para>
/// <c>FormName</c> 이 붙은 <c>EditForm</c> 은 위조방지 토큰 칸을 <b>스스로 하나
/// 넣는다.</b> 그 안에 <c>&lt;AntiforgeryToken /&gt;</c> 을 또 쓰면 같은 이름의
/// 숨은 칸이 둘이 되고, 위조방지 검사는
/// <c>Request.Form["__RequestVerificationToken"]</c> 을 <b>문자열 하나로</b>
/// 읽으므로 두 값이 쉼표로 이어진 꼴(<c>CfDJ…,CfDJ…</c>)을 토큰으로 본다.
/// 해독이 깨져 <b>POST 가 늘 400</b> 이다.
/// </para>
///
/// <para>
/// <b>빌드도 화면도 멀쩡하다.</b> 폼은 정상으로 그려지고 보내기를 눌러야 터지며,
/// 사용자에게는 브라우저의 「페이지가 작동하지 않습니다 · HTTP ERROR 400」만 뜬다.
/// </para>
///
/// <para>
/// 맨 <c>&lt;form&gt;</c> 으로 쓰는 자리(로그아웃 폼 따위)는 넣어 주는 것이
/// 없으므로 거기서는 <c>&lt;AntiforgeryToken /&gt;</c> 이 맞다 — 그래서 파일
/// 단위가 아니라 <b><c>EditForm</c> 블록 안</b>만 본다.
/// </para>
/// </summary>
public sealed class AntiforgeryTokenTests
{
    [Fact]
    public void EditForm_안에_AntiforgeryToken_을_두지_않는다()
    {
        var offenders = new List<string>();

        foreach (var file in RazorFiles())
        {
            var text = RazorSource.Read(file).Replace("\r\n", "\n");

            var at = text.IndexOf("<EditForm", StringComparison.Ordinal);

            while (at >= 0)
            {
                var end = text.IndexOf("</EditForm>", at, StringComparison.Ordinal);
                var block = end < 0 ? text[at..] : text[at..end];

                var token = block.IndexOf("<AntiforgeryToken", StringComparison.Ordinal);

                if (token >= 0)
                {
                    var line = text.Take(at + token).Count(c => c == '\n') + 1;
                    offenders.Add($"{Path.GetFileName(file)}:{line}");
                }

                at = end < 0 ? -1 : text.IndexOf("<EditForm", end, StringComparison.Ordinal);
            }
        }

        Assert.True(
            offenders.Count == 0,
            "EditForm 안에 <AntiforgeryToken /> 이 있습니다. 그 줄을 지우십시오.\n"
            + "(EditForm 이 토큰 칸을 스스로 넣으므로 둘이 되고, 그러면 POST 가 늘 400 입니다)\n  "
            + string.Join("\n  ", offenders));
    }

    /// <summary>
    /// 검사할 <c>.razor</c> 파일들. 빌드 산출물은 뺀다.
    /// 실행 디렉터리가 CI 와 로컬에서 달라 저장소 뿌리를 거슬러 올라가 찾는다.
    /// </summary>
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
