using Xunit;

namespace JSini.Web.Architecture.Tests;

/// <summary><c>EditForm</c> 안에 <c>&lt;AntiforgeryToken /&gt;</c> 을 두지 않는다. FormName 이 붙은 EditForm 은 토큰 칸을 스스로 넣는데, 또 쓰면 같은 이름의 숨은 칸이 둘이 되어 POST 가 늘 400 이 난다.
/// 빌드도 화면도 멀쩡해 보내기를 눌러야 터진다. 맨 form 에는 그것이 맞으므로 EditForm 블록 안만 본다.</summary>
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

    /// <summary>검사할 .razor 파일들. 실행 디렉터리가 CI 와 로컬에서 달라 저장소 뿌리를 거슬러 올라가 찾는다.</summary>
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
