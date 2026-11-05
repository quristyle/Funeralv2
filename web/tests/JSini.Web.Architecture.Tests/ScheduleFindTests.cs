using JSini.Web.ProjMng.Api;
using JSini.Web.ProjMng.Components.Shared;
using Xunit;

namespace JSini.Web.Architecture.Tests;

/// <summary>
/// 일정표 「일정 찾기」의 맞춤 계산. 틀려도 예외 없이 「찾은 일정이 없습니다」나
/// 엉뚱한 줄로만 보이므로 계산을 못 박아 둔다.
/// </summary>
public sealed class ScheduleFindTests
{
    private static WbsItemDto Row(
        string? title = null, string? memo = null, string? body = null, string? kind = null,
        string? dev = null, string? req = null, DateTime? sdt = null, DateTime? edt = null) =>
        new()
        {
            WbsId = 1, ProcNm = title, Comm = memo, BodyText = body, ProcTp = kind,
            DevUser = dev, ReqUser = req, PlanSdt = sdt, PlanEdt = edt,
        };

    private static string? Names(string? id) => id == "lee" ? "이순열" : id;

    [Fact]
    public void 조건이_없으면_맞고_걸린_칸이_없다()
    {
        Assert.Equal(FindField.None, ScheduleFind.Match(Row(title: "회의"), new ScheduleFindQuery()));
    }

    [Fact]
    public void 검색어는_고른_칸에서만_찾는다()
    {
        var w = Row(title: "주간 회의", memo: "배포 점검");

        Assert.Equal(FindField.Memo,
            ScheduleFind.Match(w, new ScheduleFindQuery { Keyword = "배포" }));
        Assert.Null(ScheduleFind.Match(w,
            new ScheduleFindQuery { Keyword = "배포", Targets = FindField.Title }));
    }

    [Fact]
    public void 찾을_칸을_다_끄면_다_켠_것과_같다()
    {
        var q = new ScheduleFindQuery { Keyword = "배포", Targets = FindField.None };

        Assert.Equal(FindField.Memo, ScheduleFind.Match(Row(memo: "배포"), q));
    }

    [Fact]
    public void 띄어_쓴_낱말은_모두_있어야_하고_칸이_달라도_된다()
    {
        var w = Row(title: "주간 회의", memo: "배포 점검");

        Assert.Equal(FindField.Title | FindField.Memo,
            ScheduleFind.Match(w, new ScheduleFindQuery { Keyword = "회의 배포" }));
        Assert.Null(ScheduleFind.Match(w, new ScheduleFindQuery { Keyword = "회의 출장" }));
    }

    [Fact]
    public void 칸별_조건은_모두_맞아야_한다()
    {
        var w = Row(title: "주간 회의", memo: "배포 점검");

        Assert.Equal(FindField.Title | FindField.Memo,
            ScheduleFind.Match(w, new ScheduleFindQuery { Title = "회의", Memo = "점검" }));
        Assert.Null(ScheduleFind.Match(w, new ScheduleFindQuery { Title = "회의", Memo = "출장" }));
        Assert.Null(ScheduleFind.Match(w, new ScheduleFindQuery { Title = "배포" }));
    }

    [Fact]
    public void 담당자와_요청자는_이름으로도_찾고_서로_갈린다()
    {
        var w = Row(dev: "lee", req: "서성일");

        Assert.Equal(FindField.Assignee,
            ScheduleFind.Match(w, new ScheduleFindQuery { Assignee = "이순열" }, Names));
        Assert.Equal(FindField.Assignee,
            ScheduleFind.Match(w, new ScheduleFindQuery { Assignee = "LEE" }, Names));
        Assert.Null(ScheduleFind.Match(w, new ScheduleFindQuery { Requester = "이순열" }, Names));
        Assert.Equal(FindField.Requester,
            ScheduleFind.Match(w, new ScheduleFindQuery { Keyword = "서성일" }, Names));
    }

    [Fact]
    public void 기간_기준이_시작이면_앞서_시작해_걸친_일정은_빠진다()
    {
        var w = Row(sdt: new DateTime(2026, 10, 28), edt: new DateTime(2026, 11, 3));
        var nov = (From: new DateOnly(2026, 11, 1), To: new DateOnly(2026, 11, 30));

        Assert.NotNull(ScheduleFind.Match(w,
            new ScheduleFindQuery { From = nov.From, To = nov.To, Basis = ScheduleFind.Overlap }));
        Assert.Null(ScheduleFind.Match(w,
            new ScheduleFindQuery { From = nov.From, To = nov.To, Basis = ScheduleFind.Start }));
        Assert.NotNull(ScheduleFind.Match(w,
            new ScheduleFindQuery { From = nov.From, To = nov.To, Basis = ScheduleFind.End }));
    }

    [Fact]
    public void 기간_조건이_있으면_기간_없는_일정은_빠지고_없으면_남는다()
    {
        var w = Row(title: "메모만");

        Assert.Null(ScheduleFind.Match(w, new ScheduleFindQuery { From = new DateOnly(2026, 1, 1) }));
        Assert.NotNull(ScheduleFind.Match(w, new ScheduleFindQuery()));
    }

    [Fact]
    public void 뒤집힌_기간은_바로잡아_본다()
    {
        var w = Row(sdt: new DateTime(2026, 11, 10));

        Assert.NotNull(ScheduleFind.Match(w, new ScheduleFindQuery
        {
            From = new DateOnly(2026, 11, 30), To = new DateOnly(2026, 11, 1), Basis = ScheduleFind.Start,
        }));
    }

    [Fact]
    public void 칸별_건수는_걸린_칸만_센다()
    {
        var counts = ScheduleFind.CountByField([FindField.Title, FindField.Title | FindField.Memo, FindField.None]);

        Assert.Equal([("제목", 2), ("메모", 1)], counts);
    }
}
