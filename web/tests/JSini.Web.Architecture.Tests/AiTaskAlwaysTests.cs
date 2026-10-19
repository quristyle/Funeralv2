using Xunit;

namespace JSini.Web.Architecture.Tests;

/// <summary>AI 에게 보내는 지시에는 커밋·push 를 말해 주는 문구가 무조건 붙는다. 막아 두지 않으면 지시를 보내는 자리가 하나 더 생길 때 그 자리만 조용히 문구 없이 나간다.</summary>
public sealed class AiTaskAlwaysTests
{
    /// <summary>문구의 알맹이. 온 문장을 다 적지 않는다 — 말끝을 다듬는 것까지 막으면 문구를 못 고친다.</summary>
    private static readonly string[] Must =
    [
        "commit과 push를 수행해라",

        // 가지에 커밋만 하고 끝나는 것을 막는 마디. 올리기를 켠 채 succeeded 로 끝난 건 중 하나는 pushed_commit 이 비어 있었다.
        "origin/main 에 들어갔는지",

        "DB 연결정보를 소스에서 확인",

        // 다 끝난 건이 두 시간을 버리고 실패로 앉는 것을 막는 마디 — 표준입력을 기다리며 멈춘 grep 이 print 모드를 120분 채우고 잘렸다.
        "표준입력을 기다리며",
    ];

    /// <summary>문구가 사는 한 곳.</summary>
    private const string Home =
        "src/Apps/JSini.Web.ProjMng/Components/Shared/AiTaskAlways.cs";

    /// <summary>지시를 만들어 보내는 자리들. 여기 적힌 파일은 모두 AiTaskAlways.Append 를 거쳐야 한다.</summary>
    private static readonly string[] Senders =
    [
        // 「빠른 지시」 — 새로 보내는 건(화면과 헤더 서랍이 함께 쓴다).
        "src/Apps/JSini.Web.ProjMng/Components/Shared/AiAskPanel.razor",

        // 「이어서 지시」 — 돌아간 건에 말을 덧붙여 다시 보내는 공통 팝업.
        "src/Apps/JSini.Web.ProjMng/Components/Shared/AiTaskContinuePopup.razor",
    ];

    [Fact]
    public void 문구는_한_곳에만_적혀_있다()
    {
        var offenders = SourceFiles()
            .Where(f => Relative(f) != Home)
            .Where(f =>
            {
                var text = RazorSource.Read(f);
                return Must.Any(m => text.Contains(m, StringComparison.Ordinal));
            })
            .Select(Relative)
            .OrderBy(f => f, StringComparer.Ordinal)
            .ToArray();

        Assert.True(
            offenders.Length == 0,
            $"늘 따라붙는 지시 문구는 `{Home}` 한 곳에서만 나온다. "
            + "베껴 두면 한쪽만 고쳐져, 같은 사람이 같은 자리에 던진 두 건이 "
            + "서로 다른 지시를 받는다. `AiTaskAlways.Append` 를 부르십시오.\n  "
            + string.Join("\n  ", offenders));
    }

    [Fact]
    public void 지시를_보내는_자리는_문구를_붙인다()
    {
        var missing = Senders
            .Where(rel =>
            {
                var path = Path.Combine(SolutionRoot(), rel.Replace('/', Path.DirectorySeparatorChar));
                Assert.True(File.Exists(path), $"검사 대상이 없어졌습니다: {rel}");

                return !RazorSource.Read(path)
                    .Contains("AiTaskAlways.Append", StringComparison.Ordinal);
            })
            .ToArray();

        Assert.True(
            missing.Length == 0,
            "AI 에게 지시를 보내는 자리는 사람이 적은 말 뒤에 "
            + "`AiTaskAlways.Append` 로 늘 따라붙는 문구를 붙인다. "
            + "안 붙이면 그 회차는 고친 것을 워크트리에 남긴 채 끝난다.\n  "
            + string.Join("\n  ", missing));
    }

    /// <summary>빈 글에는 붙이지 않는다 — 덧붙일 말이 없는 이어서 지시에 문구만 실으면 사람이 말한 적 없는 지시가 생긴다.</summary>
    [Fact]
    public void 빈_글에는_붙이지_않는다()
    {
        var text = RazorSource.Read(Path.Combine(
            SolutionRoot(), Home.Replace('/', Path.DirectorySeparatorChar)));

        Assert.Contains("IsNullOrWhiteSpace(written)", text, StringComparison.Ordinal);
    }

    /// <summary>이미 들어 있으면 또 붙이지 않는다 — 같은 글이 몇 번 돌 때마다 붙으면 지시 끝이 같은 문단으로 도배된다.</summary>
    [Fact]
    public void 두_번_붙이지_않는다()
    {
        var text = RazorSource.Read(Path.Combine(
            SolutionRoot(), Home.Replace('/', Path.DirectorySeparatorChar)));

        Assert.Contains("written.Contains(Text", text, StringComparison.Ordinal);
    }

    private static string Relative(string path) =>
        Path.GetRelativePath(SolutionRoot(), path).Replace(Path.DirectorySeparatorChar, '/');

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

    /// <summary>훑을 소스. .razor 와 .cs 둘 다 본다 — 문구가 어느 쪽으로도 새어 나갈 수 있다.</summary>
    private static IEnumerable<string> SourceFiles() =>
        new[] { "*.razor", "*.cs" }
            .SelectMany(pattern => Directory.EnumerateFiles(
                Path.Combine(SolutionRoot(), "src"), pattern, SearchOption.AllDirectories))
            .Where(f => !f.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}")
                     && !f.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}"));
}
