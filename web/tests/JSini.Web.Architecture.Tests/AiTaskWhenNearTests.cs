using JSini.Web.Components.Data;
using JSini.Web.ProjMng.Api;
using JSini.Web.ProjMng.Components.Shared;
using Xunit;

namespace JSini.Web.Architecture.Tests;

/// <summary>좁은 자리의 시각 표기 — 오늘에서 먼 만큼만 적는다. 틀려도 화면은 멀쩡하다: 1년 어긋난 시각도 같은 얼굴로 서서 알아챌 단서가 없으므로 여기 박아 둔다.</summary>
public sealed class AiTaskWhenNearTests
{
    /// <summary>오늘 것은 <b>시·분만</b>. 날짜는 읽는 사람이 되채울 수 있다.</summary>
    [Fact]
    public void 오늘_것은_시분만_적는다()
    {
        var today = AppTime.Today;

        Assert.Equal("09:30", AiTaskWhen.Near(Kst(today, 9, 30)));
    }

    /// <summary>이달 것은 <b>날짜까지</b>. 맨 숫자로 두면 시각과 안 갈려 「일」을 붙인다.</summary>
    [Fact]
    public void 이달_것은_날짜에_일을_붙인다()
    {
        var today = AppTime.Today;

        // 오늘이 1일이면 어제가 지난달이다 — 이달 안의 **다른** 날을 고른다.
        var day = today.Day == 1 ? 2 : 1;
        var other = new DateOnly(today.Year, today.Month, day);

        Assert.Equal($"{day:00}일 09:30", AiTaskWhen.Near(Kst(other, 9, 30)));
    }

    /// <summary>올해의 다른 달이면 <b>달까지</b> 적고 해는 뗀다.</summary>
    [Fact]
    public void 올해의_다른_달이면_월일을_적는다()
    {
        var today = AppTime.Today;
        var month = today.Month == 1 ? 2 : 1;
        var other = new DateOnly(today.Year, month, 14);

        Assert.Equal($"{month:00}-14 09:30", AiTaskWhen.Near(Kst(other, 9, 30)));
    }

    /// <summary>해가 다르면 해를 붙인다 — 해는 읽는 사람이 되채울 수 없다.</summary>
    [Fact]
    public void 지난해_것에는_해가_붙는다()
    {
        var today = AppTime.Today;
        var other = new DateOnly(today.Year - 1, 9, 14);

        Assert.Equal($"{today.Year - 1}-09-14 09:30", AiTaskWhen.Near(Kst(other, 9, 30)));
    }

    /// <summary>달만 견주면 안 된다 — 작년 같은 달이 이달로 읽히면 1년 어긋난 시각이 오늘 것과 같은 꼴로 선다.</summary>
    [Fact]
    public void 작년_같은_달을_이달로_읽지_않는다()
    {
        var today = AppTime.Today;
        var sameMonthLastYear = new DateOnly(today.Year - 1, today.Month, 14);

        Assert.Equal(
            $"{today.Year - 1}-{today.Month:00}-14 09:30",
            AiTaskWhen.Near(Kst(sameMonthLastYear, 9, 30)));
    }

    /// <summary>시각이 없으면 비운다 — 적을 것이 없는 자리에 오늘을 적지 않는다.</summary>
    [Fact]
    public void 시각이_없으면_빈_표시다()
    {
        Assert.Equal("-", AiTaskWhen.Near(null));
    }

    /// <summary>카드와 휴대폰 머리띠가 같은 규칙으로 적는다 — RunAt(compact: true) 가 이 표기를 쓴다.</summary>
    [Fact]
    public void 좁은_자리의_실행일시도_같은_규칙이다()
    {
        var today = AppTime.Today;
        var task = new AiTaskDto { StartedAt = Kst(today, 9, 30) };

        Assert.Equal("09:30", AiTaskWhen.RunAt(task, compact: true));
        Assert.Equal(AppTime.ToKorea(task.StartedAt!.Value).ToString("yyyy-MM-dd HH:mm"),
            AiTaskWhen.RunAt(task));
    }

    /// <summary>머리띠의 시각 둘도 같은 규칙이다 — 책상에서도 yyyy-MM-dd HH:mm 이 붙으면 앞 열 글자가 거의 늘 같은 글자가 된다. 되돌아가도 화면은 멀쩡해 눈으로는 못 지킨다.</summary>
    [Fact]
    public void 머리띠의_작성과_동작은_짧은_표기를_쓴다()
    {
        var razor = TaskViewRazor();

        // 작성 — 보낸 시각. 짧게 적고 연월일은 `title` 에 남긴다.
        Assert.Contains("title=\"@t.CreDt.Kst(\"yyyy-MM-dd HH:mm\")\">@AiTaskWhen.Near(t.CreDt)", razor);

        // 동작 — 실제로 돈 시각. 같은 짝이다.
        Assert.Contains("title=\"@AiTaskWhen.RunAt(t)\">@AiTaskWhen.RunAt(t, compact: true)", razor);
    }

    /// <summary>폭으로 갈리지 않는다 — 갈라 두면 같은 건의 같은 시각이 기기마다 다른 꼴로 선다.</summary>
    [Fact]
    public void 머리띠가_폭을_재지_않는다()
    {
        var razor = TaskViewRazor();

        Assert.DoesNotContain("_isPhone", razor);
        Assert.DoesNotContain("DxLayoutBreakpoint", razor);
    }

    /// <summary>작업에 걸린 시간은 mm:ss 로 적는다. 휴대폰에서 2줄로 꺾이지 않도록 두 자리 분·초로 통일한다.</summary>
    [Fact]
    public void 작업에_걸린_시간은_mmss_꼴로_적는다()
    {
        // 5초 -> 00:05
        Assert.Equal("00:05", AiTaskWhen.Elapsed(new AiTaskDto { DurationMs = 5000 }));

        // 3분 24초 -> 03:24
        Assert.Equal("03:24", AiTaskWhen.Elapsed(new AiTaskDto { DurationMs = 204000 }));

        // 1시간 2분 3초 -> 1:02:03
        Assert.Equal("1:02:03", AiTaskWhen.Elapsed(new AiTaskDto { DurationMs = 3723000 }));

        // 잰 시각이 없는 경우
        Assert.Equal("-", AiTaskWhen.Elapsed(new AiTaskDto()));

        // 대기 중인 경우
        Assert.Equal("대기 중", AiTaskWhen.Elapsed(new AiTaskDto { TaskStatus = "queued" }));
    }

    private static string TaskViewRazor() => RazorSource.Read(Path.Combine(
        SolutionRoot(), "src", "Apps", "JSini.Web.ProjMng", "Components", "Shared", "AiTaskView.razor"));

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

    /// <summary>한국 벽시계로 적은 날·시각을, 서버가 주는 꼴(UTC)로 되돌린다.</summary>
    private static DateTime Kst(DateOnly day, int hour, int minute) =>
        AppTime.FromKorea(day.ToDateTime(new TimeOnly(hour, minute)));
}
