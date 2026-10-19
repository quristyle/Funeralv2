using JSini.Web.ProjMng.Components.Shared;
using Xunit;

namespace JSini.Web.Architecture.Tests;

/// <summary>「처리 파일」 묶음을 읽는 규칙. 칸이 생기기 전 줄은 diff_stat 으로만 되읽고, git 의 8진 탈출(\355\225\234…)을 되돌려야 한국어 경로가 선다.</summary>
public sealed class AiRunFilesTests
{
    [Fact]
    public void 상태와_경로를_갈라_읽는다()
    {
        var rows = AiRunFiles.Parse(
            "M\tweb/src/Apps/JSini.Web.ProjMng/Components/Shared/AiTaskView.razor\n"
            + "A\tdeploy/sql/projmng-ai-changed-files-2026-10-12.sql\n"
            + "D\tdocs/old.md",
            diffStat: null);

        Assert.Equal(3, rows.Count);

        Assert.Equal(AiRunFileKind.Modified, rows[0].Kind);
        Assert.Equal("수정", rows[0].KindText);
        Assert.Equal("AiTaskView.razor", rows[0].Name);
        Assert.Equal("web/src/Apps/JSini.Web.ProjMng/Components/Shared", rows[0].Folder);

        Assert.Equal(AiRunFileKind.Added, rows[1].Kind);
        Assert.Equal(AiRunFileKind.Deleted, rows[2].Kind);
    }

    /// <summary>이름이 바뀐 줄은 칸이 셋이다 — <b>지금 경로</b>를 집는다.</summary>
    [Fact]
    public void 이름이_바뀐_줄은_새_경로를_집는다()
    {
        var rows = AiRunFiles.Parse("R100\tdocs/old.md\tdocs/new.md", diffStat: null);

        Assert.Single(rows);
        Assert.Equal(AiRunFileKind.Renamed, rows[0].Kind);
        Assert.Equal("docs/new.md", rows[0].Path);
    }

    /// <summary>뿌리에 놓인 파일은 폴더 자리가 비어 있어야 한다 — 「/」 하나만 서지 않게.</summary>
    [Fact]
    public void 뿌리_파일은_폴더가_비어_있다()
    {
        var rows = AiRunFiles.Parse("M\tREADME.md", diffStat: null);

        Assert.Equal("README.md", rows[0].Name);
        Assert.Equal(string.Empty, rows[0].Folder);
    }

    [Fact]
    public void 상한을_넘은_나머지는_외_N개로_선다()
    {
        var rows = AiRunFiles.Parse("M\ta.cs\n…\t외 37개", diffStat: null);

        Assert.Equal(AiRunFileKind.More, rows[1].Kind);
        Assert.Equal("외 37개", rows[1].Path);
        Assert.Equal(string.Empty, rows[1].KindText);
    }

    /// <summary>옛 건은 diff_stat 으로만 그린다 — 합계 줄과 480자에서 잘린 토막은 버린다(마지막 줄이 경로 반 토막일 수 있다).</summary>
    [Fact]
    public void 옛_건은_diff_stat_을_되읽는다()
    {
        var rows = AiRunFiles.Parse(null, """
            docs/notify-policy.md                              |  41 +-
             .../Components/Pages/NotifyPolicyPage.razor        | 372 ++++++----
             6 files changed, 918 insertions(+), 110 deletions(-)
             web/src/Apps/JSi
            """);

        Assert.Equal(2, rows.Count);
        Assert.Equal("docs/notify-policy.md", rows[0].Path);
        Assert.Equal(".../Components/Pages/NotifyPolicyPage.razor", rows[1].Path);

        // --stat 은 더해진 파일인지 고쳐진 파일인지를 말하지 않는다. 지어내지 않는다.
        Assert.All(rows, r => Assert.Equal(AiRunFileKind.Unknown, r.Kind));
        Assert.All(rows, r => Assert.Equal(string.Empty, r.KindText));
    }

    /// <summary>정본이 있으면 뒷길은 쳐다보지 않는다.</summary>
    [Fact]
    public void 정본이_있으면_diff_stat_은_안_읽는다()
    {
        var rows = AiRunFiles.Parse("A\tnew.cs", "old.cs | 3 +++");

        Assert.Single(rows);
        Assert.Equal("new.cs", rows[0].Path);
    }

