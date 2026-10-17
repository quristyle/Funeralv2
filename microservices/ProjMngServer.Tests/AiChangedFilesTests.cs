using ProjMngServer.Services;

using Xunit;

namespace ProjMngServer.Tests;

/// <summary>
/// 「처리 파일」 목록을 <b>합치는 규칙</b>.
/// </summary>
/// <remarks>
/// <para>
/// 화면으로는 확인하기 나쁜 자리다 — 실행기가 운영 장비에서 돌고 모델이
/// 결과문을 읽어 준 뒤에야 값이 생긴다. 규칙을 여기 박아 둔다.
/// </para>
/// <para>
/// 적는 쪽(<see cref="AiChangedFiles.Format"/>)과 읽는 쪽이 짝이고, 포털의
/// <c>JSini.Web.ProjMng/Components/Shared/AiRunFiles.Parse</c> 가 <b>세 번째로</b>
/// 같은 모양을 읽는다. 한쪽만 고치면 화면에서 배지가 통째로 사라진다.
/// </para>
/// </remarks>
public class AiChangedFilesTests
{
    /// <summary><b>git 이 먼저고, git 이 이긴다.</b></summary>
    [Fact]
    public void 같은_경로는_git_이_이긴다()
    {
        // 모델은 「새로 만들었다」고 하지만 git 은 「고쳤다」고 센다.
        var merged = AiChangedFiles.Merge(
            "M\tweb/a.razor",
            [new AiChangedFile(AiChangedFileKind.Added, "web/a.razor", Derived: true)]);

        Assert.Equal("M\tweb/a.razor", merged);

        var rows = AiChangedFiles.Parse(merged);

        Assert.Single(rows);
        Assert.False(rows[0].Derived);
    }

    /// <summary>git 이 못 본 줄만 「추정」 표를 달고 <b>뒤에</b> 붙는다.</summary>
    [Fact]
    public void git_이_못_본_줄만_추정으로_뒤에_붙는다()
    {
        var merged = AiChangedFiles.Merge(
            "M\tweb/a.razor",
            [
                new AiChangedFile(AiChangedFileKind.Added, "docs/new.md", Derived: true),
                new AiChangedFile(AiChangedFileKind.Deleted, "docs/old.md", Derived: true),
            ]);

        Assert.Equal("M\tweb/a.razor\nA?\tdocs/new.md\nD?\tdocs/old.md", merged);
    }

    /// <summary>
    /// <b>git 이 아무것도 못 준 건</b>이 이 기능이 생긴 까닭이다 — 2026-10-17 에
    /// 운영의 최근 2주 276건 전부가 그랬다(실행기 바이너리가 옛 것이었다).
    /// </summary>
    [Fact]
    public void git_이_빈손이어도_결과문만으로_목록이_선다()
    {
        var merged = AiChangedFiles.Merge(
            null,
            [new AiChangedFile(AiChangedFileKind.Modified, "web/a.razor", Derived: true)]);

        Assert.Equal("M?\tweb/a.razor", merged);
    }

    /// <summary>양쪽 다 빈손이면 널이다 — 화면이 그 탭을 「없음」으로 그린다.</summary>
    [Fact]
    public void 양쪽_다_없으면_널이다()
    {
        Assert.Null(AiChangedFiles.Merge(null, null));
        Assert.Null(AiChangedFiles.Merge("   ", []));
    }

    /// <summary>
    /// 「외 N개」 줄은 <b>늘 맨 끝</b>이다. 가운데 끼면 목록이 거기서 끝난 것처럼 읽힌다.
    /// </summary>
    [Fact]
    public void 외_N개_줄은_맨_끝으로_간다()
    {
        var merged = AiChangedFiles.Merge(
            "M\ta.cs\n…\t외 37개",
            [new AiChangedFile(AiChangedFileKind.Added, "b.cs", Derived: true)]);

        Assert.Equal("M\ta.cs\nA?\tb.cs\n…\t외 37개", merged);
    }

    /// <summary>
    /// 모델이 제각각으로 적어 주는 경로를 <b>git 이 적은 것과 같은 모양</b>으로
    /// 맞춘다. 안 맞추면 같은 파일이 두 줄로 선다.
    /// </summary>
    [Theory]
    [InlineData("./web/a.razor", "web/a.razor")]
    [InlineData("`web/a.razor`", "web/a.razor")]
    [InlineData("  web/a.razor  ", "web/a.razor")]
    [InlineData("web\\a.razor", "web/a.razor")]
    [InlineData("/home/lee/ai-workspaces/run-712/web/a.razor", "web/a.razor")]
    public void 경로를_저장소_기준으로_맞춘다(string raw, string expected)
        => Assert.Equal(expected, AiChangedFiles.Normalize(raw));

