using JSini.Web.ProjMng.Components.Shared;
using Xunit;

namespace JSini.Web.Architecture.Tests;

/// <summary>
/// 「처리 파일」 묶음을 읽는 규칙.
/// </summary>
/// <remarks>
/// <para>
/// 화면으로는 확인하기 나쁜 자리다 — 실행기가 운영 장비에서 돈 뒤에야 값이
/// 생기고, 옛 건을 되읽는 뒷길은 <b>2026-10-12 이전에 쌓인 줄</b>에서만
/// 나타난다. 그 두 모양을 여기 박아 둔다.
/// </para>
/// <para>
/// <b>특히 8진 탈출이다.</b> 운영의 <c>diff_stat</c> 에 한국어 파일명이
/// <c>"\355\225\234…"</c> 로 쌓여 있어서, 되돌리지 못하면 그 줄들이 화면에
/// 역슬래시 숫자 더미로 선다.
/// </para>
/// </remarks>
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

    /// <summary>
    /// <b>옛 건은 <c>diff_stat</c> 으로만 그린다.</b> 합계 줄과 잘린 토막은 버린다 —
    /// 그 칸은 480자에서 잘려 있어 마지막 줄이 경로의 반 토막인 건이 실제로 있다.
    /// </summary>
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

    /// <summary>
    /// git 이 따옴표로 싸 둔 경로를 되돌린다. <b>바이트를 모아 한 번에 푼다</b> —
    /// 한 글자가 바이트 셋이라 탈출 하나씩 풀면 글자가 안 된다.
    /// </summary>
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

    /// <summary>
    /// 상태 칸이 <c>?</c> 로 끝나면 <b>git 이 본 것이 아니라 결과문에서 뽑은 줄</b>
    /// 이다(<c>ProjMngServer.Services.AiChangedFiles.Merge</c> 가 그 표를 단다).
    /// 갈래는 그대로 읽히고 배지 색만 갈린다.
    /// </summary>
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
        Assert.Contains("git 으로 확인되지 않았습니다", rows[1].Hint);
    }

    /// <summary>
    /// 갈래를 모르는 추정 줄은 <c>-?</c> 다. <b><c>?</c> 하나로 적지 않는다</b> —
    /// 그러면 「모르는 줄」과 「추정 줄」이 같은 모양이 된다.
    /// </summary>
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
}