    /// <summary>git 이 따옴표로 싸 둔 경로를 되돌린다. 바이트를 모아 한 번에 푼다 — 한 글자가 바이트 셋이라 탈출을 하나씩 풀면 글자가 안 된다.</summary>
    [Fact]
    public void 따옴표로_싸인_한국어_경로를_되돌린다()
    {
        var rows = AiRunFiles.Parse(
            null, "\"Docs/\\355\\225\\234\\352\\270\\200.md\" | 91 +++");

        Assert.Equal("Docs/한글.md", rows[0].Path);
        Assert.Equal("한글.md", rows[0].Name);
    }

    [Fact]
    public void 읽을_것이_없으면_빈_목록이다()
    {
        Assert.Empty(AiRunFiles.Parse(null, null));
        Assert.Empty(AiRunFiles.Parse("   ", "\n\n"));
    }

    /// <summary>상태 칸이 ? 로 끝나면 git 이 본 것이 아니라 결과문에서 뽑은 추정 줄이다. 갈래는 그대로 읽히고 배지 색만 갈린다.</summary>
    [Fact]
    public void 물음표가_붙은_상태는_추정_줄이다()
    {
        var rows = AiRunFiles.Parse("M\tweb/a.razor\nA?\tdocs/b.md", diffStat: null);

        Assert.False(rows[0].Derived);
        Assert.Equal(AiRunFileKind.Modified, rows[0].Kind);
        Assert.Equal("pm-peek__chg-mark--mod", rows[0].KindClass);
        Assert.Equal("web/a.razor", rows[0].Hint);

        Assert.True(rows[1].Derived);
        Assert.Equal(AiRunFileKind.Added, rows[1].Kind);
        Assert.Equal("추가", rows[1].KindText);

        // **확인된 것과 같은 색으로 세우지 않는다.** 배지에 달린 작은 「?」를
        // 못 보고 지나간 사람에게 둘이 구별되지 않는다.
        Assert.Equal("pm-peek__chg-mark--guess", rows[1].KindClass);

        // 아직 대 보기 전이다 — 실행기가 대 보면 이 말이 셋 중 하나로 갈린다
        // (「실제로 있는 파일」 · 「그런 파일이 없습니다」 · 「여럿이라」).
        Assert.Equal(AiRunFileCheck.Pending, rows[1].Check);
        Assert.Contains("아직 실제 파일에 대 보지 않았습니다", rows[1].Hint);
    }

    /// <summary>갈래를 모르는 추정 줄은 -? 다. ? 하나로 적으면 모르는 줄과 추정 줄이 같은 모양이 된다.</summary>
    [Fact]
    public void 갈래를_모르는_추정_줄도_추정으로_읽힌다()
    {
        var rows = AiRunFiles.Parse("-?\tscripts/x.sh", diffStat: null);

        Assert.True(rows[0].Derived);
        Assert.Equal(AiRunFileKind.Unknown, rows[0].Kind);
        Assert.Equal(string.Empty, rows[0].KindText);
    }

    /// <summary>「외 N개」 줄에는 추정 표가 붙지 않는다 — 그 줄은 파일이 아니다.</summary>
    [Fact]
    public void 외_N개_줄은_추정이_아니다()
    {
        var rows = AiRunFiles.Parse("A?\ta.cs\n…\t외 3개", diffStat: null);

        Assert.True(rows[0].Derived);
        Assert.False(rows[1].Derived);
    }

    // ── 대 보기 ──────────────────────────────────────────
    // 추정 줄을 실제 파일에 대 본 결과가 상태 칸 마지막 글자로 온다. 대 보는 것은 실행기(FileProbe)이고 여기는 그릴 뿐이다.