    /// <summary>경로가 아닌 것은 버린다 — 폴더 · 위로 올라가는 길 · 빈 글자.</summary>
    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("web/src/")]
    [InlineData("../secrets.env")]
    [InlineData(null)]
    public void 경로가_아니면_널이다(string? raw)
        => Assert.Null(AiChangedFiles.Normalize(raw));

    /// <summary>
    /// 맞춰 놓으면 <b>중복이 걸러진다.</b> 모델이 `./` 를 붙여 적어도 git 이 이미
    /// 본 그 파일이다.
    /// </summary>
    [Fact]
    public void 모양을_맞춰_중복을_거른다()
    {
        var merged = AiChangedFiles.Merge(
            "M\tweb/a.razor",
            [new AiChangedFile(AiChangedFileKind.Added, "./web/a.razor", Derived: true)]);

        Assert.Equal("M\tweb/a.razor", merged);
    }

    /// <summary>
    /// 모델이 쓰는 말을 갈래로 옮긴다. <b>모르는 말은 지어내지 않는다</b> —
    /// 「고침」으로 떨어뜨리면 안 고친 파일이 고친 것으로 적힌다.
    /// </summary>
    [Theory]
    [InlineData("added", AiChangedFileKind.Added)]
    [InlineData("created", AiChangedFileKind.Added)]
    [InlineData("신규", AiChangedFileKind.Added)]
    [InlineData("modified", AiChangedFileKind.Modified)]
    [InlineData("수정", AiChangedFileKind.Modified)]
    [InlineData("deleted", AiChangedFileKind.Deleted)]
    [InlineData("삭제", AiChangedFileKind.Deleted)]
    [InlineData("renamed", AiChangedFileKind.Renamed)]
    [InlineData("무슨말인지모를것", AiChangedFileKind.Unknown)]
    [InlineData(null, AiChangedFileKind.Unknown)]
    public void 모델의_말을_갈래로_옮긴다(string? word, AiChangedFileKind expected)
        => Assert.Equal(expected, AiChangedFiles.KindFromWord(word));

    /// <summary>
    /// 갈래를 모르는 줄에도 <b>상태 칸은 있어야 한다</b> — 없으면 탭이 하나뿐이라
    /// 읽개가 그 줄을 통째로 버린다. 그리고 <c>?</c> 는 쓰지 않는다(추정 표다).
    /// </summary>
    [Fact]
    public void 갈래를_모르는_줄도_칸을_채운다()
    {
        var merged = AiChangedFiles.Merge(
            null,
            [new AiChangedFile(AiChangedFileKind.Unknown, "scripts/x.sh", Derived: true)]);

        Assert.Equal("-?\tscripts/x.sh", merged);

        var rows = AiChangedFiles.Parse(merged);

        Assert.Single(rows);
        Assert.True(rows[0].Derived);
        Assert.Equal("scripts/x.sh", rows[0].Path);
    }

    /// <summary>
    /// 이름이 바뀐 줄은 날것에서 칸이 셋이다(<c>R100\t옛것\t지금것</c>) —
    /// <b>마지막 칸이 지금 경로</b>다.
    /// </summary>
    [Fact]
    public void 이름이_바뀐_줄은_새_경로를_집는다()
    {
        var rows = AiChangedFiles.Parse("R100\tdocs/old.md\tdocs/new.md");

        Assert.Single(rows);
        Assert.Equal(AiChangedFileKind.Renamed, rows[0].Kind);
        Assert.Equal("docs/new.md", rows[0].Path);
    }

    /// <summary>
    /// 상한을 넘으면 <b>자르고 마지막 줄에 몇 개가 더 있는지만</b> 적는다.
    /// 대량 치환 한 번이 수천 줄을 DB 와 회로로 밀어 넣지 않게.
    /// </summary>
    [Fact]
    public void 상한을_넘으면_외_N개로_접는다()
    {
        var many = string.Join('\n', Enumerable.Range(0, 250).Select(i => $"M\tsrc/f{i}.cs"));

        var rows = AiChangedFiles.Parse(AiChangedFiles.Merge(many, null));

        Assert.Equal(AiChangedFiles.MaxLines + 1, rows.Count);
        Assert.Equal(AiChangedFileKind.More, rows[^1].Kind);
        Assert.Equal("외 50개", rows[^1].Path);
    }
}
