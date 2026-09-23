using Xunit;

namespace JSini.Web.Architecture.Tests;

/// <summary>
/// AI 에게 보내는 지시에는 <b>커밋·push 를 말해 주는 문구가 무조건 붙는다.</b>
///
/// <para>
/// [왜 막는가]
/// </para>
///
/// <para>
/// 그 문구는 한동안 <c>AiTaskActions</c> 안의 private const 였고
/// <b>「이어서 지시」에만</b> 붙었다. 「빠른 지시」로 처음 보낸 건은 그 말을
/// 못 들어서 <b>다 고쳐 놓고 워크트리에만 남겨 둔 채 끝났다</b> — 사람은
/// 끝났다는 메일을 받는데 저장소에는 아무것도 없다. 그 사실은 다음 회차가
/// 같은 일을 다시 시작할 때에야 드러난다.
/// </para>
///
/// <para>
/// 지금은 <c>AiTaskAlways</c> 한 곳에서 나온다. 막아 두지 않으면 지시를 보내는
/// 자리가 하나 더 생길 때 <b>그 자리만 조용히 문구 없이</b> 나간다 — 파일만
/// 보면 정상으로 보이고, 어긋났다는 것은 회차가 헛돈 뒤에 알게 된다.
/// </para>
/// </summary>
public sealed class AiTaskAlwaysTests
{
    /// <summary>
    /// 문구의 알맹이. <b>온 문장을 다 적지 않는다</b> — 말끝을 다듬는 것까지
    /// 테스트가 막으면 문구를 못 고친다. 빠지면 회차가 헛도는 두 마디만 본다.
    /// </summary>
    private static readonly string[] Must =
    [
        "commit과 push를 수행해라",

        // **가지에 커밋만 하고 끝나는 것**을 막는 마디. 올리기를 켠 채
        // `succeeded` 로 끝난 지시 넷에 하나가 `pushed_commit` 이 비어
        // 있었다 — 제 작업 가지를 밀고 「push 했다」로 끝낸 것들이다.
        "origin/main 에 들어갔는지",

        "DB 연결정보를 소스에서 확인",
    ];

    /// <summary>문구가 사는 한 곳.</summary>
    private const string Home =
        "src/Apps/JSini.Web.ProjMng/Components/Shared/AiTaskAlways.cs";

    /// <summary>
    /// 지시를 만들어 보내는 자리들. <b>여기 적힌 파일은 모두</b>
    /// <c>AiTaskAlways.Append</c> 를 거쳐야 한다.
    /// </summary>
    private static readonly string[] Senders =
    [
        // 「빠른 지시」 — 새로 보내는 건(화면과 헤더 서랍이 함께 쓴다).
        "src/Apps/JSini.Web.ProjMng/Components/Shared/AiAskPanel.razor",

        // 「이어서 지시」 — 돌아간 건에 말을 덧붙여 다시 보내는 자리.
        "src/Apps/JSini.Web.ProjMng/Components/Shared/AiTaskActions.razor",
    ];

    [Fact]
    public void 문구는_한_곳에만_적혀_있다()
    {
        var offenders = SourceFiles()
            .Where(f => Relative(f) != Home)
            .Where(f =>
            {
                var text = File.ReadAllText(f);
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

                return !File.ReadAllText(path)
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

    /// <summary>
    /// 빈 글에는 붙이지 않는다 — 「이어서 지시」는 덧붙일 말 없이 보낼 수
    /// 있는데, 거기에 문구만 실어 보내면 <b>사람이 아무 말도 안 했는데 지시가
    /// 하나 생긴다.</b>
    /// </summary>
    [Fact]
    public void 빈_글에는_붙이지_않는다()
    {
        var text = File.ReadAllText(Path.Combine(
            SolutionRoot(), Home.Replace('/', Path.DirectorySeparatorChar)));

        Assert.Contains("IsNullOrWhiteSpace(written)", text, StringComparison.Ordinal);
    }

    /// <summary>
    /// 이미 들어 있으면 또 붙이지 않는다 — 다시 보내기·이어서 지시로 같은 글이
    /// 몇 번 돌 수 있고, 그때마다 붙으면 지시 끝이 같은 문단으로 도배된다.
    /// </summary>
    [Fact]
    public void 두_번_붙이지_않는다()
    {
        var text = File.ReadAllText(Path.Combine(
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

    /// <summary>
    /// 훑을 소스. <c>.razor</c> 와 <c>.cs</c> 둘 다 본다 — 문구가 어느 쪽으로도
    /// 새어 나갈 수 있다.
    /// </summary>
    private static IEnumerable<string> SourceFiles() =>
        new[] { "*.razor", "*.cs" }
            .SelectMany(pattern => Directory.EnumerateFiles(
                Path.Combine(SolutionRoot(), "src"), pattern, SearchOption.AllDirectories))
            .Where(f => !f.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}")
                     && !f.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}"));
}