    /// <summary>
    /// 확인 표 넷을 읽는다. 백엔드의 <c>AiChangedFiles.MarkOf</c> 와 짝이다.
    /// </summary>
    [Theory]
    [InlineData("M?", AiRunFileCheck.Pending)]
    [InlineData("M*", AiRunFileCheck.Found)]
    [InlineData("M!", AiRunFileCheck.Missing)]
    [InlineData("M~", AiRunFileCheck.Ambiguous)]
    [InlineData("M", AiRunFileCheck.None)]
    public void 확인_표를_읽는다(string mark, AiRunFileCheck expected)
    {
        var rows = AiRunFiles.Parse($"{mark}\tweb/a.razor", diffStat: null);

        Assert.Equal(expected, rows[0].Check);
        Assert.Equal(expected != AiRunFileCheck.None, rows[0].Derived);
        Assert.Equal(AiRunFileKind.Modified, rows[0].Kind);
    }

    /// <summary>없는 경로는 한 칸 더 간다 — 그 줄은 틀린 것이라 같은 회색으로 물러서 있으면 목록을 훑는 눈이 그냥 지나간다.</summary>
    [Fact]
    public void 없는_경로는_따로_센다()
    {
        var rows = AiRunFiles.Parse(
            "M*\tweb/a.razor\nA!\tAiChangedFiles.Merge\nM~\tappsettings.json", diffStat: null);

        Assert.Equal("pm-peek__chg-mark--guess", rows[0].KindClass);
        Assert.Equal("✓", rows[0].CheckMark);
        Assert.Contains("실제로 있는 파일", rows[0].Hint);

        Assert.Equal("pm-peek__chg-mark--gone", rows[1].KindClass);
        Assert.Equal("✕", rows[1].CheckMark);
        Assert.Contains("그런 파일이 없습니다", rows[1].Hint);

        Assert.Equal("pm-peek__chg-mark--guess", rows[2].KindClass);
        Assert.Contains("여럿이라", rows[2].Hint);
    }

    /// <summary>git 이 센 줄에는 확인 표가 없다 — 댈 것이 없다.</summary>
    [Fact]
    public void git_줄에는_확인_표가_없다()
    {
        var rows = AiRunFiles.Parse("M\tweb/a.razor", diffStat: null);

        Assert.Equal(string.Empty, rows[0].CheckMark);
        Assert.Equal("web/a.razor", rows[0].Hint);
    }

    /// <summary>경로를 재는 뿌리를 목록 맨 위에 적는다. 줄마다 적힌 것은 대상 위치 다음부터라, 이 줄이 없으면 어느 저장소의 것인지 알 수 없다.</summary>
    [Fact]
    public void 탭이_대상_위치를_적는다()
    {
        var razor = RazorSource.Read(Path.Combine(
            RepoRoot(), "src", "Apps", "JSini.Web.ProjMng", "Components", "Shared",
            "AiTaskFilesTab.razor"));

        Assert.Contains("Item?.TargetPath", razor);
        Assert.Contains("pm-peek__chg-root", razor);
    }

    /// <summary>긴 이름은 꼭 모자랄 때만 끊고 앞자리는 어디서나 끊는다 — 이름이 두 동강 나면 눈으로 훑어 찾을 수 없다.</summary>
    [Fact]
    public void 긴_경로는_잘리지_않고_접힌다()
    {
        var css = File.ReadAllText(Path.Combine(
            RepoRoot(), "src", "Apps", "JSini.Web.ProjMng", "wwwroot", "projmng.css"));

        var name = Block(css, ".pm-peek__chg-name");
        var dir = Block(css, ".pm-peek__chg-dir");

        Assert.Contains("overflow-wrap: anywhere", name);
        Assert.Contains("word-break: break-all", dir);

        // **말줄임으로 자르지 않는다.** `rtl` 로 앞을 접어 봤더니 구분자가
        // 반대쪽으로 옮겨 붙어 **없는 경로**가 멀쩡한 얼굴로 섰다.
        Assert.DoesNotContain("text-overflow", Block(css, ".pm-peek__chg-path"));
    }

    private static string Block(string css, string selector)
    {
        var at = css.IndexOf(selector + " {", StringComparison.Ordinal);

        Assert.True(at >= 0, $"{selector} 규칙이 없습니다.");

        return css[at..(css.IndexOf('}', at) + 1)];
    }

    private static string RepoRoot()
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
