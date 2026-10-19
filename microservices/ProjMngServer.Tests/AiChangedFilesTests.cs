using ProjMngServer.Services;

using Xunit;

namespace ProjMngServer.Tests;

/// <summary>
/// 「처리 파일」 목록을 <b>합치는 규칙</b>.
/// </summary>
/// 화면으로는 확인하기 나쁘다 — 값은 운영 장비에서 돌고 모델이 결과문을 읽은 뒤에야 생긴다. 적는 쪽(Format)과 읽는 쪽(포털 AiRunFiles.Parse)은 짝이라 한쪽만 고치면 배지가 사라진다.
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
    /// 실행기가 옛 바이너리면 git 이 아무것도 못 주는 건이 생긴다 — 그때도 목록이 비지 않게 하려고 이 기능이 있다.
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

        // ── 대 보기 ────────────────────────────────────────────
    //
        // 결과문에서 뽑은 경로에는 파일이 아닌 것과 마디가 빠진 것이 섞인다. 대 보는 것은 호스트의 실행기이고, 그 답을 목록에 새기는 규칙은 여기 있다.

    /// <summary>
    /// <b>대상 위치를 알면 그 뿌리를 뗀다.</b> 모델은 결과문에 장비의 온
    /// 경로를 그대로 옮겨 적는 일이 잦다.
    /// </summary>
    [Theory]
    [InlineData("/home/lee/Funeralv2/web/a.razor", "/home/lee/Funeralv2", "web/a.razor")]
    [InlineData("/home/lee/Funeralv2/web/a.razor", "/home/lee/Funeralv2/", "web/a.razor")]
    [InlineData("/home/lee/Other/web/a.razor", "/home/lee/Funeralv2", "home/lee/Other/web/a.razor")]
    [InlineData("web/a.razor", "/home/lee/Funeralv2", "web/a.razor")]
    public void 대상_위치를_떼_낸다(string raw, string target, string expected)
        => Assert.Equal(expected, AiChangedFiles.Normalize(raw, target));

    /// <summary>
    /// 뿌리 이름이 <b>겹치는 데까지만 같은</b> 경로는 떼지 않는다 —
    /// <c>/home/lee/Funeralv2x/…</c> 는 다른 자리다.
    /// </summary>
    [Fact]
    public void 뿌리와_이름만_닮은_경로는_안_뗀다()
        => Assert.Equal(
            "home/lee/Funeralv2x/web/a.razor",
            AiChangedFiles.Normalize("/home/lee/Funeralv2x/web/a.razor", "/home/lee/Funeralv2"));

    /// <summary>합친 직후의 추정 줄은 <b>아직 안 대 본 것</b>이다.</summary>
    [Fact]
    public void 합친_직후의_추정_줄은_아직이다()
    {
        var rows = AiChangedFiles.Parse(AiChangedFiles.Merge(
            null, [new AiChangedFile(AiChangedFileKind.Modified, "web/a.razor", Derived: true)]));

        Assert.Equal(AiChangedFileCheck.Pending, rows[0].Check);
    }

    /// <summary>확인 표 넷이 <b>글자로 오간다.</b> 적는 쪽과 읽는 쪽이 짝이다.</summary>
    [Theory]
    [InlineData(AiChangedFileCheck.Pending, "M?")]
    [InlineData(AiChangedFileCheck.Found, "M*")]
    [InlineData(AiChangedFileCheck.Missing, "M!")]
    [InlineData(AiChangedFileCheck.Ambiguous, "M~")]
    public void 확인_표를_적고_되읽는다(AiChangedFileCheck check, string mark)
    {
        Assert.Equal(mark, AiChangedFiles.MarkOf(AiChangedFileKind.Modified, derived: true, check));
        Assert.Equal(check, AiChangedFiles.CheckOf(mark));
        Assert.Equal(AiChangedFileKind.Modified, AiChangedFiles.KindOf(mark));
    }

    /// <summary>git 이 센 줄에는 표가 없다 — 댈 것이 없다.</summary>
    [Fact]
    public void git_줄에는_확인_표가_없다()
    {
        Assert.Equal("M", AiChangedFiles.MarkOf(AiChangedFileKind.Modified, derived: false));
        Assert.Equal(AiChangedFileCheck.None, AiChangedFiles.CheckOf("M"));
    }

    /// <summary>
    /// 대 본 결과가 <b>표를 바꾸고 반 마디짜리 경로를 편다.</b>
    /// </summary>
    [Fact]
    public void 대_본_결과가_표와_경로를_고친다()
    {
        var next = AiChangedFiles.ApplyChecks(
            "M?\tweb/AiAskPanel.razor\nA?\tAiChangedFiles.Merge",
            new Dictionary<string, (string, AiChangedFileCheck)>
            {
                ["web/AiAskPanel.razor"] =
                    ("web/src/Apps/JSini.Web.ProjMng/Components/Shared/AiAskPanel.razor",
                     AiChangedFileCheck.Found),
                ["AiChangedFiles.Merge"] = (string.Empty, AiChangedFileCheck.Missing),
            });

        Assert.Equal(
            "M*\tweb/src/Apps/JSini.Web.ProjMng/Components/Shared/AiAskPanel.razor\n"
            + "A!\tAiChangedFiles.Merge",
            next);
    }

    /// <summary>
    /// <b>펴고 나니 git 줄과 같아지면 그 줄을 버린다.</b> 안 버리면 같은
    /// 파일이 「수정」과 「수정·추정」 두 줄로 선다.
    /// </summary>
    [Fact]
    public void 펴서_겹치면_git_줄만_남는다() => Assert.Equal(
        "M\tweb/src/Shared/AiAskPanel.razor",
        AiChangedFiles.ApplyChecks(
            "M\tweb/src/Shared/AiAskPanel.razor\nM?\tweb/AiAskPanel.razor",
            new Dictionary<string, (string, AiChangedFileCheck)>
            {
                ["web/AiAskPanel.razor"] =
                    ("web/src/Shared/AiAskPanel.razor", AiChangedFileCheck.Found),
            }));

    /// <summary>
    /// <b>답이 안 온 줄은 그대로 둔다</b> — 「아직」인 채 남아 다음 바퀴에
    /// 다시 물린다. 아무것도 안 바뀌었으면 널이라 헛 UPDATE 를 안 친다.
    /// </summary>
    [Fact]
    public void 답이_없으면_그대로고_널이다()
    {
        Assert.Null(AiChangedFiles.ApplyChecks(
            "M?\tweb/a.razor",
            new Dictionary<string, (string, AiChangedFileCheck)>
            {
                ["docs/other.md"] = (string.Empty, AiChangedFileCheck.Missing),
            }));

        Assert.Null(AiChangedFiles.ApplyChecks(
            null, new Dictionary<string, (string, AiChangedFileCheck)>()));
    }

    /// <summary>
    /// 「외 N개」 줄에는 <b>확인 표를 붙이지 않는다</b> — 그 줄은 파일이 아니다.
    /// </summary>
    [Fact]
    public void 외_N개_줄은_대_보지_않는다()
    {
        Assert.Equal(
            "…",
            AiChangedFiles.MarkOf(AiChangedFileKind.More, derived: true, AiChangedFileCheck.Missing));

        var next = AiChangedFiles.ApplyChecks(
            "M?\tweb/a.razor\n…\t외 3개",
            new Dictionary<string, (string, AiChangedFileCheck)>
            {
                ["web/a.razor"] = ("web/a.razor", AiChangedFileCheck.Found),
            });

        Assert.Equal("M*\tweb/a.razor\n…\t외 3개", next);
    }
}
